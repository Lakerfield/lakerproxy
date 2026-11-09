using System;
using System.Threading.Tasks;
using Lakerfield.Runtime;
using Lakerfield.Storage;
using LakerProxy.Service;
using LakerProxy.Storage.Collections;
using MongoDB.Driver;

namespace LakerProxy.Storage
{
  public class AppMongoDbService : AMongoDbConfig, IAppService
  {
    public ILogCollection Log { get; private set; }
    public IAcmeAccountCollection AcmeAccount { get; private set; }
    public AcmeLogCollection AcmeLog { get; private set; }
    public IApiUserCollection ApiUser { get; private set; }
    public IServerCollection Server { get; private set; }
    public IWebsiteCollection Website { get; private set; }
    public IWebsiteDataCollection WebsiteData { get; private set; }

    public override Task PreInitialization(IMongoDatabase database)
    {
      return base.PreInitialization(database);
    }

    public override async Task<AppServices> InitializeCollections(IMongoDatabase database, AppServices services)
    {
      services = await base.InitializeCollections(database, services);

      services = await services.InitializeCollection<LogCollection>(database, s => Log = s);
      try
      {
        services = await services
          .InitializeCollection<AcmeAccountCollection>(database, s => AcmeAccount = s)
          .InitializeCollection<AcmeLogCollection>(database, s => AcmeLog = s)
          .InitializeCollection<ApiUserCollection>(database, s => ApiUser = s)
          .InitializeCollection<ServerCollection>(database, s => Server = s)
          .InitializeCollection<WebsiteCollection>(database, s => Website = s)
          .InitializeCollection<WebsiteDataCollection>(database, s => WebsiteData = s);
      }
      catch (Exception)
      {
        try
        {
          var log = Log.New($"Exception in initialize of {nameof(AppMongoDbService)}");
          log.UpgradeLogLevel(FloriAppLogLevel.Fatal);

          await Log.Insert(log);
        }
        catch (Exception exception)
        {
          Console.WriteLine($"Exception while saving exception log object: {exception.Message}");
        }
        throw;
      }

      return services;
    }

    public override Task InitializationComplete(AppServices services)
    {
      return base.InitializationComplete(services);
    }

  }
}
