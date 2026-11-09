using System.Windows;

namespace LakerProxy.ProxyPlayground.Views
{
  /// <summary>
  /// Interaction logic for WebsiteView.xaml
  /// </summary>
  public partial class WebsiteView : Window
  {
    public WebsiteView()
    {
      InitializeComponent();

      Loaded += ViewLoaded;
    }

    void ViewLoaded(object sender, RoutedEventArgs e)
    {

    }

  }
}
