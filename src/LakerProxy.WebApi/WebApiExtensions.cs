using System.Net.Http;
using System.Text.Json;

namespace LakerProxy.WebApi
{
  public static class WebApiExtensions
  {
    public static StringContent AsJsonStringContent<T>(this T entity, JsonSerializerOptions options)
    {
      return new StringContent(JsonSerializer.Serialize(entity, options), System.Text.Encoding.UTF8, "application/json");
    }


  }
}
