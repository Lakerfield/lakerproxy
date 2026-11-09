using System;

namespace LakerProxy.ProxyPlayground
{
  /// <summary>
  /// https://github.com/dotnet/deployment-tools/blob/main/Documentation/dotnet-mage/ApplicationDeployment.cs
  /// </summary>
  public class AppDeployment
  {
    private static AppDeployment currentDeployment = null;
    private static bool currentDeploymentInitialized = false;

    private static bool isNetworkDeployed = false;
    private static bool isNetworkDeployedInitialized = false;

    public static bool IsNetworkDeployed
    {
      get
      {
        if (!isNetworkDeployedInitialized)
        {
          bool.TryParse(Environment.GetEnvironmentVariable("ClickOnce_IsNetworkDeployed"), out isNetworkDeployed);
          isNetworkDeployedInitialized = true;
        }

        return isNetworkDeployed;
      }
    }

    public static AppDeployment CurrentDeployment
    {
      get
      {
        if (!currentDeploymentInitialized)
        {
          currentDeployment = IsNetworkDeployed ? new AppDeployment() : null;
          currentDeploymentInitialized = true;
        }

        return currentDeployment;
      }
    }

    public Uri ActivationUri
    {
      get
      {
        Uri.TryCreate(Environment.GetEnvironmentVariable("ClickOnce_ActivationUri"), UriKind.Absolute, out Uri val);
        return val;
      }
    }

    public Version CurrentVersion
    {
      get
      {
        Version.TryParse(Environment.GetEnvironmentVariable("ClickOnce_CurrentVersion"), out Version val);
        return val;
      }
    }
    public string DataDirectory
    {
      get { return Environment.GetEnvironmentVariable("ClickOnce_DataDirectory"); }
    }

    public bool IsFirstRun
    {
      get
      {
        bool.TryParse(Environment.GetEnvironmentVariable("ClickOnce_IsFirstRun"), out bool val);
        return val;
      }
    }

    public DateTime TimeOfLastUpdateCheck
    {
      get
      {
        DateTime.TryParse(Environment.GetEnvironmentVariable("ClickOnce_TimeOfLastUpdateCheck"), out DateTime value);
        return value;
      }
    }
    public string UpdatedApplicationFullName
    {
      get
      {
        return Environment.GetEnvironmentVariable("ClickOnce_UpdatedApplicationFullName");
      }
    }

    public Version UpdatedVersion
    {
      get
      {
        Version.TryParse(Environment.GetEnvironmentVariable("ClickOnce_UpdatedVersion"), out Version val);
        return val;
      }
    }

    public Uri UpdateLocation
    {
      get
      {
        Uri.TryCreate(Environment.GetEnvironmentVariable("ClickOnce_UpdateLocation"), UriKind.Absolute, out Uri val);
        return val;
      }
    }

    public Version LauncherVersion
    {
      get
      {
        Version.TryParse(Environment.GetEnvironmentVariable("ClickOnce_LauncherVersion"), out Version val);
        return val;
      }
    }

    private AppDeployment()
    {
      // As an alternative solution, we could initialize all properties here
    }
  }
}
