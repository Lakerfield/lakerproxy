using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;
using DnsClient.Protocol;
using LakerProxy.WebApi;

namespace LakerProxy.ProxyPlayground.Tools.DnsCheck
{
  /// <summary>
  /// Cache-less DNS lookups against public resolvers (fallback: system resolvers),
  /// plus the comparison of the found addresses with the proxy server addresses.
  /// No UI dependencies.
  /// </summary>
  public class DnsChecker
  {
    public const string ProbePrefix = "lakerproxy-dnscheck-";

    public static readonly IPAddress[] DefaultResolvers =
    {
      IPAddress.Parse("1.1.1.1"),
      IPAddress.Parse("8.8.8.8"),
    };

    private readonly LookupClient _publicClient;
    private readonly Lazy<LookupClient> _systemClient;

    public DnsChecker() : this(DefaultResolvers)
    {
    }

    public DnsChecker(IEnumerable<IPAddress> resolvers)
    {
      _publicClient = new LookupClient(ConfigureOptions(new LookupClientOptions(resolvers.ToArray())));
      _systemClient = new Lazy<LookupClient>(() =>
      {
        var nameServers = NameServer.ResolveNameServers(skipIPv6SiteLocal: true, fallbackToGooglePublicDns: false);
        if (nameServers == null || nameServers.Count == 0)
          return null;
        return new LookupClient(ConfigureOptions(new LookupClientOptions(nameServers.ToArray())));
      });
    }

    private static LookupClientOptions ConfigureOptions(LookupClientOptions options)
    {
      options.UseCache = false;
      options.Timeout = TimeSpan.FromSeconds(3);
      options.Retries = 1;
      options.ThrowDnsErrors = false;
      options.UseTcpFallback = true;
      // DO bit: ask for RRSIG records and let the resolver report validation via the AD flag
      options.RequestDnsSecRecords = true;
      // an empty AAAA answer or an NXDOMAIN is a valid answer; don't ask the next resolver
      options.ContinueOnEmptyResponse = false;
      options.ContinueOnDnsError = false;
      return options;
    }


    public static string NewProbeLabel()
    {
      return ProbePrefix + Guid.NewGuid().ToString("N").Substring(0, 4);
    }

    /// <summary>
    /// Names to query for a website, derived from the certificate scope the same
    /// way YarpBackgroundService derives the hostnames to serve.
    /// </summary>
    public static IReadOnlyList<string> NamesToCheck(string hostname, CertificateScope scope, string probeLabel)
    {
      hostname = Normalize(hostname);
      switch (scope)
      {
        case CertificateScope.Wildcard:
          return new[] { $"{probeLabel}.{hostname}" };
        case CertificateScope.WildcardWithApex:
          return new[] { $"{probeLabel}.{hostname}", hostname };
        default:
          return new[] { hostname };
      }
    }

    public static string Normalize(string hostname)
    {
      return (hostname ?? "").Trim().TrimEnd('.').ToLowerInvariant();
    }


    /// <summary>
    /// A + AAAA of a proxy server hostname; used to build the expected set
    /// </summary>
    public async Task<IReadOnlyList<IPAddress>> ResolveServerAsync(string hostname, CancellationToken ct)
    {
      hostname = Normalize(hostname);
      if (hostname.Length == 0)
        return Array.Empty<IPAddress>();

      var v4 = LookupAsync(hostname, AddressFamily.InterNetwork, ct);
      var v6 = LookupAsync(hostname, AddressFamily.InterNetworkV6, ct);
      await Task.WhenAll(v4, v6);

      return v4.Result.Addresses.Concat(v6.Result.Addresses).ToArray();
    }


