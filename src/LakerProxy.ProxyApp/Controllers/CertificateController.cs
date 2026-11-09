using System;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using Lakerfield;
using Lakerfield.Runtime;
using LakerProxy.ProxyApp.ApiExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Entity = LakerProxy.Models.Website;
using ApiEntity = LakerProxy.WebApi.Models.Certificate;
using ApiError = LakerProxy.WebApi.Models.ApiError;

namespace LakerProxy.ProxyApp.Controllers
{
  //[Authorize]
  [ApiController]
  [ApiVersion("1")]
  [Route("api/v1/certificate")]
  public class CertificateController : AWebApiControllerBase
  {
    public CertificateController(AppServices services) : base(services)
    {
    }
    
    // GET: api/certificate/5
    [HttpGet("{domain}/{name}", Name = RouteNames.CertificateGetByName)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetByName(string domain, string name)
    {
      var (success, user, error) = await GetUser(ApiUserRights.DownloadCertificate);
      if (!success)
        return error;
      
      try
      { 
        var dbContext = Context.Empty.Add(user);

        if (user.Hostname.Split('.', StringSplitOptions.RemoveEmptyEntries).Length < 2)
          return GenerateErrorNotFound("certificate-domain", "Certificate not allowed without domain in user");

        domain = domain.Trim().Trim('.').ToLower();
        if (!IsDnsDomainAllowed(domain) || domain != user.Hostname)
          return GenerateErrorNotFound("certificate-domain", "Certificate domain not allowed");

        name = name.Trim().ToLower();
        if (!IsDnsHostAllowed(name))
          return GenerateErrorNotFound("certificate-name", "Certificate name not allowed");

        var hostname = $"{name}.{user.Hostname}";

        var website = await Services.Website().FindByHostname(hostname);
        if (website == null)
        {
          website = new Entity()
          {
            Hostname = hostname,
            Kind = WebsiteKind.SecureServer,
            Scope = CertificateScope.SingleDomain,
            AcmeAccount = user.AcmeAccount,
            Comments = "created by request",
          };
          website.Servers.Add(user.PrimaryServer);

          await Services.Website().Save(website, dbContext);
        }

        return Ok(await website.ToWebCertificate(Services));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    private bool IsDnsHostAllowed(string input)
    {
      return input.All(c => c switch
      {
        >= 'a' and <= 'z' => true,
        >= '0' and <= '9' => true,
        '-' => true,
        _ => false
      });
    }

    private bool IsDnsDomainAllowed(string input)
    {
      return input.All(c => c switch
      {
        >= 'a' and <= 'z' => true,
        >= '0' and <= '9' => true,
        '-' => true,
        '.' => true,
        _ => false
      });
    }

  }
}
