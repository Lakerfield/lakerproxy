using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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

    services.AddSingleton<MaliciousIpBlocklist>();
    services.AddHostedService<MaliciousIpRuntimeCleanupService>();

    return services;
  }

  public static IApplicationBuilder UseMaliciousIpFilter(
    this IApplicationBuilder app)
  {
    return app.UseMiddleware<MaliciousIpFilterMiddleware>();
  }
}
