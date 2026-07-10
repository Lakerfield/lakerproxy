extern alias IPNetwork2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using Lakerfield;
using Lakerfield.Runtime;
using LakerProxy.ProxyApp.ApiExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Entity = LakerProxy.Models.Website;
using ApiEntity = LakerProxy.WebApi.Models.Website;
using ApiError = LakerProxy.WebApi.Models.ApiError;

namespace LakerProxy.ProxyApp.Controllers
{
  //[Authorize]
  [ApiController]
  [ApiVersion("1")]
  [Route("api/v1/website")]
  public class WebsiteController : AWebApiControllerBase
  {
    public WebsiteController(AppServices services) : base(services)
    {
    }


    // GET: api/website
    [HttpGet("", Name = RouteNames.WebsiteGet)]
    [ProducesResponseType(typeof(IEnumerable<ApiEntity>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get()
    {
      var (success, user, error) = await GetUser(ApiUserRights.RoleFullAccess);
      if (!success)
        return error;
      try
      {
        var websites = await Services.Website().GetAll();

        return Ok(websites.SelectAsync(e => e.ToWeb(Services)));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // GET: api/website/5
    [HttpGet("{id:shortguid}", Name = RouteNames.WebsiteGetById)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(string id)
    {
      var (success, user, website, error) = await GetWebsite(id, ApiUserRights.RoleFullAccess);
      if (!success)
        return error;
      try
      { 
        return Ok(await website.ToWeb(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // POST: api/website
    [HttpPost("", Name = RouteNames.WebsitePost)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    //[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Post([FromBody] ApiEntity value)
    {
      var (success, user, error) = await GetUser(ApiUserRights.RoleFullAccess);
      if (!success)
        return error;
      try
      {
        var website = new Entity()
        {
          Hostname = value.Hostname,
          Fallback = value.Fallback,
          ForceRenew = value.ForceRenew,
          Scope = (CertificateScope)value.Scope,
          Kind = (WebsiteKind)value.Kind,
          Destination = value.Destination,
          DestinationProtocol = (DestinationProtocol)value.DestinationProtocol,
          Servers = await ConvertServerNamesToIds(value.Servers),
          Filter = (FilterKind)value.Filter,
          FilterAcl = TestFilterAcl(value.FilterAcl).ToList(),
          FilterDestination = value.FilterDestination,
          MaxRequestBodySize = WebsiteConvert.ConvertToMaxRequestBodySize(value.MaxRequestBodySize),
          Comments = value.Comments,
          //CreatedBy =
          //CreatedAt = 
        };

        website.AcmeAccount = await ConvertAcmeAccountCodeToId(value.AcmeAccount);

        await Services.Website().Save(website, Context.Empty);

        await WriteAuditLog(user, $"Website {website.Hostname} ({website.Id}) created");

        return CreatedAtRoute(RouteNames.WebsiteGetById, new { id = website.Id }, await website.ToWeb(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    private IEnumerable<string> TestFilterAcl(IEnumerable<string> acls)
    {
      foreach (var acl in acls)
      {
        if (!IPNetwork2::System.Net.IPNetwork2.TryParse(acl, out var network))
          throw new OperationAbortedException($"Cannot parse FilterAcl {acl}");
        yield return acl;
      }
    }

    // POST: api/website/5/websitedata
    [HttpPost("{id:shortguid}/websitedata", Name = RouteNames.WebsiteUploadWebsiteData)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadWebsiteData(string id, IFormFile file)
    {
      var (success, user, website, error) = await GetWebsite(id, ApiUserRights.RoleFullAccess);
      if (!success)
        return error;
      try
      {
        if (file == null || file.Length == 0)
          return GenerateErrorNotFound("file", "Missing form file 'file'");

        await Yarp.WebsiteDataUpload.Save(Services, user, website, file);

        await WriteAuditLog(user, $"Website zip '{file.FileName}' ({file.Length} bytes) uploaded to {website.Hostname} via management api");

        return Ok(await website.ToWeb(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // DELETE: api/website/5/websitedata
    [HttpDelete("{id:shortguid}/websitedata", Name = RouteNames.WebsiteDeleteWebsiteData)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteWebsiteData(string id)
    {
      var (success, user, website, error) = await GetWebsite(id, ApiUserRights.RoleFullAccess);
      if (!success)
        return error;
      try
      {
        await Yarp.WebsiteDataUpload.Delete(Services, user, website);

        await WriteAuditLog(user, $"Website zip removed from {website.Hostname}");

        return Ok(await website.ToWeb(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // PUT: api/website/5
    [HttpPut("{id}", Name = RouteNames.WebsitePutById)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Put(string id, [FromBody] ApiEntity value)
    {
      var (success, user, website, error) = await GetWebsite(id, ApiUserRights.RoleFullAccess);
      if (!success)
        return error;
      try
      {
        var dbContext = Context.Empty;

        website.Hostname = value.Hostname;
        website.Fallback = value.Fallback;
        website.ForceRenew = value.ForceRenew;
        website.Scope = (CertificateScope)value.Scope;
        website.Kind = (WebsiteKind) value.Kind;
        website.Destination = value.Destination;
        website.DestinationProtocol = (DestinationProtocol)value.DestinationProtocol;
        website.Servers = await ConvertServerNamesToIds(value.Servers);
        website.Filter = (FilterKind)value.Filter;
        website.FilterAcl = TestFilterAcl(value.FilterAcl).ToList();
        website.FilterDestination = value.FilterDestination;
        website.AcmeAccount = await ConvertAcmeAccountCodeToId(value.AcmeAccount);
        website.MaxRequestBodySize = WebsiteConvert.ConvertToMaxRequestBodySize(value.MaxRequestBodySize);
        website.Comments = value.Comments;

        await Services.Website().Save(website, dbContext);

        await WriteAuditLog(user, $"Website {website.Hostname} ({website.Id}) updated");

        return Ok(await website.ToWeb(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // DELETE: api/website/5
    [HttpDelete("{id}", Name = RouteNames.WebsiteDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(string id)
    {
      var (success, user, website, error) = await GetWebsite(id, ApiUserRights.RoleAdministrator);
      if (!success)
        return error;
      try
      {
        if (website.Kind == WebsiteKind.Offline)
        {
          await Services.Website().Delete(website, Context.Empty).ConfigureAwait(false);

          await WriteAuditLog(user, $"Website {website.Hostname} ({website.Id}) deleted");
        }
        else
        {
          website.Kind = WebsiteKind.Offline;

          await Services.Website().Save(website, Context.Empty);

          await WriteAuditLog(user, $"Website {website.Hostname} ({website.Id}) set offline");
        }


        return NoContent();
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }



  }
}
