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
    public string Resolver { get; init; } = "";
    public string Error { get; init; }

    public IEnumerable<IPAddress> Addresses => Records.Select(r => r.Ip);
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
