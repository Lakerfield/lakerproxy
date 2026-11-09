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

      if (AppDeployment.IsNetworkDeployed)
      {
        var current = AppDeployment.CurrentDeployment;
        Version = $"Version: {current.CurrentVersion}";
        Location = current.UpdateLocation.ToString();
      }
      else
      {
        Version = "Version: DEBUG BUILD";
        Location = "local";
      }
    }
  }
}
