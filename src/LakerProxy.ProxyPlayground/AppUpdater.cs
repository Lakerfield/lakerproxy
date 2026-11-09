using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Velopack;

namespace LakerProxy.ProxyPlayground
{
  /// <summary>
  /// Velopack based self-update of the manager. Releases are published by
  /// .github/workflows/release-manager.yaml to the S3 bucket behind UpdateUrl.
  /// </summary>
  public static class AppUpdater
  {
    public const string UpdateUrl = "https://download.lakerproxy.net/manager";

    private static readonly Lazy<UpdateManager> Manager = new(() => new UpdateManager(UpdateUrl));

    /// <summary>True when running from a Velopack installation, false for debug/portable runs.</summary>
    public static bool IsInstalled
    {
      get
      {
        try { return Manager.Value.IsInstalled; }
        catch { return false; }
      }
    }

    /// <summary>Installed Velopack version, or the assembly version when not installed.</summary>
    public static string CurrentVersion
    {
      get
      {
        try
        {
          if (Manager.Value.IsInstalled && Manager.Value.CurrentVersion != null)
            return Manager.Value.CurrentVersion.ToString();
        }
        catch
        {
          // fall through to the assembly version
        }

        var assembly = typeof(AppUpdater).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
          // strip the "+githash" suffix the SDK appends
          var plus = informational.IndexOf('+');
          return plus > 0 ? informational.Substring(0, plus) : informational;
        }
        return assembly.GetName().Version?.ToString() ?? "unknown";
      }
    }

    /// <summary>
    /// Checks for a newer release, downloads it in the background and asks the
    /// user to restart. Never throws: an unreachable update server must not
    /// bother the user, the check simply runs again next start.
    /// </summary>
    public static async Task CheckForUpdatesAsync()
    {
      try
      {
        var manager = Manager.Value;
        if (!manager.IsInstalled)
          return;

        var update = await manager.CheckForUpdatesAsync();
        if (update == null)
          return;

        await manager.DownloadUpdatesAsync(update);

        var result = await Application.Current.Dispatcher.InvokeAsync(() =>
          MessageBox.Show(
            $"LakerProxy Manager {update.TargetFullRelease.Version} has been downloaded.\n\nRestart now to install it?",
            "Update available",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information));

        if (result == MessageBoxResult.Yes)
          manager.ApplyUpdatesAndRestart(update);
      }
      catch (Exception)
      {
        // ignored: no network, no releases yet, or the update server is down
      }
    }
  }
}
