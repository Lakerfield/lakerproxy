using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using LakerProxy.ProxyPlayground.Tools;
using LakerProxy.ProxyPlayground.ViewModels;

namespace LakerProxy.ProxyPlayground.Views
{
  /// <summary>
  /// Interaction logic for WebsitesView.xaml
  /// </summary>
  public partial class WebsitesView : UserControl
  {

    public WebsitesViewModel ViewModel
    {
      get { return DataContext as WebsitesViewModel; }
      set { DataContext = value; }
    }


    public WebsitesView()
    {
      InitializeComponent();
    }

    private void ConnectionDataGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      ViewModel.Edit();
    }

    private void ConnectionDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var grid = sender as DataGrid;

      var selectedObjects = grid?.SelectedItems.OfType<WebsiteViewModel>().ToList();
      ViewModel.SelectedEntities = selectedObjects;
    }

    private void DataGridSorting(object sender, DataGridSortingEventArgs e)
    {
      var dataGrid = sender as DataGrid;
      if (dataGrid == null)
        return;

      var listCollectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
      if (listCollectionView == null)
        return;

      var sortDirection = (e.Column.SortDirection != ListSortDirection.Ascending)
        ? ListSortDirection.Ascending
        : ListSortDirection.Descending;

      switch (e.Column.SortMemberPath)
      {
        case "Entity.Servers":
          listCollectionView.CustomSort = new WebsiteServersComparer(sortDirection);
          break;

        case "Entity.Hostname":
          listCollectionView.CustomSort = new WebsiteHostnameComparer(sortDirection);
          break;

        case "CertificateInfo":
          listCollectionView.CustomSort = new WebsiteCertificateExpireDateComparer(sortDirection);
          break;

        case "DnsV4Info":
          listCollectionView.CustomSort = new WebsiteDnsComparer(vm => vm.DnsV4, sortDirection);
          break;

        case "DnsV6Info":
          listCollectionView.CustomSort = new WebsiteDnsComparer(vm => vm.DnsV6, sortDirection);
          break;

        default:
          return;
      }

      e.Column.SortDirection = sortDirection;

      //We've handled the sort
      e.Handled = true;
    }

  }


}
