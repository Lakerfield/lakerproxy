using Caliburn.Micro;
using LakerProxy.ProxyPlayground.Settings;

namespace LakerProxy.ProxyPlayground.ViewModels.Settings
{
  public class ProxyServerViewModel : Screen
  {
    public override string DisplayName
    {
      get { return "Proxy instellingen"; }
      set { }
    }

    private ProxyServer _proxyServer;
    public ProxyServer ProxyServer
    {
      get { return _proxyServer; }
      set
      {
        if (_proxyServer != null)
          _proxyServer.PropertyChanged -= ProxyServerPropertyChanged;
        _proxyServer = value;
        NotifyOfPropertyChange();
        if (_proxyServer != null)
          _proxyServer.PropertyChanged += ProxyServerPropertyChanged;
      }
    }

    public ProxyServerViewModel(ProxyServer proxyServer)
    {
      ProxyServer = proxyServer;
    }

    void ProxyServerPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
      NotifyOfPropertyChange(() => CanOk);
    }


    public async void Ok()
    {
      if (!CanOk)
        return;
      await TryCloseAsync(true);
    }

    public bool CanOk
    {
      get { return ProxyServer.IsValid(); }
    }

    public async void Cancel()
    {
      await TryCloseAsync(false);
    }

  }
}
