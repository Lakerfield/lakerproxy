using System.Windows.Controls;

namespace LakerProxy.ProxyPlayground.Views
{
  /// <summary>
  /// Interaction logic for AppVersion.xaml
  /// </summary>
  public partial class AppVersion : UserControl
  {
    public string Version { get; set; }
    public string Location { get; set; }

    public AppVersion()
    {
      InitializeComponent();

      DataContext = this;

      Version = $"Version: {AppUpdater.CurrentVersion}";
      Location = AppUpdater.IsInstalled ? AppUpdater.UpdateUrl : "local build";
    }
  }
}
