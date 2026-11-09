using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace LakerProxy.ProxyApp.Security;

public sealed class IpBlocklistBuilder
{
  private readonly List<Ipv4Range> _ipv4Ranges = [];
  private readonly List<Ipv6Prefix64Range> _ipv6Prefix64Ranges = [];

  public void Add(string ipOrCidr)
  {
    if (!TryAdd(ipOrCidr, out var error))
      throw new FormatException(error);
  }

  public void AddLines(IEnumerable<string> lines)
  {
    ArgumentNullException.ThrowIfNull(lines);

    foreach (var rawLine in lines)
    {
      var line = rawLine.Trim();

      if (line.Length == 0)
        continue;

      if (line.StartsWith('#'))
        continue;

      Add(line);
    }
  }

  public bool TryAdd(string ipOrCidr, out string? error)
  {
    error = null;

    if (string.IsNullOrWhiteSpace(ipOrCidr))
    {
      error = "IP or CIDR value is empty.";
      return false;
    }

    var text = ipOrCidr.Trim();

    var slashIndex = text.IndexOf('/');

    string ipText;
    int? prefixLength = null;

    if (slashIndex >= 0)
    {
      ipText = text[..slashIndex];

      var prefixText = text[(slashIndex + 1)..];

      if (!int.TryParse(prefixText, out var parsedPrefix))
      {
        error = $"Invalid CIDR prefix length: '{prefixText}'.";
        return false;
      }

      prefixLength = parsedPrefix;
    }
    else
    {
      ipText = text;
    }

    if (!IPAddress.TryParse(ipText, out var address))
    {
      error = $"Invalid IP address: '{ipText}'.";
      return false;
    }

    address = IpAddressNumeric.Normalize(address);

    switch (address.AddressFamily)
    {
      case AddressFamily.InterNetwork:
        return TryAddIpv4(address, prefixLength, out error);

      case AddressFamily.InterNetworkV6:
        return TryAddIpv6Prefix64(address, prefixLength, out error);

      default:
        error = $"Unsupported address family: {address.AddressFamily}.";
        return false;
    }
  }

  public IpBlocklistSnapshot Build()
  {
    var ipv4 = MergeIpv4Ranges(_ipv4Ranges);
    var ipv6 = MergeIpv6Prefix64Ranges(_ipv6Prefix64Ranges);

    return new IpBlocklistSnapshot(ipv4, ipv6);
  }

  private bool TryAddIpv4(
      IPAddress address,
      int? prefixLength,
      out string? error)
  {
    error = null;

    var prefix = prefixLength ?? 32;

    if (prefix < 0 || prefix > 32)
    {
      error = $"Invalid IPv4 prefix length: {prefix}.";
      return false;
    }

    var value = IpAddressNumeric.ToUInt32(address);

    uint mask = prefix switch
    {
      0 => 0u,
      32 => uint.MaxValue,
      _ => uint.MaxValue << (32 - prefix)
    };

    var start = value & mask;
    var end = start | ~mask;

    _ipv4Ranges.Add(new Ipv4Range(start, end));
    return true;
  }

  private bool TryAddIpv6Prefix64(
      IPAddress address,
      int? prefixLength,
      out string? error)
  {
    error = null;

    var prefix = prefixLength ?? 128;

    if (prefix < 0 || prefix > 128)
    {
      error = $"Invalid IPv6 prefix length: {prefix}.";
      return false;
    }

    var prefix64 = IpAddressNumeric.ToIpv6Prefix64(address);

    ulong start;
    ulong end;

    if (prefix >= 64)
    {
      // Alles langer dan /64 wordt bewust herleid tot precies één /64.
      start = prefix64;
      end = prefix64;
    }
    else
    {
      var mask = prefix switch
      {
        0 => 0UL,
        _ => ulong.MaxValue << (64 - prefix)
      };

      start = prefix64 & mask;
      end = start | ~mask;
    }

    _ipv6Prefix64Ranges.Add(new Ipv6Prefix64Range(start, end));
    return true;
  }

  private static Ipv4Range[] MergeIpv4Ranges(List<Ipv4Range> ranges)
  {
    if (ranges.Count == 0)
      return Array.Empty<Ipv4Range>();

    ranges.Sort(static (left, right) =>
    {
      var startComparison = left.Start.CompareTo(right.Start);

      return startComparison != 0
          ? startComparison
          : left.End.CompareTo(right.End);
    });

    var merged = new List<Ipv4Range>(ranges.Count);

    var current = ranges[0];

    for (var i = 1; i < ranges.Count; i++)
    {
      var next = ranges[i];

      if (OverlapsOrAdjacent(current.End, next.Start))
      {
        current = new Ipv4Range(
            current.Start,
            Math.Max(current.End, next.End));
      }
      else
      {
        merged.Add(current);
        current = next;
      }
    }

    merged.Add(current);

    return merged.ToArray();
  }

  private static Ipv6Prefix64Range[] MergeIpv6Prefix64Ranges(
      List<Ipv6Prefix64Range> ranges)
  {
    if (ranges.Count == 0)
      return Array.Empty<Ipv6Prefix64Range>();

    ranges.Sort(static (left, right) =>
    {
      var startComparison = left.Start.CompareTo(right.Start);

      return startComparison != 0
          ? startComparison
          : left.End.CompareTo(right.End);
    });

    var merged = new List<Ipv6Prefix64Range>(ranges.Count);

    var current = ranges[0];

    for (var i = 1; i < ranges.Count; i++)
    {
      var next = ranges[i];

      if (OverlapsOrAdjacent(current.End, next.Start))
      {
        current = new Ipv6Prefix64Range(
            current.Start,
            Math.Max(current.End, next.End));
      }
      else
      {
        merged.Add(current);
        current = next;
      }
    }

    merged.Add(current);

    return merged.ToArray();
  }

  private static bool OverlapsOrAdjacent(uint currentEnd, uint nextStart)
  {
    return nextStart <= currentEnd ||
           (currentEnd != uint.MaxValue && nextStart == currentEnd + 1);
  }

  private static bool OverlapsOrAdjacent(ulong currentEnd, ulong nextStart)
  {
    return nextStart <= currentEnd ||
           (currentEnd != ulong.MaxValue && nextStart == currentEnd + 1);
  }
}
