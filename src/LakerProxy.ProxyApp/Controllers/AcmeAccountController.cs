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
using Entity = LakerProxy.Models.AcmeAccount;
using ApiEntity = LakerProxy.WebApi.Models.AcmeAccount;
using ApiError = LakerProxy.WebApi.Models.ApiError;

namespace LakerProxy.ProxyApp.Controllers
{
  [ApiController]
  [ApiVersion("1")]
  [Route("api/v1/acmeaccount")]
  public class AcmeAccountController : AWebApiControllerBase
  {
    public AcmeAccountController(AppServices services) : base(services)
    {
    }


    // GET: api/v1/acmeaccount
    [HttpGet("", Name = RouteNames.AcmeAccountGet)]
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
        var accounts = await Services.AcmeAccount().GetAll();

        return Ok(accounts.Select(e => e.ToWeb()));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // GET: api/v1/acmeaccount/5
    [HttpGet("{id:shortguid}", Name = RouteNames.AcmeAccountGetById)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(string id)
    {
      var (success, user, account, error) = await GetAcmeAccount(id, ApiUserRights.RoleAdministrator);
      if (!success)
        return error;
      try
      {
        return Ok(account.ToWeb());
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // POST: api/v1/acmeaccount
    [HttpPost("", Name = RouteNames.AcmeAccountPost)]
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

        var account = new Entity()
        {
          Code = value.Code,
          Name = value.Name,
          Type = (AcmeAccountTypes)value.Type,
          AcmeDiscoveryUrl = value.AcmeDiscoveryUrl,
          Default = value.Default,
          Mail = value.Mail,
          UseEab = value.UseEab,
          EabKeyIdentifier = value.EabKeyIdentifier,
          EabKeyAlgorithm = value.EabKeyAlgorithm,
          PreferredRootCertificate = value.PreferredRootCertificate,
          CertificateCountryName = value.CertificateCountryName,
          CertificateState = value.CertificateState,
          CertificateLocality = value.CertificateLocality,
          CertificateOrganization = value.CertificateOrganization,
          CertificateOrganizationUnit = value.CertificateOrganizationUnit,
        };

        if (value.EabKey.IsNotNullOrWhiteSpace())
          account.EabKey = value.EabKey;

        await Services.AcmeAccount().Save(account, dbContext);

        return CreatedAtRoute(RouteNames.AcmeAccountGetById, new { id = account.Id }, account.ToWeb());
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }

    // PUT: api/v1/acmeaccount/5
    [HttpPut("{id}", Name = RouteNames.AcmeAccountPutById)]
    [ProducesResponseType(typeof(ApiEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Put(string id, [FromBody] ApiEntity value)
    {
      var (success, user, account, error) = await GetAcmeAccount(id, ApiUserRights.RoleAdministrator);
      if (!success)
        return error;
      try
      {
        var dbContext = Context.Empty.Add(user);

        account.Code = value.Code;
        account.Name = value.Name;
        account.Type = (AcmeAccountTypes)value.Type;
        account.AcmeDiscoveryUrl = value.AcmeDiscoveryUrl;
        account.Default = value.Default;
        account.Mail = value.Mail;
        account.UseEab = value.UseEab;
        account.EabKeyIdentifier = value.EabKeyIdentifier;
        if (value.EabKey.IsNotNullOrWhiteSpace())
          account.EabKey = value.EabKey;
        account.EabKeyAlgorithm = value.EabKeyAlgorithm;
        account.PreferredRootCertificate = value.PreferredRootCertificate;
        account.CertificateCountryName = value.CertificateCountryName;
        account.CertificateState = value.CertificateState;
        account.CertificateLocality = value.CertificateLocality;
        account.CertificateOrganization = value.CertificateOrganization;
        account.CertificateOrganizationUnit = value.CertificateOrganizationUnit;

        await Services.AcmeAccount().Save(account, dbContext);

        return Ok(account.ToWeb());
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }



    protected async Task<(bool success, Models.ApiUser user, Entity account, IActionResult failedResult)> GetAcmeAccount(string idRaw, ApiUserRights requiredRights)
    {
      var (success, user, error) = await GetUser(requiredRights);
      if (!success)
        return (false, user, null, error);

      Entity account = default;
      if (ShortGuid.TryParse(idRaw, out var id))
        account = await Services.AcmeAccount().GetById(id);

      if (account == null)
        error = GenerateErrorNotFound("acmeaccount", "AcmeAccount not found");

      return (error == null, user, account, error);
    }

  }
}
