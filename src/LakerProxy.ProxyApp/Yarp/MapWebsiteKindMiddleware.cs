using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LakerProxy.ProxyApp.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LakerProxy.ProxyApp.Yarp
{
  public static class MapWebsiteKindMiddlewareExtensions
  {
    public static IApplicationBuilder MapWebsiteKind(this IApplicationBuilder builder, params (WebsiteKind kind, RequestDelegate? branch)[] options)
    {
      return builder
        .UseMiddleware<MapWebsiteKindMiddleware>(options);
    }
  }

    /// <summary>
    /// Represents a middleware that runs a sub-request pipeline when a given predicate is matched.
    /// </summary>
    public class MapWebsiteKindMiddleware
  {
    private readonly RequestDelegate _next;
    private readonly Dictionary<WebsiteKind, RequestDelegate> _options;

    public MapWebsiteKindMiddleware(RequestDelegate next, (WebsiteKind kind, RequestDelegate? branch)[] options)
    {
      if (next == null)
        throw new ArgumentNullException(nameof(next));

      if (options == null)
        throw new ArgumentNullException(nameof(options));

      _next = next;
      _options = new Dictionary<WebsiteKind, RequestDelegate>();
      foreach (var option in options)
      {
        if (option.branch == null)
          continue;
        _options.Add(option.kind, option.branch);
      }
    }

    /// <summary>
    /// Executes the middleware.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>A task that represents the execution of this middleware.</returns>
    public async Task Invoke(HttpContext context)
    {
      if (context == null)
      {
        throw new ArgumentNullException(nameof(context));
      }

      var kind = WebsiteKind.Offline;
      var filter = context.GetTlsConnectionFilter();
      if (filter != null && filter.OverrideWebsiteKind.HasValue)
      {
        kind = filter.OverrideWebsiteKind.Value;
      }
      else
      {
        var website = context.GetWebsite();
        if (website != null)
          kind = website.Website.Kind;
      }

      if (_options.TryGetValue(kind, out var branch))
      {
        await branch(context);
      }
      else
      {
        await _next(context);
      }
    }
  }
}
