using System;
using System.Threading.Tasks;
using LakerProxy.WebApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace LakerProxy.ProxyApp.ApiExtensions
{
  public class ApiErrorActionResult : IActionResult
  {
    private readonly ApiError _result;

    public ApiErrorActionResult(ApiError result)
    {
      _result = result;
    }

    public ApiErrorActionResult(Exception exception)
    {
      _result = new ApiError(
        "/",
        "/errors/generic",
        "Unexpected exception",
        StatusCodes.Status500InternalServerError,
        $"{exception.Message}");
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
      ObjectResult objectResult;

      if (_result != null)
      {
        objectResult = new ObjectResult(_result)
        {
          StatusCode = _result.Status,
          ContentTypes = new MediaTypeCollection()
          {
            "application/problem+json",
          }
        };
      }
      else
      {
        objectResult = new ObjectResult("null")
        {
          StatusCode = StatusCodes.Status501NotImplemented,
        };
      }

      await objectResult.ExecuteResultAsync(context);
    }
  }

  public static class ApiErrorExtensions
  {
    public static ApiErrorActionResult ToActionResult(this ApiError apiError)
    {
      return new ApiErrorActionResult(apiError);
    }

    public static ApiErrorActionResult ToActionResult(this Exception exception)
    {
      return new ApiErrorActionResult(exception);
    }

  }
}
