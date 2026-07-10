using System.Windows.Controls;
using System.Windows.Input;
using LakerProxy.ProxyPlayground.ViewModels;

namespace LakerProxy.ProxyPlayground.Views
{
  public partial class ServersView : UserControl
  {

    public ServersViewModel ViewModel
    {
      get { return DataContext as ServersViewModel; }
      set { DataContext = value; }
    }


    public ServersView()
    {
      InitializeComponent();
    }

    private void ConnectionDataGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      ViewModel.Edit();
    }
  }
}
