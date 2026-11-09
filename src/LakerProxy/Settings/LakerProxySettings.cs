using Lakerfield;
using Lakerfield.Settings;
using Microsoft.Extensions.Configuration;

namespace LakerProxy.Settings
{
  public class LakerProxySettings
  {
    public string InitialAdminToken { get; }
    public string InitialHostname { get; }
    public bool InitialHostnameWithCertificate { get; }

    public string AcmeMail { get; }
    public string AcmeOrganization { get; set; }
    public string AcmeOrganizationUnit { get; set; }
    public string AcmeLocality { get; set; }
    public string AcmeState { get; set; }
    public string AcmeCountry { get; set; }

    public LakerProxySettings(IConfiguration configuration)
    {
      var dataConfiguration = configuration.GetSection("data");

      InitialAdminToken = (dataConfiguration["initial-admin-token"] ?? "").Trim();
      InitialHostname = (dataConfiguration["initial-hostname"] ?? "").Trim();
      InitialHostnameWithCertificate = dataConfiguration["initial-hostname-with-certificate"].ToBool(false);

      AcmeMail = (dataConfiguration["acme-mail"] ?? "").Trim();
      AcmeOrganization = (dataConfiguration["acme-organization"] ?? "").Trim();
      AcmeOrganizationUnit = (dataConfiguration["acme-organization-unit"] ?? "").Trim();
      AcmeLocality = (dataConfiguration["acme-locality"] ?? "").Trim();
      AcmeState = (dataConfiguration["acme-state"] ?? "").Trim();
      AcmeCountry = (dataConfiguration["acme-country"] ?? "").Trim();
    }

  }

  public static class AppSettingsExtensions
  {
    public static LakerProxySettings LakerProxy(this AppSettings settings)
    {
      return settings.Get<LakerProxySettings>();
    }

  }

}
