using LakerProxy.ProxyApp.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LakerProxy.ProxyApp
{
  public class SwaggerSetObsoleteFilter : IOperationFilter
  {
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
      if (operation.OperationId?.EndsWith(RouteNames.Obsolete) == true)
        operation.Deprecated = true;
    }
  }
}
