using Caliburn.Micro;

namespace LakerProxy.ProxyPlayground.Settings
{
  public abstract class EtradeBase<T> : PropertyChangedBase where T : EtradeBase<T>
  {

    private bool _enabled;
    public bool Enabled
    {
      get { return _enabled; }
      set
      {
        _enabled = value;
        NotifyOfPropertyChange();
      }
    }

    private string _groep;
    public string Groep
    {
      get { return _groep; }
      set
      {
        _groep = value;
        NotifyOfPropertyChange();
      }
    }

    private string _omschrijving;
    public string Omschrijving
    {
      get { return _omschrijving; }
      set
      {
        _omschrijving = value;
        NotifyOfPropertyChange();
      }
    }

    private string _comments;
    public string Comments
    {
      get { return _comments; }
      set
      {
        _comments = value;
        NotifyOfPropertyChange();
      }
    }



    public abstract T Clone();

    public virtual bool IsValid()
    {
      var result = true;
      result &= !string.IsNullOrWhiteSpace(Omschrijving);
      return result;
    }

  }
}
