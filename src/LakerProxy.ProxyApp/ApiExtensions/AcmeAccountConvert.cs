using Entity = LakerProxy.Models.AcmeAccount;
using WebEntity = LakerProxy.WebApi.Models.AcmeAccount;

namespace LakerProxy.ProxyApp.ApiExtensions
{
  public static class AcmeAccountConvert
  {

    public static WebEntity ToWeb(this Entity entity)
    {
      if (entity == null)
        return null;

      return new WebEntity()
      {
        Id = entity.Id,
        Code = entity.Code ?? "",
        Name = entity.Name ?? "",
        Type = (LakerProxy.WebApi.AcmeAccountTypes)entity.Type,
        AcmeDiscoveryUrl = entity.AcmeDiscoveryUrl ?? "",
        Default = entity.Default,
        Mail = entity.Mail ?? "",
        UseEab = entity.UseEab,
        EabKeyIdentifier = entity.EabKeyIdentifier ?? "",
        EabKey = "",
        EabKeyAlgorithm = entity.EabKeyAlgorithm ?? "",
        PreferredRootCertificate = entity.PreferredRootCertificate ?? "",
        AccountUrl = entity.AccountUrl ?? "",
        CertificateCountryName = entity.CertificateCountryName ?? "",
        CertificateState = entity.CertificateState ?? "",
        CertificateLocality = entity.CertificateLocality ?? "",
        CertificateOrganization = entity.CertificateOrganization ?? "",
        CertificateOrganizationUnit = entity.CertificateOrganizationUnit ?? "",
      };
    }

  }
}
