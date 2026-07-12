using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LakerProxy.ProxyApp.Security;

public static class MaliciousIpFilterExtensions
{
  public static IServiceCollection AddMaliciousIpFilter(
    this IServiceCollection services,
    Action<MaliciousIpFilterOptions>? configure = null)
  {
    services.AddOptions<MaliciousIpFilterOptions>();

    if (configure is not null)
      services.Configure(configure);

    services.AddSingleton<MaliciousIpBlocklist>(sp =>
    {
      var options = sp.GetRequiredService<IOptions<MaliciousIpFilterOptions>>().Value;

      var blocklist = new MaliciousIpBlocklist();

      if (options.AllowList.Count > 0)
      {
        var builder = new IpBlocklistBuilder();
        foreach (var entry in options.AllowList)
          builder.Add(entry); // throws on invalid entries, a broken allow list must fail at startup
        blocklist.ReplaceAllowSnapshot(builder.Build());
      }

      return blocklist;
    });
    services.AddSingleton<BlocklistFeedLoader>();
    services.AddHostedService<MaliciousIpRuntimeCleanupService>();
    services.AddHostedService<BlocklistFeedRefreshService>();

    return services;
  }

  public static IApplicationBuilder UseMaliciousIpFilter(
    this IApplicationBuilder app)
  {
    return app.UseMiddleware<MaliciousIpFilterMiddleware>();
  }
}
