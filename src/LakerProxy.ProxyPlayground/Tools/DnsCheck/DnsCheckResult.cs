using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LakerProxy.WebApi;

namespace LakerProxy.ProxyPlayground.Tools.DnsCheck
{
  public enum DnsCheckStatus
  {
    NotChecked,
    Checking,
    Ok,
    Partial,
    Mismatch,
    /// <summary>Domain exists but has no record of the requested family</summary>
    None,
    /// <summary>NXDOMAIN</summary>
    NoDomain,
    /// <summary>Records found, but no linked server has an address of this family</summary>
    Unknown,
    Error,
    Skipped,
  }

  /// <summary>
  /// Result of one DNS query (one name, one address family)
  /// </summary>
  public class DnsLookup
  {
    public string Name { get; init; } = "";
    public AddressFamily Family { get; init; }
    public IReadOnlyList<string> CnameChain { get; init; } = Array.Empty<string>();
    public IReadOnlyList<(IPAddress Ip, int Ttl)> Records { get; init; } = Array.Empty<(IPAddress, int)>();
    public bool NxDomain { get; init; }
    /// <summary>RCODE SERVFAIL; with a validating resolver usually a broken DNSSEC chain</summary>
    public bool ServerFailure { get; init; }
    /// <summary>AD flag: the resolver validated the DNSSEC chain of this answer</summary>
    public bool AuthenticData { get; init; }
    /// <summary>RRSIG records were returned (zone is signed), regardless of validation</summary>
    public bool HasRrsig { get; init; }
    public string Resolver { get; init; } = "";
    public string Error { get; init; }

    public IEnumerable<IPAddress> Addresses => Records.Select(r => r.Ip);
  }

  public enum DnssecStatus
  {
    NotChecked,
    Checking,
    /// <summary>Resolver validated every answer (AD flag)</summary>
    Signed,
    /// <summary>No AD flag and no RRSIG: zone is not signed</summary>
    Unsigned,
    /// <summary>SERVFAIL while a DS record exists for the zone: signed but validation fails</summary>
    Broken,
    /// <summary>RRSIG present but no AD flag: the resolver that answered does not validate</summary>
    NotValidated,
    Unknown,
    Skipped,
  }

  /// <summary>
  /// One grid cell: DNSSEC verdict for one website, derived from the A/AAAA lookups of all its names
  /// </summary>
  public class DnssecCheckResult
  {
    public DnssecStatus Status { get; init; }
    public IReadOnlyList<DnsLookup> Lookups { get; init; } = Array.Empty<DnsLookup>();
    public string Message { get; init; } = "";
    public DateTime CheckedAt { get; init; } = DateTime.Now;

    public static DnssecCheckResult Checking() => new() { Status = DnssecStatus.Checking };
    public static DnssecCheckResult Skipped(string reason) => new() { Status = DnssecStatus.Skipped, Message = reason };
    public static DnssecCheckResult Failed(string message) => new() { Status = DnssecStatus.Unknown, Message = message };

    public static int Rank(DnssecStatus status) => status switch
    {
      DnssecStatus.Broken => 0,
      DnssecStatus.Unknown => 1,
      DnssecStatus.NotValidated => 2,
      DnssecStatus.Unsigned => 3,
      DnssecStatus.Signed => 4,
      DnssecStatus.Skipped => 5,
      DnssecStatus.Checking => 6,
      _ => 7,
    };

    public string ShortText => Status switch
    {
      DnssecStatus.NotChecked => "",
      DnssecStatus.Checking => "...",
      DnssecStatus.Signed => "signed",
      DnssecStatus.Unsigned => "unsigned",
      DnssecStatus.Broken => "BROKEN",
      DnssecStatus.NotValidated => "not validated",
      DnssecStatus.Unknown => "unknown" + (string.IsNullOrWhiteSpace(Message) ? "" : ": " + Message),
      DnssecStatus.Skipped => "skipped",
      _ => Status.ToString(),
    };

    public string ToolTipText
    {
      get
      {
        if (Status is DnssecStatus.NotChecked or DnssecStatus.Checking)
          return null;

        var sb = new StringBuilder();
        sb.AppendLine($"DNSSEC: {Status}");
        if (!string.IsNullOrWhiteSpace(Message))
          sb.AppendLine(Message);
        sb.AppendLine($"Checked at {CheckedAt:yyyy-MM-dd HH:mm:ss}");

        foreach (var lookup in Lookups)
        {
          var family = lookup.Family == AddressFamily.InterNetworkV6 ? "AAAA" : "A";
          var state = lookup.ServerFailure ? "SERVFAIL"
            : lookup.Error != null ? $"error: {lookup.Error}"
            : $"AD={(lookup.AuthenticData ? "yes" : "no")} RRSIG={(lookup.HasRrsig ? "yes" : "no")}";
          sb.AppendLine($"  {family} {lookup.Name} via {lookup.Resolver}: {state}");
        }

        return sb.ToString().TrimEnd();
      }
    }
  }

