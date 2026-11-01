using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Caliburn.Micro;
using LakerProxy.WebApi;
using LakerProxy.ProxyPlayground.Settings;
using LakerProxy.ProxyPlayground.Tools;
using LakerProxy.ProxyPlayground.Tools.DnsCheck;
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
        NotifyOfPropertyChange(() => CanDeleteZip);
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
        NotifyOfPropertyChange(() => CanDeleteZip);
      }
    }


    public WebsitesViewModel(ProxyServer settings) : base(settings)
    {
      Entities = new ObservableCollection<EntityVm>();

      // the grid binds to the default view of Entities; the filter lives on that view,
      // so it survives Refresh() (which only clears/refills the collection) and
      // combines with the CustomSort set by the view's sorting handler
      _entitiesView = CollectionViewSource.GetDefaultView(Entities);
      _entitiesView.Filter = FilterEntity;
      ((INotifyCollectionChanged)_entitiesView).CollectionChanged += (_, _) => NotifyOfPropertyChange(() => FilterSummary);

      // constructed on the UI thread, so the current synchronization context is the dispatcher
      _filterSubscription = _filterChanged
        .Throttle(FilterDebounce)
        .ObserveOn(SynchronizationContext.Current)
        .Subscribe(_ => ApplyFilters());

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



    public async void DeleteZip()
    {
      var entity = SelectedEntity;
      if (entity == null)
        return;

      if (MessageBox.Show($"Delete the uploaded website zip of {entity.Entity.Hostname}?", "Delete website zip", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        return;

      FormEnabled = false;
      try
      {
        await Client.WebsiteDeleteWebsiteData(entity.Entity.Id);
      }
      catch (Exception exception)
      {
        App.ShowException("Delete website zip failed", exception);
      }
      FormEnabled = true;
      Refresh();
    }

    public bool CanDeleteZip
    {
      get { return SelectedEntity != null && SelectedEntity.Entity.HasWebsiteData; }
    }



    private readonly ICollectionView _entitiesView;

    private string _hostnameFilter = "";
    /// <summary>
    /// Case-insensitive substring match on the hostname
    /// </summary>
    public string HostnameFilter
    {
      get { return _hostnameFilter; }
      set
      {
        if (Equals(value, _hostnameFilter)) return;
        _hostnameFilter = value ?? "";
        NotifyOfPropertyChange();
        ScheduleApplyFilters();
      }
    }

    private string _tagFilter = "";
    /// <summary>
    /// Space separated words; a website matches when every word is a (case-insensitive) prefix of one of its tags
    /// </summary>
    public string TagFilter
    {
      get { return _tagFilter; }
      set
      {
        if (Equals(value, _tagFilter)) return;
        _tagFilter = value ?? "";
        NotifyOfPropertyChange();
        ScheduleApplyFilters();
      }
    }

    public bool HasFilter => !string.IsNullOrWhiteSpace(HostnameFilter) || !string.IsNullOrWhiteSpace(TagFilter);

    public string FilterSummary
    {
      get
      {
        if (!HasFilter)
          return "";
        return $"{_entitiesView.Cast<object>().Count()} of {Entities.Count}";
      }
    }

    public void ClearFilters()
    {
      HostnameFilter = "";
      TagFilter = "";
      // no need to wait for the debounce when clearing
      ApplyFilters();
    }

    // refreshing the view on every keystroke is slow with many rows; wait until typing pauses
    private static readonly TimeSpan FilterDebounce = TimeSpan.FromMilliseconds(300);
    private readonly Subject<Unit> _filterChanged = new();
    private readonly IDisposable _filterSubscription;

    private void ScheduleApplyFilters()
    {
      _filterChanged.OnNext(Unit.Default);
    }

    private void ApplyFilters()
    {
      _entitiesView.Refresh();
      NotifyOfPropertyChange(() => HasFilter);
      NotifyOfPropertyChange(() => FilterSummary);
    }

    private bool FilterEntity(object item)
    {
      if (item is not EntityVm vm)
        return true;

      var hostname = HostnameFilter.Trim();
      if (hostname.Length > 0 && (vm.Entity.Hostname ?? "").IndexOf(hostname, StringComparison.OrdinalIgnoreCase) < 0)
        return false;

      var tagTerms = TagFilter.Split((char[])null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      foreach (var term in tagTerms)
      {
        if (!vm.Entity.Tags.Any(tag => tag.StartsWith(term, StringComparison.OrdinalIgnoreCase)))
          return false;
      }

      return true;
    }



    private CancellationTokenSource _dnsCts;

    private bool _isCheckingDns;
    public bool IsCheckingDns
    {
      get { return _isCheckingDns; }
      private set
      {
        if (Equals(value, _isCheckingDns)) return;
        _isCheckingDns = value;
        NotifyOfPropertyChange();
        NotifyOfPropertyChange(() => CanCheckDns);
      }
    }

    /// <summary>
    /// Looks up the hostnames of all (or the selected) websites without using the
    /// local dns cache and compares the addresses with those of the linked proxy servers.
    /// The form stays enabled; rows fill in as results come in.
    /// </summary>
    public async void CheckDns()
    {
      _dnsCts?.Cancel();
      var cts = _dnsCts = new CancellationTokenSource();
      var ct = cts.Token;
      IsCheckingDns = true;
      try
      {
        var targets = SelectedEntities.Count > 1 ? SelectedEntities.ToList() : Entities.ToList();

        var servers = await Client.ServerGet();
        var checker = new DnsChecker();

        // expected set: addresses of every server hostname, keyed by server name (Website.Servers holds names)
        var serverAddresses = new Dictionary<string, IReadOnlyList<IPAddress>>(StringComparer.OrdinalIgnoreCase);
        await Task.WhenAll(servers.Select(async server =>
        {
          var addresses = await checker.ResolveServerAsync(server.Hostname, ct);
          lock (serverAddresses)
            serverAddresses[server.Name] = addresses;
        }));
        var proxyServerNames = servers
          .Where(s => s.Services.HasFlag(ServerServices.Proxy))
          .Select(s => s.Name)
          .ToList();

        var probeLabel = DnsChecker.NewProbeLabel();
        var pending = new List<EntityVm>();
        foreach (var vm in targets)
        {
          var skipReason = SkipReason(vm.Entity);
          if (skipReason != null)
          {
            vm.DnsV4 = DnsCheckResult.Skipped(AddressFamily.InterNetwork, skipReason);
            vm.DnsV6 = DnsCheckResult.Skipped(AddressFamily.InterNetworkV6, skipReason);
            vm.Dnssec = DnssecCheckResult.Skipped(skipReason);
            continue;
          }
          vm.DnsV4 = DnsCheckResult.Checking(AddressFamily.InterNetwork);
          vm.DnsV6 = DnsCheckResult.Checking(AddressFamily.InterNetworkV6);
          vm.Dnssec = DnssecCheckResult.Checking();
          pending.Add(vm);
        }

        using var semaphore = new SemaphoreSlim(8);
        await Task.WhenAll(pending.Select(async vm =>
        {
          await semaphore.WaitAsync(ct);
          try
          {
            var names = DnsChecker.NamesToCheck(vm.Entity.Hostname, vm.Entity.Scope, probeLabel);
            var serverNames = vm.Entity.Servers.Count > 0 ? vm.Entity.Servers : proxyServerNames;
            var expected = serverNames
              .SelectMany(name => serverAddresses.TryGetValue(name, out var addresses)
                ? addresses.Select(ip => (Server: name, Ip: ip))
                : Enumerable.Empty<(string Server, IPAddress Ip)>())
              .ToList();

            var v4 = checker.CheckAsync(names, vm.Entity.Scope, AddressFamily.InterNetwork, expected, ct);
            var v6 = checker.CheckAsync(names, vm.Entity.Scope, AddressFamily.InterNetworkV6, expected, ct);
            await Task.WhenAll(v4, v6);

            // dnssec verdict comes from the same answers (DO bit is set on every query)
            var dnssec = await checker.EvaluateDnssecAsync(vm.Entity.Hostname, v4.Result.Lookups.Concat(v6.Result.Lookups).ToList(), ct);

            OnUIThread(() =>
            {
              vm.DnsV4 = v4.Result;
              vm.DnsV6 = v6.Result;
              vm.Dnssec = dnssec;
            });
          }
          catch (OperationCanceledException)
          {
            OnUIThread(() =>
            {
              vm.DnsV4 = null;
              vm.DnsV6 = null;
              vm.Dnssec = null;
            });
          }
          catch (Exception exception)
          {
            OnUIThread(() =>
            {
              vm.DnsV4 = DnsCheckResult.Failed(AddressFamily.InterNetwork, exception.Message);
              vm.DnsV6 = DnsCheckResult.Failed(AddressFamily.InterNetworkV6, exception.Message);
              vm.Dnssec = DnssecCheckResult.Failed(exception.Message);
            });
          }
          finally
          {
            semaphore.Release();
          }
        }));
      }
      catch (OperationCanceledException)
      {
        // cancelled by a new run or by closing the tab
      }
      catch (Exception exception)
      {
        App.ShowException("DNS check failed", exception);
      }
      finally
      {
        if (ReferenceEquals(_dnsCts, cts))
        {
          _dnsCts = null;
          IsCheckingDns = false;
        }
        cts.Dispose();
      }
    }

    public bool CanCheckDns
    {
      get { return !IsCheckingDns; }
    }

    private static string SkipReason(Entity entity)
    {
      if (entity.Kind == WebsiteKind.Offline)
        return "website is offline";
      if (string.Equals(DnsChecker.Normalize(entity.Hostname), "localhost", StringComparison.OrdinalIgnoreCase))
        return "localhost";
      if (string.IsNullOrWhiteSpace(entity.Hostname))
        return "no hostname";
      return null;
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
      if (close)
      {
        _dnsCts?.Cancel();
        _filterSubscription.Dispose();
      }
      if (close && AcmeLogs != null)
        await ((IDeactivate)AcmeLogs).DeactivateAsync(true, cancellationToken);
      await base.OnDeactivateAsync(close, cancellationToken);
    }
  }
}
