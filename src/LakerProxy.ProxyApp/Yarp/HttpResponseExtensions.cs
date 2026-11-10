using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace LakerProxy.ProxyApp.Yarp
{
  public static class HttpResponseExtensions
  {
    /// <summary>
    /// Completes the response with <paramref name="text"/> as its entire body.
    /// Content-Length is set to the exact byte count, so Kestrel sends headers and
    /// body in one go without chunked transfer encoding. This must be the only body
    /// write: anything written afterwards exceeds the declared length and Kestrel
    /// throws. Use Response.WriteAsync for streamed or incremental output.
    /// </summary>
    public static Task CompleteWithTextAsync(this HttpResponse response, string text, string contentType = "text/plain; charset=utf-8", CancellationToken cancellationToken = default)
    {
      var bytes = Encoding.UTF8.GetBytes(text);
      if (contentType != null)
        response.ContentType = contentType;
      response.ContentLength = bytes.Length;
      return response.Body.WriteAsync(bytes, cancellationToken).AsTask();
    }
  }
}
