namespace LakerProxy.ProxyPlayground.Settings
{
  public class ProxyServer : EtradeBase<ProxyServer>
  {

    private string _url;
    public string Url
    {
      get { return _url; }
      set
      {
        _url = value;
        NotifyOfPropertyChange();
      }
    }

    private string _username;
    public string Username
    {
      get { return _username; }
      set
      {
        _username = value;
        NotifyOfPropertyChange();
      }
    }

    private string _password;
    public string Password
    {
      get { return _password; }
      set
      {
        _password = value;
        NotifyOfPropertyChange();
      }
    }




    public ProxyServer()
    {
      Enabled = true;
    }

    public override ProxyServer Clone()
    {
      return (ProxyServer)MemberwiseClone();
    }

    public override bool IsValid()
    {
      var result = base.IsValid();
      result &= !string.IsNullOrWhiteSpace(Omschrijving);
      result &= !string.IsNullOrWhiteSpace(Url);
      return result;
    }
  }
}