  /// <summary>
  /// One grid cell: the verdict for one website and one address family
  /// </summary>
  public class DnsCheckResult
  {
    public DnsCheckStatus Status { get; init; }
    public AddressFamily Family { get; init; }
    public CertificateScope Scope { get; init; }
    public IReadOnlyList<DnsLookup> Lookups { get; init; } = Array.Empty<DnsLookup>();
    public IReadOnlyList<(string Server, IPAddress Ip)> Expected { get; init; } = Array.Empty<(string, IPAddress)>();
    public string Message { get; init; } = "";
    public DateTime CheckedAt { get; init; } = DateTime.Now;

    public bool IsWildcard => Scope is CertificateScope.Wildcard or CertificateScope.WildcardWithApex;

    public string FamilyName => Family == AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4";

    public static DnsCheckResult Checking(AddressFamily family) => new() { Status = DnsCheckStatus.Checking, Family = family };
    public static DnsCheckResult Skipped(AddressFamily family, string reason) => new() { Status = DnsCheckStatus.Skipped, Family = family, Message = reason };
    public static DnsCheckResult Failed(AddressFamily family, string message) => new() { Status = DnsCheckStatus.Error, Family = family, Message = message };

    /// <summary>
    /// Sort rank: problems first, not-checked last
    /// </summary>
    public static int Rank(DnsCheckStatus status) => status switch
    {
      DnsCheckStatus.Error => 0,
      DnsCheckStatus.NoDomain => 1,
      DnsCheckStatus.Mismatch => 2,
      DnsCheckStatus.Partial => 3,
      DnsCheckStatus.Unknown => 4,
      DnsCheckStatus.None => 5,
      DnsCheckStatus.Ok => 6,
      DnsCheckStatus.Skipped => 7,
      DnsCheckStatus.Checking => 8,
      _ => 9,
    };

    private string FoundText => string.Join(", ", Lookups.SelectMany(l => l.Addresses).Distinct().Select(ip => ip.ToString()));

    public string ShortText
    {
      get
      {
        var suffix = IsWildcard ? " (wildcard)" : "";
        switch (Status)
        {
          case DnsCheckStatus.NotChecked:
            return "";
          case DnsCheckStatus.Checking:
            return "...";
          case DnsCheckStatus.Ok:
            return FoundText + suffix;
          case DnsCheckStatus.Partial:
            return "partial " + FoundText + suffix;
          case DnsCheckStatus.Mismatch:
            return "MISMATCH " + FoundText + suffix;
          case DnsCheckStatus.None:
            return "none" + suffix;
          case DnsCheckStatus.NoDomain:
            return "nxdomain" + suffix;
          case DnsCheckStatus.Unknown:
            return "no server ip " + FoundText + suffix;
          case DnsCheckStatus.Error:
            return "error: " + Message;
          case DnsCheckStatus.Skipped:
            return "skipped";
          default:
            return Status.ToString();
        }
      }
    }

    public string ToolTipText
    {
      get
      {
        if (Status is DnsCheckStatus.NotChecked or DnsCheckStatus.Checking)
          return null;

        var sb = new StringBuilder();
        sb.AppendLine($"{FamilyName}: {Status}");
        if (!string.IsNullOrWhiteSpace(Message))
          sb.AppendLine(Message);
        sb.AppendLine($"Checked at {CheckedAt:yyyy-MM-dd HH:mm:ss}");

        foreach (var lookup in Lookups)
        {
          sb.AppendLine();
          sb.AppendLine($"Lookup {lookup.Name} via {lookup.Resolver}");
          if (lookup.Error != null)
            sb.AppendLine($"  error: {lookup.Error}");
          else if (lookup.NxDomain)
            sb.AppendLine("  NXDOMAIN");
          foreach (var cname in lookup.CnameChain)
            sb.AppendLine($"  CNAME -> {cname}");
          foreach (var (ip, ttl) in lookup.Records)
            sb.AppendLine($"  {ip} (ttl {ttl}s)");
          if (lookup.Error == null && !lookup.NxDomain && lookup.Records.Count == 0)
            sb.AppendLine("  (no records)");
        }

        sb.AppendLine();
        sb.AppendLine("Expected:");
        if (Expected.Count == 0)
          sb.AppendLine($"  (no server with an {FamilyName} address)");
        foreach (var group in Expected.GroupBy(e => e.Server))
          sb.AppendLine($"  {group.Key}: {string.Join(", ", group.Select(e => e.Ip.ToString()))}");

        return sb.ToString().TrimEnd();
      }
    }
  }
}
