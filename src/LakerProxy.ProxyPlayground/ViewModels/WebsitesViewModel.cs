using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using LakerProxy.WebApi;
using LakerProxy.ProxyPlayground.Settings;
using LakerProxy.ProxyPlayground.Tools;
using LakerProxy.WebApi;
using Microsoft.Win32;
using Entity = LakerProxy.WebApi.Models.Website;
using EntityVm = LakerProxy.ProxyPlayground.ViewModels.WebsiteViewModel;

namespace LakerProxy.ProxyPlayground.ViewModels
{
  public class WebsitesViewModel : BaseTabViewModel<ProxyServer>
  {
    public override string DisplayName
    {
      get { return $"Websites on {Settings.Omschrijving}"; }
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
        NotifyOfPropertyChange(() => CanDownloadCertificate);
        NotifyOfPropertyChange(() => CanUploadZip);
      }
    }

    private List<EntityVm> _selectedEntities;
    public List<EntityVm> SelectedEntities
    {
      get { return _selectedEntities ??= new List<WebsiteViewModel>(); }
      set
      {
        if (Equals(value, _selectedEntities)) return;
        _selectedEntities = value;
        NotifyOfPropertyChange();
        NotifyOfPropertyChange(() => CanEdit);
        NotifyOfPropertyChange(() => CanCopy);
        NotifyOfPropertyChange(() => CanDelete);
        NotifyOfPropertyChange(() => CanDownloadCertificate);
        NotifyOfPropertyChange(() => CanUploadZip);
      }
    }


    public WebsitesViewModel(ProxyServer settings) : base(settings)
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
        var entities = await Client.WebsiteGet();

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
        var newEntity = await Client.WebsitePost(entity.Entity);

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
        var newEntity = await Client.WebsitePut(copy.Entity);

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
        var newEntity = await Client.WebsitePost(copy.Entity);

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
        if (SelectedEntities.Count > 1)
        {
          if (MessageBox.Show("Are you sure to delete all selected items?", "BE AWARE", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

          var entities = SelectedEntities.ToArray();
          foreach (var item in entities)
          {
            await Client.WebsiteDelete(item.Entity.Id);

            Entities.Remove(item);
          }
        }
        else
        {
          await Client.WebsiteDelete(entity.Entity.Id);

          Entities.Remove(entity);
        }
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



    public async void DownloadCertificate()
    {
      var entity = SelectedEntity;
      if (entity == null)
        return;

      FormEnabled = false;
      try
      {
        var websiteCertificate = await Client.WebsiteCertificateGetById(entity.Entity.Id);
        if (websiteCertificate.State != CertificateStates.Active 
            || string.IsNullOrWhiteSpace(websiteCertificate.PemCertificate) 
            || string.IsNullOrWhiteSpace(websiteCertificate.PemCertificateKey))
        {
          MessageBox.Show("No pem certificate available");
          return;
        }

        SaveFileDialog saveFileDialog = new SaveFileDialog();
        //saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        saveFileDialog.Filter = "Certificate file (*.crt)|*.crt|All files|*.*";
        saveFileDialog.FileName = $"{websiteCertificate.Hostname}.crt";
        if (saveFileDialog.ShowDialog() == true)
          File.WriteAllText(saveFileDialog.FileName, websiteCertificate.PemCertificate);

        saveFileDialog.Filter = "Certificate key file (*.key)|*.key|All files|*.*";
        saveFileDialog.FileName = Path.ChangeExtension(saveFileDialog.FileName, ".key");
        if (saveFileDialog.ShowDialog() == true)
          File.WriteAllText(saveFileDialog.FileName, websiteCertificate.PemCertificateKey);
      }
      catch (Exception exception)
      {
        App.ShowException("Download certificate failed", exception);
      }
      FormEnabled = true;
      Refresh();
    }

    public bool CanDownloadCertificate
    {
      get { return SelectedEntity != null; }
    }



    public async void UploadZip()
    {
      var entity = SelectedEntity;
      if (entity == null)
        return;

      var openFileDialog = new OpenFileDialog();
      openFileDialog.Filter = "Zip archive (*.zip)|*.zip|All files|*.*";
      openFileDialog.Title = $"Upload website zip for {entity.Entity.Hostname}";
      if (openFileDialog.ShowDialog() != true)
        return;

      FormEnabled = false;
      try
      {
        var fileName = Path.GetFileName(openFileDialog.FileName);
        var data = await File.ReadAllBytesAsync(openFileDialog.FileName);

        await Client.WebsiteUploadWebsiteData(entity.Entity.Id, fileName, data);

        MessageBox.Show($"Uploaded {fileName} ({data.Length / 1024:N0} KB) to {entity.Entity.Hostname}", "Upload website zip");
      }
      catch (Exception exception)
      {
        App.ShowException("Upload website zip failed", exception);
      }
      FormEnabled = true;
      Refresh();
    }

    public bool CanUploadZip
    {
      get { return SelectedEntity != null; }
    }



    private AcmeLogsViewModel _acmeLogs;
    public AcmeLogsViewModel AcmeLogs
    {
      get { return _acmeLogs; }
      private set
      {
        if (Equals(value, _acmeLogs)) return;
        _acmeLogs = value;
        NotifyOfPropertyChange();
      }
    }

    private bool _showAcmeLog;
    public bool ShowAcmeLog
    {
      get { return _showAcmeLog; }
      set
      {
        if (Equals(value, _showAcmeLog)) return;
        _showAcmeLog = value;
        NotifyOfPropertyChange();

        if (value)
        {
          AcmeLogs ??= new AcmeLogsViewModel(Settings);
          _ = ((IActivate)AcmeLogs).ActivateAsync(CancellationToken.None);
        }
        else if (AcmeLogs != null)
        {
          // close stops the stream; toggling back on reconnects
          _ = ((IDeactivate)AcmeLogs).DeactivateAsync(true, CancellationToken.None);
        }
      }
    }



    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
      if (close && AcmeLogs != null)
        await ((IDeactivate)AcmeLogs).DeactivateAsync(true, cancellationToken);
      await base.OnDeactivateAsync(close, cancellationToken);
    }
  }
}
