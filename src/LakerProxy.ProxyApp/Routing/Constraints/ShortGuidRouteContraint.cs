using System;
using System.Globalization;
using Lakerfield;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LakerProxy.ProxyApp.Routing.Constraints
{
  public class ShortGuidRouteContraint : IRouteConstraint
  {
    /// <inheritdoc />
    public bool Match(
      HttpContext httpContext,
      IRouter route,
      string routeKey,
      RouteValueDictionary values,
      RouteDirection routeDirection)
    {
      if (routeKey == null)
      {
        throw new ArgumentNullException(nameof(routeKey));
      }

      if (values == null)
      {
        throw new ArgumentNullException(nameof(values));
      }

      if (values.TryGetValue(routeKey, out var value) && value != null)
      {
        if (value is ShortGuid)
        {
          return true;
        }

        var valueString = Convert.ToString(value, CultureInfo.InvariantCulture);

        return ShortGuid.TryParse(valueString, out _);
      }

      return false;
    }
  }
}
