using Caliburn.Micro;
using LakerProxy.ProxyPlayground.Settings;

namespace LakerProxy.ProxyPlayground.ViewModels
{
  public class TabBaseViewModel : Screen
  {
    private bool _formEnabled = true;
    public bool FormEnabled
    {
      get { return _formEnabled; }
      set
      {
        _formEnabled = value;
        NotifyOfPropertyChange(() => FormEnabled);
      }
    }

    
  }

  public class BaseTabViewModel<T> : TabBaseViewModel where T : EtradeBase<T>
  {
    public EtradeBase<T> Settings { get; set; }
    public string ExceptionTitle => $"Fout in [{Settings.Omschrijving}] connectie";

    public override string DisplayName
    {
      get
      {
        return Settings.Omschrijving;
      }
      set
      {
        base.DisplayName = value;
      }
    }

    public BaseTabViewModel(EtradeBase<T> settings)
    {
      Settings = settings;
    }


  }
}
