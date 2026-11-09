using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace LakerProxy.ProxyApp.Middleware
{
  /// <summary>
  /// Fix to disable websockets over non http/1.1 connections
  /// Implementation of comment https://github.com/microsoft/reverse-proxy/issues/1375#issuecomment-1312799130
  /// </summary>
  public class RejectWebsocketOverHttp2WhileUnsupportedMiddleware
  {
    private readonly RequestDelegate _next;


    public RejectWebsocketOverHttp2WhileUnsupportedMiddleware(RequestDelegate next)
    {
      _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
      if (context.Request.Method == HttpMethods.Connect && context.Request.Protocol != HttpProtocol.Http11)
      {
        var resetFeature = context.Features.Get<IHttpResetFeature>();
        if (resetFeature != null)
        {
          //https://www.rfc-editor.org/rfc/rfc7540#page-51
          //HTTP_1_1_REQUIRED (0xd):  The endpoint requires that HTTP/1.1 be used instead of HTTP/2.
          resetFeature.Reset(errorCode: 0xd);
          return;
        }
      }

      await _next.Invoke(context);
    }
  }

  public static class RejectWebsocketOverHttp2WhileUnsupportedMiddlewareExtensions
  {
    public static IApplicationBuilder UseRejectWebsocketOverHttp2WhileUnsupported(this IApplicationBuilder builder)
    {
      return builder.UseMiddleware<RejectWebsocketOverHttp2WhileUnsupportedMiddleware>();
    }
  }
}
