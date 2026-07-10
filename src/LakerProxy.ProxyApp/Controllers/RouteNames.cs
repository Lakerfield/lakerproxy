namespace LakerProxy.ProxyApp.Controllers
{
  public static class RouteNames
  {
    public const string ApiUserGet = nameof(ApiUserGet);
    public const string ApiUserGetById = nameof(ApiUserGetById);
    public const string ApiUserPost = nameof(ApiUserPost);
    public const string ApiUserPutById = nameof(ApiUserPutById);
    public const string ApiUserDelete = nameof(ApiUserDelete);

    public const string CertificateGetByName = nameof(CertificateGetByName);

    public const string LogGet = nameof(LogGet);
    public const string LogStream = nameof(LogStream);

    public const string AcmeLogGet = nameof(AcmeLogGet);
    public const string AcmeLogStream = nameof(AcmeLogStream);

    public const string WebsiteGet = nameof(WebsiteGet);
    public const string WebsiteGetById = nameof(WebsiteGetById);
    public const string WebsiteCertificateGetById = nameof(WebsiteCertificateGetById);
    public const string WebsitePost = nameof(WebsitePost);
    public const string WebsitePutById = nameof(WebsitePutById);
    public const string WebsiteDelete = nameof(WebsiteDelete);

    public const string Obsolete = nameof(Obsolete);
  }
}
