using System;

namespace LakerProxy.ProxyApp.Security;

public sealed class IpBlocklistSnapshot
{
  private readonly Ipv4Range[] _ipv4Ranges;
  private readonly Ipv6Prefix64Range[] _ipv6Prefix64Ranges;

  public static readonly IpBlocklistSnapshot Empty =
    new(
      Array.Empty<Ipv4Range>(),
      Array.Empty<Ipv6Prefix64Range>());

  internal IpBlocklistSnapshot(
    Ipv4Range[] ipv4Ranges,
    Ipv6Prefix64Range[] ipv6Prefix64Ranges)
  {
    _ipv4Ranges = ipv4Ranges;
    _ipv6Prefix64Ranges = ipv6Prefix64Ranges;
  }

  public bool ContainsIpv4(uint value)
  {
    var ranges = _ipv4Ranges;
    var low = 0;
    var high = ranges.Length - 1;

    while (low <= high)
    {
      var mid = low + ((high - low) >> 1);
      ref readonly var range = ref ranges[mid];

      if (value < range.Start)
      {
        high = mid - 1;
      }
      else if (value > range.End)
      {
        low = mid + 1;
      }
      else
      {
        return true;
      }
    }

    return false;
  }

  public bool ContainsIpv6Prefix64(ulong value)
  {
    var ranges = _ipv6Prefix64Ranges;
    var low = 0;
    var high = ranges.Length - 1;

    while (low <= high)
    {
      var mid = low + ((high - low) >> 1);
      ref readonly var range = ref ranges[mid];

      if (value < range.Start)
      {
        high = mid - 1;
      }
      else if (value > range.End)
      {
        low = mid + 1;
      }
      else
      {
        return true;
      }
    }

    return false;
  }
}

public readonly record struct Ipv4Range(uint Start, uint End);

public readonly record struct Ipv6Prefix64Range(ulong Start, ulong End);
