using Caliburn.Micro;
using LakerProxy.WebApi;
using Entity = LakerProxy.WebApi.Models.Server;

namespace LakerProxy.ProxyPlayground.ViewModels
{
  public class ServerViewModel : Screen
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
        NotifyOfPropertyChange(() => ServiceProxy);
        NotifyOfPropertyChange(() => ServiceLetsEncrypt);
      }
    }

    public ServerViewModel(Entity entity)
    {
      Entity = entity;
    }

    // ServerServices is a flags enum without named combinations, so the editor
    // exposes each flag as a checkbox instead of an enum combobox
    public bool ServiceProxy
    {
      get { return Entity.Services.HasFlag(ServerServices.Proxy); }
      set
      {
        Entity.Services = value
          ? Entity.Services | ServerServices.Proxy
          : Entity.Services & ~ServerServices.Proxy;
        NotifyOfPropertyChange();
      }
    }

    public bool ServiceLetsEncrypt
    {
      get { return Entity.Services.HasFlag(ServerServices.LetsEncrypt); }
      set
      {
        Entity.Services = value
          ? Entity.Services | ServerServices.LetsEncrypt
          : Entity.Services & ~ServerServices.LetsEncrypt;
        NotifyOfPropertyChange();
      }
    }

    public ServerViewModel Clone()
    {
      return new ServerViewModel(Entity.Clone());
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
