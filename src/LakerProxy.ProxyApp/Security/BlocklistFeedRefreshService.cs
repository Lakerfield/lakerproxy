using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LakerProxy.ProxyApp.Security;

/// <summary>
/// Periodically downloads the external ip blocklist feeds and replaces the feed
/// snapshot of the MaliciousIpBlocklist with the combined result. Does nothing
/// when no feed urls are configured. A failed download keeps the last
/// successful content of that feed, so a temporarily unreachable feed does not
/// lose its entries.
/// </summary>
public sealed class BlocklistFeedRefreshService : BackgroundService
{
  private readonly BlocklistFeedLoader _loader;
  private readonly MaliciousIpFilterOptions _options;
  private readonly ILogger<BlocklistFeedRefreshService> _logger;
  private readonly HttpClient _httpClient;

  private readonly Dictionary<string, string[]> _lastFeedLines = new();

  public BlocklistFeedRefreshService(
    BlocklistFeedLoader loader,
    IOptions<MaliciousIpFilterOptions> options,
    ILogger<BlocklistFeedRefreshService> logger)
  {
    _loader = loader;
    _options = options.Value;
    _logger = logger;
    _httpClient = new HttpClient()
    {
      Timeout = TimeSpan.FromSeconds(30),
    };
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var urls = _options.BlocklistFeedUrls
      .Where(url => !string.IsNullOrWhiteSpace(url))
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToArray();

    if (urls.Length == 0)
      return;

    _logger.LogInformation(
      "Blocklist feed refresh started for {Count} feed(s), interval {Interval}",
      urls.Length,
      _options.BlocklistFeedRefreshInterval);

    while (!stoppingToken.IsCancellationRequested)
    {
      foreach (var url in urls)
      {
        try
        {
          var content = await _httpClient.GetStringAsync(url, stoppingToken);
          _lastFeedLines[url] = content.Split('\n');
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
          return;
        }
        catch (Exception exception)
        {
          _logger.LogWarning(exception, "Blocklist feed download failed for {Url}, last content kept", url);
        }
      }

      try
      {
        var count = _loader.ReloadFromFeeds(_lastFeedLines.Values);

        if (count > 0)
          _logger.LogInformation("Blocklist feeds refreshed: {Count} entries from {Feeds} feed(s)", count, _lastFeedLines.Count);
        else
          _logger.LogWarning("Blocklist feeds returned no entries, previous snapshot kept");
      }
      catch (Exception exception)
      {
        _logger.LogWarning(exception, "Blocklist feed refresh failed, previous snapshot kept");
      }

      try
      {
        await Task.Delay(_options.BlocklistFeedRefreshInterval, stoppingToken);
      }
      catch (TaskCanceledException)
      {
        break;
      }
    }
  }

  public override void Dispose()
  {
    _httpClient.Dispose();
    base.Dispose();
  }
}
