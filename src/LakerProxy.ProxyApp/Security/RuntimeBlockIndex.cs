using System;
using System.Collections.Concurrent;
using System.Threading;

namespace LakerProxy.ProxyApp.Security;

internal sealed class RuntimeBlockIndex<TKey>
    where TKey : notnull
{
  private const long PermanentBlock = long.MaxValue;
  private const long CleanupMarker = long.MinValue;

  private readonly ConcurrentDictionary<TKey, long> _entries = new();

  public bool IsEmpty => _entries.IsEmpty;

  public bool Contains(TKey key, long nowUtcTicks)
  {
    if (!_entries.TryGetValue(key, out var expiresAtUtcTicks))
      return false;

    if (expiresAtUtcTicks == CleanupMarker)
      return false;

    return expiresAtUtcTicks == PermanentBlock ||
           expiresAtUtcTicks > nowUtcTicks;
  }

  public void AddOrExtend(TKey key, long expiresAtUtcTicks)
  {
    AddOrExtendCore(key, expiresAtUtcTicks);
  }

  public void AddOrExtendPermanently(TKey key)
  {
    AddOrExtendCore(key, PermanentBlock);
  }

  public bool Remove(TKey key)
  {
    return _entries.TryRemove(key, out _);
  }

  public void RemoveExpired(long nowUtcTicks)
  {
    foreach (var entry in _entries)
    {
      var expiresAtUtcTicks = entry.Value;

      if (expiresAtUtcTicks == PermanentBlock)
        continue;

      if (expiresAtUtcTicks == CleanupMarker)
        continue;

      if (expiresAtUtcTicks > nowUtcTicks)
        continue;

      // Claim entry for removal.
      // Zolang CleanupMarker actief is, kunnen AddOrExtend-calls deze key niet muteren,
      // maar zullen ze wachten tot de remove afgerond is.
      if (_entries.TryUpdate(
              entry.Key,
              CleanupMarker,
              expiresAtUtcTicks))
      {
        _entries.TryRemove(entry.Key, out _);
      }
    }
  }

  private void AddOrExtendCore(TKey key, long requestedExpiryUtcTicks)
  {
    while (true)
    {
      if (!_entries.TryGetValue(key, out var currentExpiryUtcTicks))
      {
        if (_entries.TryAdd(key, requestedExpiryUtcTicks))
          return;

        continue;
      }

      if (currentExpiryUtcTicks == CleanupMarker)
      {
        Thread.SpinWait(1);
        continue;
      }

      var nextExpiryUtcTicks =
          currentExpiryUtcTicks == PermanentBlock ||
          requestedExpiryUtcTicks == PermanentBlock
              ? PermanentBlock
              : Math.Max(currentExpiryUtcTicks, requestedExpiryUtcTicks);

      if (nextExpiryUtcTicks == currentExpiryUtcTicks)
        return;

      if (_entries.TryUpdate(
              key,
              nextExpiryUtcTicks,
              currentExpiryUtcTicks))
      {
        return;
      }
    }
  }
}
