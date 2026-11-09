using System.Linq;
using System.Reflection;

namespace LakerProxy.ProxyApp
{
  public static class EnvironmentHelper
  {
    private static string _gitHash;
    /// <summary>
    /// https://www.hanselman.com/blog/adding-a-git-commit-hash-and-azure-devops-build-number-and-build-id-to-an-aspnet-website
    /// </summary>
    public static string GitHash
    {
      get
      {
        if (string.IsNullOrEmpty(_gitHash))
        {
          var version = "1.0.0+LOCALBUILD"; // Dummy version for local dev
          var appAssembly = typeof(EnvironmentHelper).Assembly;
          var infoVerAttr = (AssemblyInformationalVersionAttribute)appAssembly
            .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute)).FirstOrDefault();

          if (infoVerAttr != null && infoVerAttr.InformationalVersion.Length > 6)
          {
            // Hash is embedded in the version after a '+' symbol, e.g. 1.0.0+a34a913742f8845d3da5309b7b17242222d41a21
            version = infoVerAttr.InformationalVersion;
          }
          _gitHash = version.Substring(version.IndexOf('+') + 1);

        }

        return _gitHash;
      }
    }

    private static string _gitShortHash;
    public static string ShortGitHash
    {
      get
      {
        if (string.IsNullOrEmpty(_gitShortHash))
        {
          _gitShortHash = GitHash.Substring(GitHash.Length - 6, 6);
        }
        return _gitShortHash;
      }
    }

  }
}
