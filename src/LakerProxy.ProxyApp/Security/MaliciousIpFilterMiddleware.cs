using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LakerProxy.ProxyApp.Security;

public sealed class MaliciousIpFilterMiddleware
{
  private readonly RequestDelegate _next;
  private readonly MaliciousIpBlocklist _blocklist;
  private readonly MaliciousIpFilterOptions _options;

  public MaliciousIpFilterMiddleware(
    RequestDelegate next,
    MaliciousIpBlocklist blocklist,
    IOptions<MaliciousIpFilterOptions> options)
  {
    _next = next;
    _blocklist = blocklist;
    _options = options.Value;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    var remoteIpAddress = context.Connection.RemoteIpAddress;

    if (_blocklist.IsBlocked(remoteIpAddress))
    {
      context.Response.StatusCode = _options.BlockedStatusCode;
      context.Response.ContentLength = 0;
      return;
    }

    await _next(context);
  }
}

public sealed class MaliciousIpFilterOptions
{
  public int BlockedStatusCode { get; set; } =
    StatusCodes.Status403Forbidden;

  public TimeSpan RuntimeCleanupInterval { get; set; } =
    TimeSpan.FromMinutes(1);
}
