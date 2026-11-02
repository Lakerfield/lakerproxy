using System.Threading.Tasks;
using LakerProxy.ProxyApp.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LakerProxy.ProxyApp.Yarp
{
  /// <summary>
  /// Default behaviour of a zip hosted static site: an index.html per directory and a 404.html fallback
  /// </summary>
  public static class StaticSiteExtensions
  {
    private const string DefaultDocument = "index.html";
    /// <summary>Surge.sh convention: served with status 200 for every unknown route, for SPAs with client-side routing</summary>
    private const string SpaFallbackDocument = "/200.html";
    /// <summary>GitHub Pages / Netlify convention: served with status 404 for unknown routes</summary>
    private const string NotFoundDocument = "/404.html";

    /// <summary>
    /// Rewrites "/" and "/dir/" to the index.html in that directory. A path without trailing slash like "/contact"
    /// is rewritten to "/contact/index.html" when no file "/contact" exists but that index does.
    /// </summary>
    public static IApplicationBuilder UseStaticSiteDefaultDocument(this IApplicationBuilder app, SuperAwesomeFileProvider files)
    {
      return app.Use(async (context, next) =>
      {
        var path = context.Request.Path.Value ?? "/";

        if (path.EndsWith('/'))
          context.Request.Path = path + DefaultDocument;
        else if (!await files.FileExists(path) && await files.FileExists(path + "/" + DefaultDocument))
          context.Request.Path = path + "/" + DefaultDocument;

        await next(context);
      });
    }

    /// <summary>
    /// Terminal step for an unknown route, in order: /200.html with status 200 (SPA fallback),
    /// /404.html with status 404, otherwise an empty 404.
    /// </summary>
    public static void RunStaticSiteNotFound(this IApplicationBuilder app, SuperAwesomeFileProvider files)
    {
      app.Run(async context =>
      {
        if (await files.FileExists(SpaFallbackDocument))
        {
          await SendDocument(context, files, SpaFallbackDocument, StatusCodes.Status200OK);
          return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;

        if (await files.FileExists(NotFoundDocument))
          await SendDocument(context, files, NotFoundDocument, StatusCodes.Status404NotFound);
      });
    }

    private static async Task SendDocument(HttpContext context, SuperAwesomeFileProvider files, string document, int statusCode)
    {
      // GetFileInfo picks the brotli/gzip variant and sets Content-Encoding and Cache-Control
      var file = files.GetFileInfo(document);
      if (!file.Exists)
        return;

      context.Response.StatusCode = statusCode;
      context.Response.ContentType = "text/html; charset=utf-8";
      context.Response.ContentLength = file.Length;
      await context.Response.SendFileAsync(file, context.RequestAborted);
    }
  }
}
