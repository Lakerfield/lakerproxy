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

    private ProxyServer _connectedServer;
    public ProxyServer ConnectedServer
    {
      get { return _connectedServer; }
      private set
      {
        _connectedServer = value;
        NotifyOfPropertyChange();
        NotifyOfPropertyChange(() => ConnectedServerName);
        NotifyOfPropertyChange(() => CanOpenWebsites);
        NotifyOfPropertyChange(() => CanOpenUsers);
        NotifyOfPropertyChange(() => CanOpenServers);
        NotifyOfPropertyChange(() => CanOpenAcmeAccounts);
        NotifyOfPropertyChange(() => CanOpenLogs);
        NotifyOfPropertyChange(() => CanOpenAcmeLogs);
      }
    }

    public string ConnectedServerName
    {
      get { return ConnectedServer?.Omschrijving ?? "- not connected -"; }
    }

    public AppViewModel()
    {

    }



    public async void Connect()
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

      ConnectedServer = clonedProxyServer;

      OpenWebsites();
    }



    private async Task OpenTab(TabBaseViewModel vm)
    {
      await ActivateItemAsync(vm, CancellationToken.None);
      SelectedTab = vm;
    }

    public async void OpenWebsites()
    {
      await OpenTab(new WebsitesViewModel(ConnectedServer.Clone()));
    }

    public bool CanOpenWebsites
    {
      get { return ConnectedServer != null; }
    }

    public async void OpenUsers()
    {
      await OpenTab(new ApiUsersViewModel(ConnectedServer.Clone()));
    }

    public bool CanOpenUsers
    {
      get { return ConnectedServer != null; }
    }

    public async void OpenServers()
    {
      await OpenTab(new ServersViewModel(ConnectedServer.Clone()));
    }

    public bool CanOpenServers
    {
      get { return ConnectedServer != null; }
    }

    public async void OpenAcmeAccounts()
    {
      await OpenTab(new AcmeAccountsViewModel(ConnectedServer.Clone()));
    }

    public bool CanOpenAcmeAccounts
    {
      get { return ConnectedServer != null; }
    }

    public async void OpenLogs()
    {
      await OpenTab(new LogsViewModel(ConnectedServer.Clone()));
    }

    public bool CanOpenLogs
    {
      get { return ConnectedServer != null; }
    }

    public async void OpenAcmeLogs()
    {
      await OpenTab(new AcmeLogsViewModel(ConnectedServer.Clone()));
    }

    public bool CanOpenAcmeLogs
    {
      get { return ConnectedServer != null; }
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
