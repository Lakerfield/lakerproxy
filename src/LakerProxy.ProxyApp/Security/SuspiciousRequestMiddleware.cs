using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace LakerProxy.ProxyApp.Security;

public sealed class SuspiciousRequestMiddleware
{
  private readonly RequestDelegate _next;
  private readonly MaliciousIpBlocklist _blocklist;

  public SuspiciousRequestMiddleware(
    RequestDelegate next,
    MaliciousIpBlocklist blocklist)
  {
    _next = next;
    _blocklist = blocklist;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    if (LooksMalicious(context))
    {
      var ip = context.Connection.RemoteIpAddress;

      if (ip is not null)
      {
        _blocklist.Block(ip, TimeSpan.FromMinutes(30));
      }

      context.Response.StatusCode = StatusCodes.Status403Forbidden;
      return;
    }

    await _next(context);
  }

  private static bool LooksMalicious(HttpContext context)
  {
    // Eigen detectielogica
    return false;
  }
}
