using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LakerProxy.ProxyPlayground.Settings;

namespace LakerProxy.ProxyPlayground.Views.Settings
{
  /// <summary>
  /// Interaction logic for ProxyServerView.xaml
  /// </summary>
  public partial class ProxyServerView : Window
  {
    public ProxyServerView()
    {
      InitializeComponent();

      Loaded += ProxyServerViewLoaded;
    }

    void ProxyServerViewLoaded(object sender, RoutedEventArgs e)
    {
      AddComboBoxItem(ComboBoxUrl,
        @"https://proxy.lakerfield.net/");
      
#if DEBUG
      AddComboBoxItem(ComboBoxUrl,
        @"https://proxy1.lakerfield.net/",
        @"https://proxy2.lakerfield.net/",
        @"http://proxy1.lakerfield.net/",
        @"http://proxy2.lakerfield.net/");
#endif

      foreach (var groep in DataManager.Current.Root.ProxyServers
          .Select(c => c.Groep)
          .Where(g => !string.IsNullOrWhiteSpace(g))
          .Distinct())
        AddComboBoxItem(ComboBoxGroep, groep);
    }

    private void AddComboBoxItem(ComboBox comboBox, params string[] urls)
    {
      foreach (var url in urls)
        comboBox.Items.Add(new ComboBoxItem()
        {
          Content = url
        });
    }

  }
}
