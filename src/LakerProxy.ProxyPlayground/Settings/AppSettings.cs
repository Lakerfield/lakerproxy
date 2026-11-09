using Caliburn.Micro;

namespace LakerProxy.ProxyPlayground.Settings
{
  public class AppSettings : PropertyChangedBase
  {
    public const string DefaultApiBaseUrl = @"https://flori.app/";
    public const string DefaultCode = @"default";

    private string _code = DefaultCode;
    public string Code
    {
      get { return _code; }
      set
      {
        _code = value.Trim().ToLower();
        NotifyOfPropertyChange();
      }
    }

    private string _apiBaseUrl = DefaultApiBaseUrl;
    public string ApiBaseUrl
    {
      get { return _apiBaseUrl; }
      set
      {
        _apiBaseUrl = value;
        NotifyOfPropertyChange();
      }
    }

    private bool _enableExtendedApi;
    public bool EnableExtendedApi
    {
      get { return _enableExtendedApi; }
      set
      {
        _enableExtendedApi = value;
        NotifyOfPropertyChange();
      }
    }

    private string _apiKey;
    public string ApiKey
    {
      get { return (_apiKey ?? "").Trim(); }
      set
      {
        _apiKey = value;
        NotifyOfPropertyChange();
      }
    }



  }
}
