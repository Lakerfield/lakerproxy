using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LakerProxy.ProxyApp.Security;

internal sealed class MaliciousIpRuntimeCleanupService : BackgroundService
{
  private readonly MaliciousIpBlocklist _blocklist;
  private readonly MaliciousIpFilterOptions _options;

  public MaliciousIpRuntimeCleanupService(
    MaliciousIpBlocklist blocklist,
    IOptions<MaliciousIpFilterOptions> options)
  {
    _blocklist = blocklist;
    _options = options.Value;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(_options.RuntimeCleanupInterval);

    try
    {
      while (await timer.WaitForNextTickAsync(stoppingToken))
      {
        _blocklist.RemoveExpiredRuntimeBlocks();
      }
    }
    catch (OperationCanceledException)
    {
    }
  }
}
