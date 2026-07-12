using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Runtime;
using LakerProxy.ProxyApp.ApiExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Entity = LakerProxy.Models.ApiUser;
using ApiEntity = LakerProxy.WebApi.Models.ApiUser;
using ApiError = LakerProxy.WebApi.Models.ApiError;

namespace LakerProxy.ProxyApp.Controllers
{
  //[Authorize]
#if DEBUG
  [Asp.Versioning.ApiVersion("1")]
#endif
  [ApiController]
  [Route("api/v1/apiuser")]
  public class ApiUserController : AWebApiControllerBase
  {
    public ApiUserController(AppServices services) : base(services)
    {
    }


    // GET: api/apiuser
    [HttpGet("", Name = RouteNames.ApiUserGet)]
    [ProducesResponseType(typeof(IEnumerable<ApiEntity>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get()
    {
      var (success, user, error) = await GetUser(ApiUserRights.RoleAdministrator);
      if (!success)
        return error;
      try
      {
        var websites = await Services.ApiUser().GetAll();

        return Ok(websites.SelectAsync(e => ApiUserConvert.ToWeb(e, Services)));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // GET: api/apiuser/5
    [HttpGet("{id:shortguid}", Name = RouteNames.ApiUserGetById)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(string id)
    {
      var (success, user, apiUser, error) = await GetApiUser(id, ApiUserRights.RoleAdministrator);
      if (!success)
        return error;
      try
      { 
        return Ok(await apiUser.ToWeb(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // POST: api/apiuser
    [HttpPost("", Name = RouteNames.ApiUserPost)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    //[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Post([FromBody] ApiEntity value)
    {
      var (success, user, error) = await GetUser(ApiUserRights.RoleAdministrator);
      if (!success)
        return error;
      try
      {
        var dbContext = Context.Empty.Add(user);

        var apiUser = new Entity()
        {
          Name = value.Name,
          Hostname = value.Hostname,
          Rights = (ApiUserRights)value.Rights,
          PrimaryServer = await ConvertServerNameToId(value.Server),
          Website = await ConvertWebsiteHostnameToId(value.Website),
          Comments = value.Comments,
          //CreatedBy =
          //CreatedAt =
        };

        apiUser.AcmeAccount = await ConvertAcmeAccountCodeToId(value.AcmeAccount);

        if (value.ApiKey.IsNotNullOrWhiteSpace())
          apiUser.ApiKey = value.ApiKey;

        await Services.ApiUser().Save(apiUser, dbContext);

        await WriteAuditLog(user, $"ApiUser {apiUser.Name} ({apiUser.Id}) created with rights {apiUser.Rights}");

        return CreatedAtRoute(RouteNames.ApiUserGetById, new { id = apiUser.Id }, await apiUser.ToWeb(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // PUT: api/apiuser/5
    [HttpPut("{id}", Name = RouteNames.ApiUserPutById)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Put(string id, [FromBody] ApiEntity value)
    {
      var (success, user, apiUser, error) = await GetApiUser(id, ApiUserRights.RoleAdministrator);
      if (!success)
        return error;
      try
      {
        var dbContext = Context.Empty.Add(user);

        apiUser.Name = value.Name;
        apiUser.Hostname = value.Hostname;
        apiUser.AcmeAccount = await ConvertAcmeAccountCodeToId(value.AcmeAccount);
        if (value.ApiKey.IsNotNullOrWhiteSpace())
          apiUser.ApiKey = value.ApiKey;
        apiUser.PrimaryServer = await ConvertServerNameToId(value.Server);
        apiUser.Website = await ConvertWebsiteHostnameToId(value.Website);
        apiUser.Rights = (ApiUserRights) value.Rights;
        apiUser.Comments = value.Comments;

        await Services.ApiUser().Save(apiUser, dbContext);

        await WriteAuditLog(user, $"ApiUser {apiUser.Name} ({apiUser.Id}) updated with rights {apiUser.Rights}");

        return Ok(await apiUser.ToWeb(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // DELETE: api/apiuser/5
    [HttpDelete("{id}", Name = RouteNames.ApiUserDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(string id)
    {
      var (success, user, apiUser, error) = await GetApiUser(id, ApiUserRights.RoleAdministrator);
      if (!success)
        return error;
      try
      {
        var dbContext = Context.Empty.Add(user);

        if (user.Id != apiUser.Id)
        {
          await Services.ApiUser().Delete(apiUser, dbContext).ConfigureAwait(false);

          await WriteAuditLog(user, $"ApiUser {apiUser.Name} ({apiUser.Id}) deleted");
        }

        return NoContent();
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }



    protected async Task<(bool success, Entity? user, Entity? apiUser, IActionResult? failedResult)> GetApiUser(string idRaw, ApiUserRights requiredRights)
    {
      var (success, user, error) = await GetUser(requiredRights);
      if (!success)
        return (false, user, null, error);


      Entity? website = default;
      if (ShortGuid.TryParse(idRaw, out var id))
        website = await Services.ApiUser().GetById(id);

      if (website == null)
        error = GenerateErrorNotFound("apiuser", "ApiUser not found");

      return (error == null, user, website, error);
    }

  }
}
