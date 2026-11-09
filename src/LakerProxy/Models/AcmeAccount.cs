using Lakerfield;

namespace LakerProxy.Models
{
  public class AcmeAccount : IId
  {
    public ShortGuid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AcmeAccountTypes Type { get; set; }
    public string AcmeDiscoveryUrl { get; set; }

    public bool Default { get; set; }
    public string Mail { get; set; } = "";
    public bool UseEab { get; set; } = false;
    public string EabKeyIdentifier { get; set; } = "";
    public string EabKey { get; set; } = "";
    /// <summary>
    /// Select algorithm currently supported HMACSHA variants: HS256 (default), HS384, HS512
    /// </summary>
    public string EabKeyAlgorithm { get; set; } = "";

    public string PreferredRootCertificate { get; set; }
    public string Key { get; set; }
    public string AccountUrl { get; set; }

    public string CertificateCountryName { get; set; }
    public string CertificateState { get; set; }
    public string CertificateLocality { get; set; }
    public string CertificateOrganization { get; set; }
    public string CertificateOrganizationUnit { get; set; }
  }
}
