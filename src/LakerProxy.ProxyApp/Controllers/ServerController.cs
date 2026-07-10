using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asp.Versioning;
using Lakerfield;
using Lakerfield.Runtime;
using LakerProxy.ProxyApp.ApiExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Entity = LakerProxy.Models.Server;
using ApiEntity = LakerProxy.WebApi.Models.Server;
using ApiError = LakerProxy.WebApi.Models.ApiError;

namespace LakerProxy.ProxyApp.Controllers
{
  [ApiController]
  [ApiVersion("1")]
  [Route("api/v1/server")]
  public class ServerController : AWebApiControllerBase
  {
    public ServerController(AppServices services) : base(services)
    {
    }


    // GET: api/v1/server
    [HttpGet("", Name = RouteNames.ServerGet)]
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
        var servers = await Services.Server().GetAll();

        return Ok(servers.SelectAsync(e => e.ToWeb(Services)));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // GET: api/v1/server/5
    [HttpGet("{id:shortguid}", Name = RouteNames.ServerGetById)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(string id)
    {
      var (success, user, server, error) = await GetServer(id, ApiUserRights.RoleAdministrator);
      if (!success)
        return error;
      try
      {
        return Ok(await server.ToWeb(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // POST: api/v1/server
    [HttpPost("", Name = RouteNames.ServerPost)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Post([FromBody] ApiEntity value)
    {
      var (success, user, error) = await GetUser(ApiUserRights.RoleAdministrator);
      if (!success)
        return error;
      try
      {
        var dbContext = Context.Empty.Add(user);

        var server = new Entity()
        {
          Name = value.Name,
          Hostname = value.Hostname,
          AcmeChallengeZone = value.AcmeChallengeZone,
          Services = (ServerServices)value.Services,
          AcmeAccount = await ConvertAcmeAccountCodeToId(value.AcmeAccount),
          Comments = value.Comments,
          CreatedAt = DateTime.Now,
        };

        await Services.Server().Save(server, dbContext);

        return CreatedAtRoute(RouteNames.ServerGetById, new { id = server.Id }, await server.ToWeb(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // PUT: api/v1/server/5
    [HttpPut("{id}", Name = RouteNames.ServerPutById)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Put(string id, [FromBody] ApiEntity value)
    {
      var (success, user, server, error) = await GetServer(id, ApiUserRights.RoleAdministrator);
      if (!success)
        return error;
      try
      {
        var dbContext = Context.Empty.Add(user);

        server.Name = value.Name;
        server.Hostname = value.Hostname;
        server.AcmeChallengeZone = value.AcmeChallengeZone;
        server.Services = (ServerServices)value.Services;
        server.AcmeAccount = await ConvertAcmeAccountCodeToId(value.AcmeAccount);
        server.Comments = value.Comments;

        await Services.Server().Save(server, dbContext);

        return Ok(await server.ToWeb(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }



    protected async Task<(bool success, Models.ApiUser user, Entity server, IActionResult failedResult)> GetServer(string idRaw, ApiUserRights requiredRights)
    {
      var (success, user, error) = await GetUser(requiredRights);
      if (!success)
        return (false, user, null, error);

      Entity server = default;
      if (ShortGuid.TryParse(idRaw, out var id))
        server = await Services.Server().GetById(id);

      if (server == null)
        error = GenerateErrorNotFound("server", "Server not found");

      return (error == null, user, server, error);
    }

  }
}
