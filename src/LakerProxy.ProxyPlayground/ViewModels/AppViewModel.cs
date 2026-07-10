using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using LakerProxy.ProxyPlayground.Settings;
using LakerProxy.ProxyPlayground.ViewModels.Settings;

namespace LakerProxy.ProxyPlayground.ViewModels
{
  public class AppViewModel : Conductor<TabBaseViewModel>.Collection.AllActive
  {

    public override string DisplayName
    {
      get { return "LakerProxy manager"; }
      set { base.DisplayName = value; }
    }

    private TabBaseViewModel _selectedTab;
    public TabBaseViewModel SelectedTab
    {
      get { return _selectedTab; }
      set
      {
        _selectedTab = value;
        NotifyOfPropertyChange(() => SelectedTab);
      }
    }

    public AppViewModel()
    {

    }



    public async void OpenWebsites()
    {
      var vm = new ProxyServersViewModel();

      dynamic windowSettings = new ExpandoObject();
      //System.Windows.Window settings;
      //windowSettings.WindowState = WindowState.Maximized;
      windowSettings.SizeToContent = SizeToContent.WidthAndHeight;

      var windowManager = IoC.Get<IWindowManager>();
      if (await windowManager.ShowDialogAsync(vm, null, windowSettings) != true)
        return;

      var proxyServer = vm.SelectedProxyServer;
      if (proxyServer == null)
        return;

      var clonedProxyServer = proxyServer.Clone();
      clonedProxyServer.Enabled = false;

      var websitesViewModel = new WebsitesViewModel(clonedProxyServer);
      await ActivateItemAsync(websitesViewModel, CancellationToken.None);
      SelectedTab = websitesViewModel;
    }



    public async void OpenUsers()
    {
      var vm = new ProxyServersViewModel();

      dynamic windowSettings = new ExpandoObject();
      //System.Windows.Window settings;
      //windowSettings.WindowState = WindowState.Maximized;
      windowSettings.SizeToContent = SizeToContent.WidthAndHeight;

      var windowManager = IoC.Get<IWindowManager>();
      if (await windowManager.ShowDialogAsync(vm, null, windowSettings) != true)
        return;

      var proxyServer = vm.SelectedProxyServer;
      if (proxyServer == null)
        return;

      var clonedProxyServer = proxyServer.Clone();
      clonedProxyServer.Enabled = false;

      var usersViewModel = new ApiUsersViewModel(clonedProxyServer);
      await ActivateItemAsync(usersViewModel, CancellationToken.None);
      SelectedTab = usersViewModel;
    }



    public async void OpenLogs()
    {
      var vm = new ProxyServersViewModel();

      dynamic windowSettings = new ExpandoObject();
      windowSettings.SizeToContent = SizeToContent.WidthAndHeight;

      var windowManager = IoC.Get<IWindowManager>();
      if (await windowManager.ShowDialogAsync(vm, null, windowSettings) != true)
        return;

      var proxyServer = vm.SelectedProxyServer;
      if (proxyServer == null)
        return;

      var clonedProxyServer = proxyServer.Clone();
      clonedProxyServer.Enabled = false;

      var logsViewModel = new LogsViewModel(clonedProxyServer);
      await ActivateItemAsync(logsViewModel, CancellationToken.None);
      SelectedTab = logsViewModel;
    }



    public async void OpenAcmeAccounts()
    {
      var vm = new ProxyServersViewModel();

      dynamic windowSettings = new ExpandoObject();
      windowSettings.SizeToContent = SizeToContent.WidthAndHeight;

      var windowManager = IoC.Get<IWindowManager>();
      if (await windowManager.ShowDialogAsync(vm, null, windowSettings) != true)
        return;

      var proxyServer = vm.SelectedProxyServer;
      if (proxyServer == null)
        return;

      var clonedProxyServer = proxyServer.Clone();
      clonedProxyServer.Enabled = false;

      var acmeAccountsViewModel = new AcmeAccountsViewModel(clonedProxyServer);
      await ActivateItemAsync(acmeAccountsViewModel, CancellationToken.None);
      SelectedTab = acmeAccountsViewModel;
    }



    public async void OpenAcmeLogs()
    {
      var vm = new ProxyServersViewModel();

      dynamic windowSettings = new ExpandoObject();
      windowSettings.SizeToContent = SizeToContent.WidthAndHeight;

      var windowManager = IoC.Get<IWindowManager>();
      if (await windowManager.ShowDialogAsync(vm, null, windowSettings) != true)
        return;

      var proxyServer = vm.SelectedProxyServer;
      if (proxyServer == null)
        return;

      var clonedProxyServer = proxyServer.Clone();
      clonedProxyServer.Enabled = false;

      var acmeLogsViewModel = new AcmeLogsViewModel(clonedProxyServer);
      await ActivateItemAsync(acmeLogsViewModel, CancellationToken.None);
      SelectedTab = acmeLogsViewModel;
    }



    public async void OpenSettingsFile()
    {
      System.Diagnostics.Process.Start("notepad.exe", DataManager.Current.SettingsFullname);
    }
    public async void CloseItem(TabBaseViewModel vm)
    {
      await DeactivateItemAsync(vm, true, CancellationToken.None);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
      await base.OnDeactivateAsync(close, cancellationToken);
    }

  }
}
