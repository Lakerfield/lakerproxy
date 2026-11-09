using System.Windows;

namespace LakerProxy.ProxyPlayground.Views
{
  /// <summary>
  /// Interaction logic for ApiUserView.xaml
  /// </summary>
  public partial class ApiUserView : Window
  {
    public ApiUserView()
    {
      InitializeComponent();

      Loaded += ViewLoaded;
    }

    void ViewLoaded(object sender, RoutedEventArgs e)
    {

    }

  }
}
