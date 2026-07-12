using System;
using System.Collections.Generic;
using System.IO;

namespace LakerProxy.ProxyApp.Security;

public sealed class BlocklistFeedLoader
{
  private readonly MaliciousIpBlocklist _blocklist;

  public BlocklistFeedLoader(MaliciousIpBlocklist blocklist)
  {
    _blocklist = blocklist;
  }

  public void ReloadFromFile(string path)
  {
    ReloadFromLines(File.ReadLines(path));
  }

  public int ReloadFromLines(IEnumerable<string> lines)
  {
    return ReloadFromFeeds(new[] { lines });
  }

  /// <summary>
  /// Parses plain "ip" / "ip/cidr" lines and the dshield block.txt format
  /// ("startip&lt;TAB&gt;endip&lt;TAB&gt;netmask&lt;TAB&gt;...") from one or more feeds
  /// and replaces the feed snapshot with the combined result. Unparsable lines
  /// are skipped. Returns the number of entries added.
  /// </summary>
  public int ReloadFromFeeds(IEnumerable<IEnumerable<string>> feeds)
  {
    var builder = new IpBlocklistBuilder();
    var count = 0;

    foreach (var lines in feeds)
      count += AddLines(builder, lines);

    // an empty result is most likely a broken download, keep the old snapshot
    if (count > 0)
      _blocklist.ReplaceFeedSnapshot(builder.Build());

    return count;
  }

  private static int AddLines(IpBlocklistBuilder builder, IEnumerable<string> lines)
  {
    var count = 0;

    foreach (var rawLine in lines)
    {
      var line = rawLine.Trim();

      if (line.Length == 0)
        continue;

      if (line.StartsWith('#'))
        continue;

      var fields = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      if (fields.Length == 0)
        continue;

      // dshield block.txt: first field is the network start, third the netmask
      var entry = fields.Length >= 3 && int.TryParse(fields[2], out var netmask)
        ? $"{fields[0]}/{netmask}"
        : fields[0];

      if (builder.TryAdd(entry, out _))
        count++;
    }

    return count;
  }
}

/* Voorbeeld bestand:

# IPv4
185.220.100.0/22
45.155.204.17

# IPv6
2a0b:f4c0::/32
2001:db8::bad:cafe
*/
