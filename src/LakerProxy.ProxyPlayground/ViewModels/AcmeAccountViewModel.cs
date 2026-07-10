using Caliburn.Micro;
using Entity = LakerProxy.WebApi.Models.AcmeAccount;

namespace LakerProxy.ProxyPlayground.ViewModels
{
  public class AcmeAccountViewModel : Screen
  {
    public override string DisplayName
    {
      get { return typeof(Entity).Name; }
      set { }
    }

    private Entity _entity;
    public Entity Entity
    {
      get { return _entity; }
      set
      {
        _entity = value;
        NotifyOfPropertyChange();
      }
    }

    public AcmeAccountViewModel(Entity entity)
    {
      Entity = entity;
    }

    public AcmeAccountViewModel Clone()
    {
      return new AcmeAccountViewModel(Entity.Clone());
    }

    public async void Ok()
    {
      if (!CanOk)
        return;
      await TryCloseAsync(true);
    }

    public bool CanOk
    {
      get { return true; }
    }

    public async void Cancel()
    {
      await TryCloseAsync(false);
    }

  }
}
