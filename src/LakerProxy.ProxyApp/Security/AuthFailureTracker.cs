using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LakerProxy.ProxyApp.Security;

public static class AuthFailureTrackerExtensions
{
  public static IServiceCollection AddAuthFailureTracking(
    this IServiceCollection services,
    Action<AuthFailureTrackerOptions>? configure = null)
  {
    services.AddOptions<AuthFailureTrackerOptions>();

    if (configure is not null)
      services.Configure(configure);

    services.AddSingleton<AuthFailureTracker>();

    return services;
  }
}

public sealed class AuthFailureTrackerOptions
{
  /// <summary>
  /// Aantal mislukte pogingen binnen FailureWindow voordat het IP wordt geblokkeerd.
  /// </summary>
  public int MaxFailures { get; set; } = 10;

  public TimeSpan FailureWindow { get; set; } = TimeSpan.FromMinutes(5);

  public TimeSpan BlockDuration { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Telt mislukte bearer-token authenticaties per remote IP en blokkeert het IP
/// via de MaliciousIpBlocklist zodra de drempel binnen het venster wordt
/// overschreden. Loopback-adressen worden nooit geblokkeerd.
/// </summary>
public sealed class AuthFailureTracker
{
  private readonly MaliciousIpBlocklist _blocklist;
  private readonly AuthFailureTrackerOptions _options;
  private readonly TimeProvider _timeProvider;

  private readonly object _lock = new();
  private readonly Dictionary<IPAddress, Entry> _failures = new();

  private sealed class Entry
  {
    public long WindowEndUtcTicks;
    public int Count;
  }

  public AuthFailureTracker(
    MaliciousIpBlocklist blocklist,
    IOptions<AuthFailureTrackerOptions> options,
    TimeProvider? timeProvider = null)
  {
    _blocklist = blocklist;
    _options = options.Value;
    _timeProvider = timeProvider ?? TimeProvider.System;
  }

  public void RegisterFailure(IPAddress? address)
  {
    if (address is null)
      return;

    address = IpAddressNumeric.Normalize(address);

    if (IPAddress.IsLoopback(address))
      return;

    var nowUtcTicks = _timeProvider.GetUtcNow().UtcTicks;
    bool block;

    lock (_lock)
    {
      if (_failures.Count > 10_000)
        Prune(nowUtcTicks);

      if (!_failures.TryGetValue(address, out var entry) || nowUtcTicks > entry.WindowEndUtcTicks)
      {
        entry = new Entry { WindowEndUtcTicks = nowUtcTicks + _options.FailureWindow.Ticks };
        _failures[address] = entry;
      }

      entry.Count++;
      block = entry.Count >= _options.MaxFailures;
      if (block)
        _failures.Remove(address);
    }

    if (block)
      _blocklist.Block(address, _options.BlockDuration);
  }

  private void Prune(long nowUtcTicks)
  {
    var expired = new List<IPAddress>();
    foreach (var pair in _failures)
      if (nowUtcTicks > pair.Value.WindowEndUtcTicks)
        expired.Add(pair.Key);

    foreach (var address in expired)
      _failures.Remove(address);
  }
}
