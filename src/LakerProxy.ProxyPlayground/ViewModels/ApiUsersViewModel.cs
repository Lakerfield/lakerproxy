using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using LakerProxy.ProxyPlayground.Settings;
using LakerProxy.ProxyPlayground.Tools;
using LakerProxy.WebApi;
using Entity = LakerProxy.WebApi.Models.ApiUser;
using EntityVm = LakerProxy.ProxyPlayground.ViewModels.ApiUserViewModel;

namespace LakerProxy.ProxyPlayground.ViewModels
{
  public class ApiUsersViewModel : BaseTabViewModel<ProxyServer>
  {
    public override string DisplayName
    {
      get { return $"Users on {Settings.Omschrijving}"; }
      set { }
    }

    public new ProxyServer Settings => (ProxyServer)base.Settings;

    public LakerProxyApiClient Client { get; private set; }

    public ObservableCollection<EntityVm> Entities { get; private set; }

    private EntityVm _selectedEntity;
    public EntityVm SelectedEntity
    {
      get { return _selectedEntity; }
      set
      {
        if (Equals(value, _selectedEntity)) return;
        _selectedEntity = value;
        NotifyOfPropertyChange();
        NotifyOfPropertyChange(() => CanEdit);
        NotifyOfPropertyChange(() => CanCopy);
        NotifyOfPropertyChange(() => CanDelete);
      }
    }

    public ApiUsersViewModel(ProxyServer settings) : base(settings)
    {
      Entities = new ObservableCollection<EntityVm>();

      Client = new LakerProxyApiClient(new LakerProxyApiOptions()
      {
        ApiBaseUrl = settings.Url,
        ApiKey = settings.Username + settings.Password,
      });

      Refresh();
    }


    public async void Refresh()
    {
      FormEnabled = false;
      try
      {
        var entities = await Client.ApiUserGet();

        Entities.Clear();

        var sortedEntities = entities
          .OrderBy(e => e.Hostname, new HostnameComparer());

        foreach (var entity in sortedEntities)
          Entities.Add(new EntityVm(entity));
      }
      catch (Exception exception)
      {
        App.ShowException(ExceptionTitle, exception);
      }
      finally
      {
        FormEnabled = true;
      }
    }

    private async Task<bool> EditInWindow(EntityVm entityVm)
    {
      var windowManager = IoC.Get<IWindowManager>();
      if (await windowManager.ShowDialogAsync(entityVm) == true)
        return true;
      return false;
    }


    public async void New()
    {
      var entity = new EntityVm(new Entity());
      if (!await EditInWindow(entity))
        return;

      FormEnabled = false;
      try
      {
        var newEntity = await Client.ApiUserPost(entity.Entity);

        Entities.Add(new EntityVm(newEntity));
      }
      catch (Exception exception)
      {
        App.ShowException("Create failed", exception);
      }
      FormEnabled = true;
      Refresh();
    }



    public async void Edit(EntityVm entityVm = null)
    {
      entityVm = entityVm ?? SelectedEntity;
      if (entityVm == null)
        return;

      var copy = entityVm.Clone();
      if (!await EditInWindow(copy))
        return;

      FormEnabled = false;
      try
      {
        var newEntity = await Client.ApiUserPut(copy.Entity);

        Entities.Add(new EntityVm(newEntity));
      }
      catch (Exception exception)
      {
        App.ShowException("Update failed", exception);
      }
      FormEnabled = true;
      Refresh();
    }

    public bool CanEdit
    {
      get { return SelectedEntity != null; }
    }



    public async void Copy()
    {
      var original = SelectedEntity;
      if (original == null)
        return;

      var copy = original.Clone();
      if (!await EditInWindow(copy))
        return;

      FormEnabled = false;
      try
      {
        var newEntity = await Client.ApiUserPost(copy.Entity);

        Entities.Add(new EntityVm(newEntity));
      }
      catch (Exception exception)
      {
        App.ShowException("Create failed", exception);
      }
      FormEnabled = true;
      Refresh();
    }

    public bool CanCopy
    {
      get { return SelectedEntity != null; }
    }



    public async void Delete()
    {
      var entity = SelectedEntity;
      if (entity == null)
        return;

      FormEnabled = false;
      try
      {
        await Client.ApiUserDelete(entity.Entity.Id);

        Entities.Remove(entity);
      }
      catch (Exception exception)
      {
        App.ShowException("Delete failed", exception);
      }
      FormEnabled = true;
      Refresh();
    }

    public bool CanDelete
    {
      get { return SelectedEntity != null; }
    }



    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
      await base.OnDeactivateAsync(close, cancellationToken);
    }
  }
}
