using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using LakerProxy.ProxyPlayground.Settings;

namespace LakerProxy.ProxyPlayground.ViewModels.Settings
{
  public class ProxyServersViewModel : Screen
  {
    public override string DisplayName
    {
      get { return "Verbindingen"; }
      set { }
    }

    public ObservableCollection<ProxyServer> ProxyServers { get; set; }

    private ProxyServer _selectedProxyServer;
    public ProxyServer SelectedProxyServer
    {
      get { return _selectedProxyServer; }
      set
      {
        if (Equals(value, _selectedProxyServer)) return;
        _selectedProxyServer = value;
        NotifyOfPropertyChange();
        NotifyOfPropertyChange(() => CanEdit);
        NotifyOfPropertyChange(() => CanCopy);
        NotifyOfPropertyChange(() => CanDelete);
        NotifyOfPropertyChange(() => CanSelect);
      }
    }

    public ProxyServersViewModel()
    {
      ProxyServers = DataManager.Current.Root.ProxyServers;
    }

    public async void New()
    {
      var proxyServer = new ProxyServer();
      if (await Edit(proxyServer))
        ProxyServers.Add(proxyServer);
    }



    public async Task<bool> Edit(ProxyServer proxyServer = null)
    {
      proxyServer = proxyServer ?? SelectedProxyServer;
      if (proxyServer == null)
        return false;

      var viewmodel = new ProxyServerViewModel(proxyServer);

      var windowManager = IoC.Get<IWindowManager>();
      if (await windowManager.ShowDialogAsync(viewmodel) == true)
        return true;
      return false;
    }

    public bool CanEdit
    {
      get { return SelectedProxyServer != null; }
    }



    public async Task<bool> Copy()
    {
      var original = SelectedProxyServer;
      if (original == null)
        return false;

      var copy = original.Clone();
      if (await Edit(copy))
        ProxyServers.Add(copy);
      else
        return false;
      return true;
    }

    public bool CanCopy
    {
      get { return SelectedProxyServer != null; }
    }



    public void Delete()
    {
      var proxyServer = SelectedProxyServer;
      if (proxyServer != null)
        ProxyServers.Remove(proxyServer);
    }

    public bool CanDelete
    {
      get { return SelectedProxyServer != null; }
    }



    public async void Select()
    {
      await TryCloseAsync(true);
    }

    public bool CanSelect
    {
      get { return SelectedProxyServer != null; }
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
      DataManager.Current.Save();

      await base.OnDeactivateAsync(close, cancellationToken);
    }
  }
}
