using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Runtime;
using LakerProxy.Models;
using LakerProxy.ProxyApp.ApiExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ApiError = LakerProxy.WebApi.Models.ApiError;

namespace LakerProxy.ProxyApp.Controllers
{
  public abstract class AWebApiControllerBase : ControllerBase
  {
    public const string CompanyHeader = "x-floriapp-company-id";

    protected AppServices Services { get; }

    protected AWebApiControllerBase(AppServices services)
    {
      Services = services;
    }

    //https://docs.microsoft.com/en-us/aspnet/web-api/overview/error-handling/exception-handling
    protected async Task<(bool success, ApiUser? apiUser, IActionResult? failedResult)> GetUser(ApiUserRights requiredRights)
    {
      var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
      if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("bearer ", StringComparison.OrdinalIgnoreCase))
        return (false, default, GenerateErrorUnauthorized("no-authorization-header", "Missing authentication header or not starting with Bearer"));

      var key = authHeader.Substring(7);
      if (key.Length < 20)
        return (false, default, GenerateErrorUnauthorized("invalid-token-to-short", "Invalid token to short"));

      var user = await Services.ApiUser().Login(key);
      if (user == null)
        return (false, default, GenerateErrorUnauthorized("invalid-token", "Invalid token"));

      if (!user.Rights.HasFlag(requiredRights))
        return (false, default, GenerateErrorUnauthorized("invalid-rights", "Invalid rights"));
      
      return (true, user, null);
    }

    protected async Task<(bool success, ApiUser user, Website website, IActionResult failedResult)> GetWebsite(string idRaw, ApiUserRights requiredRights)
    {
      var (success, user, error) = await GetUser(requiredRights);
      if (!success)
        return (false, user, null, error);


      Website website = default;
      if (ShortGuid.TryParse(idRaw, out var id))
        website = await Services.Website().GetById(id);

      if (website == null)
        error = GenerateErrorNotFound("website", "Website not found");

      return (error == null, user, website, error);
    }


    protected IActionResult GenerateErrorUnauthorized(string key, string title)
    {
      var error = new ApiError(
        HttpContext.Request.Path,
        $"/error/unauthorized/{key}",
        title,
        StatusCodes.Status401Unauthorized);

      return new ApiErrorActionResult(error);
    }

    protected IActionResult GenerateErrorForbidden(string key, string title)
    {
      var error = new ApiError(
        HttpContext.Request.Path,
        $"/error/forbidden/{key}",
        title,
        StatusCodes.Status403Forbidden);

      return new ApiErrorActionResult(error);
    }

    protected IActionResult GenerateErrorNotFound(string key, string title)
    {
      var error = new ApiError(
        HttpContext.Request.Path,
        $"/error/not-found/{key}",
        title,
        StatusCodes.Status404NotFound);

      return new ApiErrorActionResult(error);
    }

    protected IActionResult GenerateConflict(string key, string title)
    {
      var error = new ApiError(
        HttpContext.Request.Path,
        $"/error/conflict/{key}",
        title,
        StatusCodes.Status409Conflict);

      return new ApiErrorActionResult(error);
    }





    protected ValueTask<List<ShortGuid>> ConvertServerNamesToIds(IEnumerable<string> names)
    {
      return names
        .SelectAsync(async name => await ConvertServerNameToId(name))
        .Where(i => !ShortGuid.IsEmpty(i))
        .ToListAsync();
    }

    protected async Task<ShortGuid> ConvertServerNameToId(string name)
    {
      return (await Services.Server().Find(name))?.Id ?? ShortGuid.Empty;
    }

    protected async ValueTask<ShortGuid> ConvertAcmeAccountCodeToId(string code)
    {
      return (await Services.AcmeAccount().Find(code))?.Id ?? ShortGuid.Empty;
    }

    protected async Task<ShortGuid> ConvertWebsiteHostnameToId(string hostname)
    {
      if (string.IsNullOrWhiteSpace(hostname))
        return ShortGuid.Empty;
      return (await Services.Website().FindByHostname(hostname.ToLower().Trim()))?.Id ?? ShortGuid.Empty;
    }


  }
}