    public async Task<DnsLookup> LookupAsync(string name, AddressFamily family, CancellationToken ct)
    {
      var queryType = family == AddressFamily.InterNetworkV6 ? QueryType.AAAA : QueryType.A;

      try
      {
        return await LookupAsync(_publicClient, name, family, queryType, ct);
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (Exception publicException)
      {
        // transport failure (e.g. outbound udp/53 blocked): retry once via the system resolvers
        var systemClient = _systemClient.Value;
        if (systemClient == null)
          return new DnsLookup { Name = name, Family = family, Error = publicException.Message };

        try
        {
          return await LookupAsync(systemClient, name, family, queryType, ct);
        }
        catch (OperationCanceledException)
        {
          throw;
        }
        catch (Exception systemException)
        {
          return new DnsLookup { Name = name, Family = family, Error = $"{publicException.Message}; system resolver: {systemException.Message}" };
        }
      }
    }

    private static async Task<DnsLookup> LookupAsync(LookupClient client, string name, AddressFamily family, QueryType queryType, CancellationToken ct)
    {
      var response = await client.QueryAsync(name, queryType, QueryClass.IN, ct);
      var resolver = response.NameServer?.ToString() ?? "";

      var authenticData = response.Header.IsAuthenticData;

      if (response.Header.ResponseCode == DnsHeaderResponseCode.NotExistentDomain)
        return new DnsLookup { Name = name, Family = family, NxDomain = true, Resolver = resolver, AuthenticData = authenticData };

      if (response.Header.ResponseCode == DnsHeaderResponseCode.ServerFailure)
        return new DnsLookup { Name = name, Family = family, Resolver = resolver, ServerFailure = true, Error = response.ErrorMessage };

      if (response.HasError)
        return new DnsLookup { Name = name, Family = family, Resolver = resolver, Error = response.ErrorMessage };

      var cnames = response.Answers
        .OfType<CNameRecord>()
        .Select(c => c.CanonicalName.Value.TrimEnd('.'))
        .ToArray();

      var records = family == AddressFamily.InterNetworkV6
        ? response.Answers.OfType<AaaaRecord>().Select(r => (r.Address, r.TimeToLive)).ToArray()
        : response.Answers.OfType<ARecord>().Select(r => (r.Address, r.TimeToLive)).ToArray();

      return new DnsLookup
      {
        Name = name,
        Family = family,
        CnameChain = cnames,
        Records = records,
        Resolver = resolver,
        AuthenticData = authenticData,
        HasRrsig = response.Answers.OfType<RRSigRecord>().Any(),
      };
    }


    /// <summary>
    /// True when a DS record exists for the name or one of its parent zones (down to two labels),
    /// i.e. the zone is signed at the registry. Used to tell a broken DNSSEC chain from a plain SERVFAIL.
    /// </summary>
    public async Task<bool> HasDsRecordAsync(string hostname, CancellationToken ct)
    {
      var labels = Normalize(hostname).Split('.', StringSplitOptions.RemoveEmptyEntries);
      for (var skip = 0; labels.Length - skip >= 2; skip++)
      {
        var zone = string.Join(".", labels.Skip(skip));
        try
        {
          var response = await _publicClient.QueryAsync(zone, QueryType.DS, QueryClass.IN, ct);
          if (response.Answers.OfType<DsRecord>().Any())
            return true;
        }
        catch (OperationCanceledException)
        {
          throw;
        }
        catch (Exception)
        {
          // transport error on this level; keep walking up, the parent may still answer
        }
      }
      return false;
    }

    /// <summary>
    /// DNSSEC verdict for a website from the A/AAAA lookups of all its names
    /// </summary>
    public async Task<DnssecCheckResult> EvaluateDnssecAsync(string hostname, IReadOnlyList<DnsLookup> lookups, CancellationToken ct)
    {
      if (lookups.Any(l => l.ServerFailure))
      {
        var hasDs = await HasDsRecordAsync(hostname, ct);
        return new DnssecCheckResult
        {
          Status = hasDs ? DnssecStatus.Broken : DnssecStatus.Unknown,
          Lookups = lookups,
          Message = hasDs
            ? "resolver returned SERVFAIL while a DS record exists: the signed zone fails validation"
            : "server failure",
        };
      }

      var answered = lookups.Where(l => l.Error == null).ToArray();
      if (answered.Length == 0)
        return new DnssecCheckResult { Status = DnssecStatus.Unknown, Lookups = lookups, Message = lookups.FirstOrDefault()?.Error ?? "no answer" };

      if (answered.All(l => l.AuthenticData))
        return new DnssecCheckResult { Status = DnssecStatus.Signed, Lookups = lookups };

      if (answered.Any(l => l.AuthenticData))
        return new DnssecCheckResult { Status = DnssecStatus.Signed, Lookups = lookups, Message = "not every answer carried the AD flag" };

      if (answered.Any(l => l.HasRrsig))
        return new DnssecCheckResult { Status = DnssecStatus.NotValidated, Lookups = lookups, Message = "zone is signed (RRSIG) but the resolver that answered did not validate it" };

      return new DnssecCheckResult { Status = DnssecStatus.Unsigned, Lookups = lookups };
    }


    /// <summary>
    /// Looks up all names of a website for one address family and compares with the expected set
    /// </summary>
    public async Task<DnsCheckResult> CheckAsync(IReadOnlyList<string> names, CertificateScope scope, AddressFamily family,
      IReadOnlyList<(string Server, IPAddress Ip)> expected, CancellationToken ct)
    {
      var lookups = await Task.WhenAll(names.Select(n => LookupAsync(n, family, ct)));
      return Compare(scope, family, lookups, expected);
    }

    public static DnsCheckResult Compare(CertificateScope scope, AddressFamily family, IReadOnlyList<DnsLookup> lookups,
      IReadOnlyList<(string Server, IPAddress Ip)> expected)
    {
      var expectedForFamily = expected.Where(e => e.Ip.AddressFamily == family).ToArray();
      var expectedSet = new HashSet<IPAddress>(expectedForFamily.Select(e => e.Ip));

      var worst = DnsCheckStatus.Ok;
      string message = "";
      foreach (var lookup in lookups)
      {
        var status = Evaluate(lookup, expectedSet);
        if (DnsCheckResult.Rank(status) < DnsCheckResult.Rank(worst))
        {
          worst = status;
          message = status == DnsCheckStatus.Error ? lookup.Error : "";
        }
      }

      return new DnsCheckResult
      {
        Status = worst,
        Family = family,
        Scope = scope,
        Lookups = lookups,
        Expected = expectedForFamily,
        Message = message,
      };
    }

    private static DnsCheckStatus Evaluate(DnsLookup lookup, HashSet<IPAddress> expected)
    {
      if (lookup.Error != null)
        return DnsCheckStatus.Error;
      if (lookup.NxDomain)
        return DnsCheckStatus.NoDomain;

      var found = lookup.Addresses.ToArray();
      if (found.Length == 0)
        return DnsCheckStatus.None;
      if (expected.Count == 0)
        return DnsCheckStatus.Unknown;

      var matching = found.Count(expected.Contains);
      if (matching == found.Length)
        return DnsCheckStatus.Ok;
      if (matching > 0)
        return DnsCheckStatus.Partial;
      return DnsCheckStatus.Mismatch;
    }
  }
}
