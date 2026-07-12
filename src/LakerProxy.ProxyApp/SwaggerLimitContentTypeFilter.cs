#if DEBUG
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LakerProxy.ProxyApp
{
  public class SwaggerLimitContentTypeFilter : IOperationFilter
  {
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
      //operation.RequestBody.Content.Clear();
      //operation.Consumes.Add("application/json");

      foreach (var operationResponse in operation.Responses)
      {
        var contentTypes = operationResponse.Value.Content;
        if (contentTypes.ContainsKey("text/plain"))
          contentTypes.Remove("text/plain");
        if (contentTypes.ContainsKey("text/json"))
          contentTypes.Remove("text/json");
      }
      //operation.Responses.Clear();
      //operation.Responses.
      //operation.Produces.Add("application/json");
    }
  }
}
#endif
