using Lakerfield.Runtime;
using LakerProxy.Storage.Collections;

namespace LakerProxy
{
  public static class ServicesExtensions
  {

    public static AcmeAccountCollection AcmeAccount(this AppServices services)
    {
      return services.Get<AcmeAccountCollection>();
    }

    public static AcmeLogCollection AcmeLog(this AppServices services)
    {
      return services.Get<AcmeLogCollection>();
    }

    public static ApiUserCollection ApiUser(this AppServices services)
    {
      return services.Get<ApiUserCollection>();
    }

    public static LogCollection Log(this AppServices services)
    {
      return services.Get<LogCollection>();
    }

    public static ServerCollection Server(this AppServices services)
    {
      return services.Get<ServerCollection>();
    }

    public static WebsiteCollection Website(this AppServices services)
    {
      return services.Get<WebsiteCollection>();
    }

    public static WebsiteDataCollection WebsiteData(this AppServices services)
    {
      return services.Get<WebsiteDataCollection>();
    }

  }
}
