using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace LakerProxy.ProxyApp.Security;
//TODO: namespace Lakerfield.ReverseProxy.Security;

internal static class IpAddressNumeric
{
  public static IPAddress Normalize(IPAddress address)
  {
    return address.IsIPv4MappedToIPv6
      ? address.MapToIPv4()
      : address;
  }

  public static uint ToUInt32(IPAddress ipv4Address)
  {
    if (ipv4Address.AddressFamily != AddressFamily.InterNetwork)
      throw new ArgumentException("Expected an IPv4 address.", nameof(ipv4Address));

    Span<byte> bytes = stackalloc byte[4];

    if (!ipv4Address.TryWriteBytes(bytes, out var bytesWritten) || bytesWritten != 4)
      throw new InvalidOperationException("Failed to write IPv4 address bytes.");

    return BinaryPrimitives.ReadUInt32BigEndian(bytes);
  }

  public static ulong ToIpv6Prefix64(IPAddress ipv6Address)
  {
    if (ipv6Address.AddressFamily != AddressFamily.InterNetworkV6)
      throw new ArgumentException("Expected an IPv6 address.", nameof(ipv6Address));

    Span<byte> bytes = stackalloc byte[16];

    if (!ipv6Address.TryWriteBytes(bytes, out var bytesWritten) || bytesWritten != 16)
      throw new InvalidOperationException("Failed to write IPv6 address bytes.");

    return BinaryPrimitives.ReadUInt64BigEndian(bytes[..8]);
  }
}
