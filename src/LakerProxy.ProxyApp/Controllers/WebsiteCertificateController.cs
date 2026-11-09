using System;
using System.Threading.Tasks;
using Asp.Versioning;
using Lakerfield.Runtime;
using LakerProxy.ProxyApp.ApiExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ApiEntity = LakerProxy.WebApi.Models.Certificate;
using ApiError = LakerProxy.WebApi.Models.ApiError;

namespace LakerProxy.ProxyApp.Controllers
{
  //[Authorize]
  [ApiController]
  [ApiVersion("1")]
  [Route("api/v1/websitecertificate")]
  public class WebsiteCertificateController : AWebApiControllerBase
  {
    public WebsiteCertificateController(AppServices services) : base(services)
    {
    }

    // GET: api/websitecertificate/5
    [HttpGet("{id:shortguid}", Name = RouteNames.WebsiteCertificateGetById)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCertificateById(string id)
    {
      var (success, user, website, error) = await GetWebsite(id, ApiUserRights.RoleFullAccess);
      if (!success)
        return error;
      try
      {
        return Ok(await website.ToWebCertificate(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

  }
}
