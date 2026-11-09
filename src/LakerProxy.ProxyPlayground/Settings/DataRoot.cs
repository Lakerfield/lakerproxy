using System;
using System.Collections.ObjectModel;

namespace LakerProxy.ProxyPlayground.Settings
{
  [Serializable]
  public class DataRoot
  {
    private ObservableCollection<ProxyServer> _proxyServers;
    public ObservableCollection<ProxyServer> ProxyServers
    {
      get
      {
        if (_proxyServers == null)
          _proxyServers = new ObservableCollection<ProxyServer>();
        return _proxyServers;
      }
      set { _proxyServers = value; }
    }

  }
}
