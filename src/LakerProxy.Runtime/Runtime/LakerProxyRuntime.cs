using System.IO;
using System.Threading.Tasks;
using Lakerfield.Runtime;

namespace LakerProxy.Runtime
{
  public static class LakerProxyRuntime
  {
    public static Task<AppRuntimeOptions> CreateOptions(string applicationName)
    {
      var options = new AppRuntimeOptions(
        applicationName: "LakerProxy App",
        binDirectory: null,
        new AppConfigurationFile("app-configuration.ini", Optional: false),
        new AppConfigurationFile("app-configuration-override.ini", Optional: true),
        new AppConfigurationFile(Path.Combine("config", "app-configuration.ini"), Optional: true))
      {
        WriteApplicationHeader = true,
        WriteApplicationHeaderDetailed = true,
      };

      return Task.FromResult(options);
    }

  }
}
