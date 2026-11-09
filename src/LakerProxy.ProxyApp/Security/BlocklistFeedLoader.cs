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
    var builder = new IpBlocklistBuilder();

    builder.AddLines(File.ReadLines(path));

    var snapshot = builder.Build();

    _blocklist.ReplaceFeedSnapshot(snapshot);
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
