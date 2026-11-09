using LakerProxy.WebApi;

namespace LakerProxy.ProxyPlayground.Converters
{
  public class EnumListApiUserRights : EnumListConverter<ApiUserRights> { }
  public class EnumListCertificateScope : EnumListConverter<CertificateScope> { }
  public class EnumListDestinationProtocol : EnumListConverter<DestinationProtocol> { }
  public class EnumListFilterKind : EnumListConverter<FilterKind> { }
  public class EnumListWebsiteKind : EnumListConverter<WebsiteKind> { }
}
