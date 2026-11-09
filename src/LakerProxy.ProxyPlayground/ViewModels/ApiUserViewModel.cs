using Caliburn.Micro;
using Lakerfield;
using Entity = LakerProxy.WebApi.Models.ApiUser;

namespace LakerProxy.ProxyPlayground.ViewModels
{
  public class ApiUserViewModel : Screen
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

    public ApiUserViewModel(Entity entity)
    {
      Entity = entity;
    }

    public ApiUserViewModel Clone()
    {
      return new ApiUserViewModel(Entity.Clone());

    }

    public void GenerateApiKey()
    {
      Entity.ApiKey = ShortGuid.NewGuid();
      Entity = Entity;
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
