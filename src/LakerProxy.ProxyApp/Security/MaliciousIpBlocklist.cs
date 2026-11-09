using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LakerProxy.ProxyApp.Security;

public sealed class MaliciousIpBlocklist
{
  private readonly TimeProvider _timeProvider;

  private IpBlocklistSnapshot _feedSnapshot = IpBlocklistSnapshot.Empty;

  private readonly RuntimeBlockIndex<uint> _runtimeIpv4 = new();
  private readonly RuntimeBlockIndex<ulong> _runtimeIpv6Prefix64 = new();

  public MaliciousIpBlocklist(TimeProvider? timeProvider = null)
  {
    _timeProvider = timeProvider ?? TimeProvider.System;
  }

  /// <summary>
  /// Atomisch vervangen van de externe/feed denylist.
  /// De bestaande snapshot blijft geldig voor requests die er al mee bezig zijn.
  /// </summary>
  public void ReplaceFeedSnapshot(IpBlocklistSnapshot snapshot)
  {
    ArgumentNullException.ThrowIfNull(snapshot);
    Volatile.Write(ref _feedSnapshot, snapshot);
  }

  /// <summary>
  /// Controleert of een IP geblokkeerd is.
  /// IPv6 wordt uitsluitend op de eerste 64 bits beoordeeld.
  /// </summary>
  public bool IsBlocked(IPAddress? address)
  {
    if (address is null)
      return false;

    address = IpAddressNumeric.Normalize(address);

    var nowUtcTicks = _timeProvider.GetUtcNow().UtcTicks;
    var snapshot = Volatile.Read(ref _feedSnapshot);

    return address.AddressFamily switch
    {
      AddressFamily.InterNetwork =>
          IsBlockedIpv4(address, nowUtcTicks, snapshot),

      AddressFamily.InterNetworkV6 =>
          IsBlockedIpv6Prefix64(address, nowUtcTicks, snapshot),

      _ => false
    };
  }

  /// <summary>
  /// Voeg een tijdelijk runtime block toe.
  /// IPv6 blokkeert het gehele /64-prefix.
  /// Een bestaande blokkade wordt alleen verlengd, nooit verkort.
  /// </summary>
  public void Block(IPAddress address, TimeSpan duration)
  {
    ArgumentNullException.ThrowIfNull(address);

    if (duration <= TimeSpan.Zero)
      throw new ArgumentOutOfRangeException(
          nameof(duration),
          "Block duration must be greater than zero.");

    address = IpAddressNumeric.Normalize(address);

    var nowUtcTicks = _timeProvider.GetUtcNow().UtcTicks;
    var expiresAtUtcTicks = checked(nowUtcTicks + duration.Ticks);

    switch (address.AddressFamily)
    {
      case AddressFamily.InterNetwork:
        {
          var key = IpAddressNumeric.ToUInt32(address);
          _runtimeIpv4.AddOrExtend(key, expiresAtUtcTicks);
          break;
        }

      case AddressFamily.InterNetworkV6:
        {
          var key = IpAddressNumeric.ToIpv6Prefix64(address);
          _runtimeIpv6Prefix64.AddOrExtend(key, expiresAtUtcTicks);
          break;
        }

      default:
        throw new NotSupportedException($"Unsupported address family: {address.AddressFamily}");
    }
  }

  /// <summary>
  /// Voeg een permanente runtime blokkade toe.
  /// IPv6 blokkeert het gehele /64-prefix.
  /// </summary>
  public void BlockPermanently(IPAddress address)
  {
    ArgumentNullException.ThrowIfNull(address);

    address = IpAddressNumeric.Normalize(address);

    switch (address.AddressFamily)
    {
      case AddressFamily.InterNetwork:
        {
          var key = IpAddressNumeric.ToUInt32(address);
          _runtimeIpv4.AddOrExtendPermanently(key);
          break;
        }

      case AddressFamily.InterNetworkV6:
        {
          var key = IpAddressNumeric.ToIpv6Prefix64(address);
          _runtimeIpv6Prefix64.AddOrExtendPermanently(key);
          break;
        }

      default:
        throw new NotSupportedException($"Unsupported address family: {address.AddressFamily}");
    }
  }

  /// <summary>
  /// Verwijdert uitsluitend een runtime blokkade.
  /// Een block uit de feed snapshot blijft uiteraard actief.
  /// </summary>
  public bool RemoveRuntimeBlock(IPAddress address)
  {
    ArgumentNullException.ThrowIfNull(address);

    address = IpAddressNumeric.Normalize(address);

    return address.AddressFamily switch
    {
      AddressFamily.InterNetwork =>
          _runtimeIpv4.Remove(IpAddressNumeric.ToUInt32(address)),

      AddressFamily.InterNetworkV6 =>
          _runtimeIpv6Prefix64.Remove(IpAddressNumeric.ToIpv6Prefix64(address)),

      _ => false
    };
  }

  /// <summary>
  /// Verwijdert verlopen tijdelijke runtime blokkades.
  /// Bedoeld om periodiek via BackgroundService aan te roepen.
  /// </summary>
  public void RemoveExpiredRuntimeBlocks()
  {
    var nowUtcTicks = _timeProvider.GetUtcNow().UtcTicks;

    _runtimeIpv4.RemoveExpired(nowUtcTicks);
    _runtimeIpv6Prefix64.RemoveExpired(nowUtcTicks);
  }

  private bool IsBlockedIpv4(
      IPAddress address,
      long nowUtcTicks,
      IpBlocklistSnapshot snapshot)
  {
    var key = IpAddressNumeric.ToUInt32(address);

    if (!_runtimeIpv4.IsEmpty &&
        _runtimeIpv4.Contains(key, nowUtcTicks))
    {
      return true;
    }

    return snapshot.ContainsIpv4(key);
  }

  private bool IsBlockedIpv6Prefix64(
      IPAddress address,
      long nowUtcTicks,
      IpBlocklistSnapshot snapshot)
  {
    var key = IpAddressNumeric.ToIpv6Prefix64(address);

    if (!_runtimeIpv6Prefix64.IsEmpty &&
        _runtimeIpv6Prefix64.Contains(key, nowUtcTicks))
    {
      return true;
    }

    return snapshot.ContainsIpv6Prefix64(key);
  }
}
