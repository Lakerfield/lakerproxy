using System.Windows;
using System.Windows.Input;
using LakerProxy.ProxyPlayground.ViewModels.Settings;

namespace LakerProxy.ProxyPlayground.Views.Settings
{
  /// <summary>
  /// Interaction logic for ProxyServersView.xaml
  /// </summary>
  public partial class ProxyServersView : Window
  {

    public ProxyServersViewModel ViewModel
    {
      get { return DataContext as ProxyServersViewModel; }
      set { DataContext = value; }
    }


    public ProxyServersView()
    {
      InitializeComponent();
    }

    private void ConnectionDataGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      ViewModel.Select();
    }
  }
}
