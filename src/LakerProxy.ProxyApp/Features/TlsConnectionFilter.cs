using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;

namespace LakerProxy.ProxyApp.Features
{

  public class TlsConnectionFilterContext
  {
    public WebsiteKind? OverrideWebsiteKind { get; set; }
  }

  public static class TlsHandshakeMiddlewareExtensions
  {
    public static readonly Type TlsConnectionFilterContextKey = typeof(TlsConnectionFilterContext);

    public static TlsConnectionFilterContext? GetTlsConnectionFilter(this ConnectionContext context)
    {
      if (!context.Items.TryGetValue(TlsConnectionFilterContextKey, out var filterContext))
        return null;

      return filterContext as TlsConnectionFilterContext;
    }

    public static void SetTlsConnectionFilter(this ConnectionContext context, TlsConnectionFilterContext filterContext)
    {
      context.Items[TlsConnectionFilterContextKey] = filterContext;
    }

    public static TlsConnectionFilterContext? GetTlsConnectionFilter(this HttpContext context)
    {
      var connectionItemsFeature = context.Features.Get<IConnectionItemsFeature>();
      if (connectionItemsFeature == null)
        return null;

      if (!connectionItemsFeature.Items.TryGetValue(TlsConnectionFilterContextKey, out var filterContext))
        return null;

      return filterContext as TlsConnectionFilterContext;
    }


  }
}
