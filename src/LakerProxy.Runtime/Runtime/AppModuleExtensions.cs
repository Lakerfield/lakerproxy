using Lakerfield.Runtime;

namespace LakerProxy.Runtime
{
  public static class AppModuleExtensions
  {
    public static AppModules AddLakerProxy(this AppModules modules)
    {
      return modules.Add<LakerProxyModule>();
    }

    public static LakerProxyModule LakerProxy(this AppModules modules)
    {
      return modules.Get<LakerProxyModule>();
    }

    
  }
}
