using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LakerProxy.ProxyPlayground.Views
{
  /// <summary>
  /// Interaction logic for AppView.xaml
  /// </summary>
  public partial class AppView : Window, INotifyPropertyChanged
  {
    public ViewModels.AppViewModel ViewModel
    {
      get { return DataContext as ViewModels.AppViewModel; }
    }

    public string ProgramVersion
    {
      get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
    }

    public AppView()
    {
      InitializeComponent();
    }


    public void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
    {
      PropertyChangedEventHandler changedEventHandler = this.PropertyChanged;
      if (changedEventHandler == null)
        return;
      changedEventHandler(this, new PropertyChangedEventArgs(propertyName));
    }

    public event PropertyChangedEventHandler PropertyChanged;


    public Task WhenClicked(Button btn)
    {
      var tcs = new TaskCompletionSource<bool>();
      RoutedEventHandler lambda = null;
      lambda = (s, e) =>
      {
        btn.Click -= lambda;
        tcs.SetResult(true);
      };
      btn.Click += lambda;
      return tcs.Task;
    }

  }
}
