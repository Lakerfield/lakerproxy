using System;
using System.Threading.Tasks;
using LakerProxy.ProxyApp.ApiExtensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace LakerProxy.ProxyApp.Yarp
{
  public static class RequestBodySizeMiddlewareExtensions
  {
    public static IApplicationBuilder UseMaxRequestBodySize(this IApplicationBuilder builder)
    {
      return builder
        .UseMiddleware<MaxRequestBodySizeMiddleware>();
    }
  }

  public class MaxRequestBodySizeMiddleware
  {
    private readonly RequestDelegate _next;

    public MaxRequestBodySizeMiddleware(RequestDelegate next)
    {
      if (next == null)
        throw new ArgumentNullException(nameof(next));

      _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
      if (context == null)
        throw new ArgumentNullException(nameof(context));

      var website = context.GetWebsite();
      var maxRequestBodySize = GetMaxRequestBodySize();

      var maxRequestBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
      if (maxRequestBodySizeFeature != null && !maxRequestBodySizeFeature.IsReadOnly)
        maxRequestBodySizeFeature.MaxRequestBodySize = maxRequestBodySize;

      await _next(context);

      long? GetMaxRequestBodySize()
      {
        if (website == null)
          return 0;

        var bytes = website.Website.MaxRequestBodySize;

        if (bytes == WebsiteConvert.UnlimitedValue)
          return null;// unlimited

        if (bytes == WebsiteConvert.MaxValue)
          return Program.MaxRequestBodySize;

        if (bytes <= WebsiteConvert.DefaultValue)
          return Program.DefaultRequestBodySize;

        if (bytes > Program.MaxRequestBodySize)
          return Program.MaxRequestBodySize;

        return bytes;
      }
    }
  }
}
