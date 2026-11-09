using System.Windows.Controls;
using System.Windows.Input;
using LakerProxy.ProxyPlayground.ViewModels;

namespace LakerProxy.ProxyPlayground.Views
{
  /// <summary>
  /// Interaction logic for ApiUsersView.xaml
  /// </summary>
  public partial class ApiUsersView : UserControl
  {

    public ApiUsersViewModel ViewModel
    {
      get { return DataContext as ApiUsersViewModel; }
      set { DataContext = value; }
    }


    public ApiUsersView()
    {
      InitializeComponent();
    }

    private void ConnectionDataGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      ViewModel.Edit();
    }
  }
}
