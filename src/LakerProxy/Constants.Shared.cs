using System;

#if WEBAPI
namespace LakerProxy.WebApi
#else
namespace LakerProxy
#endif
{
  public static partial class Constants
  {
  }

  public enum AcmeAccountTypes
  {
    Unknown = 0,
    LetsEncrypt = 1,
    LetsEncryptStaging = 2,
    Custom = 3,

    /// <summary>
    /// Not yet working, "The Key Identifier was not recongnized"
    /// </summary>
    ZeroSsl = 4,
  }

  public enum CertificateStates
  {
    Unknown,
    Requesting,
    Active,
    Expired,
  }

  public enum CertificateScope
  {
    None,
    SingleDomain,
    Wildcard,
    WildcardWithApex,
  }

  public enum AcmeChallengeType
  {
    Auto = 0,
    Http01 = 1 << 0,
    Dns01 = 1 << 1,
    //TlsAlpn01 = 1 << 2,
    //DnsPersist01= 1 << 3,
  }

  [Flags]
  public enum ApiUserRights
  {
    None = 0,
    Login = 1 << 0,
    DownloadCertificate = 1 << 1,
    ManageWebsites = 1 << 2,
    ManageApiUsers = 1 << 3,

    RoleCertificate = Login | DownloadCertificate,
    RoleFullAccess = Login | DownloadCertificate | ManageWebsites,
    RoleAdministrator = RoleFullAccess | ManageApiUsers,
  }

  [Flags]
  public enum ServerServices
  {
    None,
    Proxy = 1 << 0,
    LetsEncrypt = 1 << 1,
  }

  public enum WebsiteKind
  {
    Offline = 0,
    Forward = 1,
    Redirect = 2,
    RedirectPermanent = 3,
    NotFound = 4,
    ForwardWithOriginalHostHeader = 5,
    Info = 7,
    InfoExtended = 8,
    ProxyApi = 9,
    StaticSite = 10,
    SecureServer = 11,
    FilterRedirect = 12,
  }

  public enum DestinationProtocol
  {
    Default = 0,
    Http2 = 2,
    Http3 = 3,
    Http10 = 10,
    Http11 = 11,
  }

  public enum FilterKind
  {
    None = 0,
    AllowAcl = 1,
    BlockAcl = 2,
    AllowAclRedirect = 3,
    BlockAclRedirect = 4,
  }


}
