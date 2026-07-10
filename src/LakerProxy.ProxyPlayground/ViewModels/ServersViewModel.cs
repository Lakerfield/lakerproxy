using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Caliburn.Micro;
using LakerProxy.ProxyPlayground.Settings;
using LakerProxy.WebApi;
using Entity = LakerProxy.WebApi.Models.Server;
using EntityVm = LakerProxy.ProxyPlayground.ViewModels.ServerViewModel;

namespace LakerProxy.ProxyPlayground.ViewModels
{
  public class ServersViewModel : BaseTabViewModel<ProxyServer>
  {
    public override string DisplayName
    {
      get { return $"Servers on {Settings.Omschrijving}"; }
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
      }
    }

    public ServersViewModel(ProxyServer settings) : base(settings)
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
        var entities = await Client.ServerGet();

        Entities.Clear();

        var sortedEntities = entities
          .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);

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
        var newEntity = await Client.ServerPost(entity.Entity);

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
        var newEntity = await Client.ServerPut(copy.Entity);

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
      copy.Entity.Id = "";
      if (!await EditInWindow(copy))
        return;

      FormEnabled = false;
      try
      {
        var newEntity = await Client.ServerPost(copy.Entity);

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

  }
}
