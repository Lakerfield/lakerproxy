using System;
using System.Threading.Tasks;
using Lakerfield.Exceptions;
using Lakerfield.Runtime;
using Lakerfield.Settings;
using LakerProxy.Bson;
using LakerProxy.Settings;
using LakerProxy.Storage;
using Microsoft.Extensions.Configuration;

namespace LakerProxy.Runtime
{
  public class LakerProxyModule : AppModule
  {
    private LakerProxySettings? _settings;
    public LakerProxySettings Settings => _settings ?? throw new ApplicationFailureException($"{nameof(Settings)} is not available before initialization of {nameof(LakerProxyModule)}");


    public LakerProxyModule() : base("LakerProxy App")
    {
    }

    protected override void ModuleRegistration(AppModules modules)
    {
      // validate required modules exists
      var bson = modules.Bson();
      var mongoDb = modules.MongoDb();
    }

    protected override Task ModuleSettingsInitialization(IConfiguration configuration, AppSettings settings, Action<string> writeHeader)
    {
      writeHeader($"Module {nameof(LakerProxyModule)}");

      _settings = settings.Add(new LakerProxySettings(configuration));

      return Task.CompletedTask;
    }

    protected override Task ModulePreInitialization(AppModules modules)
    {
      var bsonModule = modules.Bson();
      bsonModule.RegisterConfig(new AppBsonConfiguration());

      var mongoDbModule = modules.MongoDb();
      mongoDbModule.RegisterConfig(new AppMongoDbService());

      return Task.CompletedTask;
    }

    protected override Task<AppServices> ModuleInitialization(AppModules modules, AppServices services)
    {
      return Task.FromResult(services);
    }

    protected override Task ModuleInitialized(AppRuntime runtime)
    {
      return Task.CompletedTask;
    }



    public override ValueTask DisposeAsync()
    {
      return ValueTask.CompletedTask;
    }
  }
}
