namespace LakerProxy.WebApi.Models
{
  public class AcmeAccount
  {
    public string Id { get; set; } = "";

    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AcmeAccountTypes Type { get; set; }
    public string AcmeDiscoveryUrl { get; set; }

    public bool Default { get; set; }

    public string Mail { get; set; } = "";

    public bool UseEab { get; set; }
    public string EabKeyIdentifier { get; set; } = "";
    /// <summary>
    /// Secret: the server never returns it and only applies it when non-empty
    /// </summary>
    public string EabKey { get; set; } = "";
    public string EabKeyAlgorithm { get; set; } = "";

    public string PreferredRootCertificate { get; set; } = "";

    /// <summary>
    /// Read-only acme registration state
    /// </summary>
    public string AccountUrl { get; set; } = "";

    public string CertificateCountryName { get; set; } = "";
    public string CertificateState { get; set; } = "";
    public string CertificateLocality { get; set; } = "";
    public string CertificateOrganization { get; set; } = "";
    public string CertificateOrganizationUnit { get; set; } = "";



    public AcmeAccount Clone()
    {
      return (AcmeAccount)MemberwiseClone();
    }
  }
}
