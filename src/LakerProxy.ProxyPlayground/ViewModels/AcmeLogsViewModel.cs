using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using LakerProxy.ProxyPlayground.Settings;
using LakerProxy.WebApi;
using Entity = LakerProxy.WebApi.Models.AcmeLogEntry;

namespace LakerProxy.ProxyPlayground.ViewModels
{
  public class AcmeLogsViewModel : BaseTabViewModel<ProxyServer>
  {
    private const int MaxEntries = 2000;
    private const int StreamHistory = 100;

    public override string DisplayName
    {
      get { return $"Acme logs on {Settings.Omschrijving}"; }
      set { }
    }

    public new ProxyServer Settings => (ProxyServer)base.Settings;

    public LakerProxyApiClient Client { get; private set; }

    public ObservableCollection<Entity> Entries { get; }
    private readonly ICollectionView _entriesView;

    private string _domainFilter = "";
    public string DomainFilter
    {
      get { return _domainFilter; }
      set
      {
        if (Equals(value, _domainFilter)) return;
        _domainFilter = value;
        NotifyOfPropertyChange();
        _entriesView.Refresh();
      }
    }

    private bool _paused;
    public bool Paused
    {
      get { return _paused; }
      set
      {
        if (Equals(value, _paused)) return;
        _paused = value;
        NotifyOfPropertyChange();
        if (!value)
          FlushPausedBuffer();
      }
    }

    private string _connectionStatus = "";
    public string ConnectionStatus
    {
      get { return _connectionStatus; }
      set
      {
        if (Equals(value, _connectionStatus)) return;
        _connectionStatus = value;
        NotifyOfPropertyChange();
      }
    }

    private readonly List<Entity> _pausedBuffer = new List<Entity>();
    private readonly HashSet<string> _seenIds = new HashSet<string>();
    private readonly Queue<string> _seenIdsOrder = new Queue<string>();
    private CancellationTokenSource _cts;


    public AcmeLogsViewModel(ProxyServer settings) : base(settings)
    {
      Entries = new ObservableCollection<Entity>();
      _entriesView = CollectionViewSource.GetDefaultView(Entries);
      _entriesView.Filter = o =>
        string.IsNullOrWhiteSpace(DomainFilter) ||
        ((Entity)o).Domain.Contains(DomainFilter.Trim(), StringComparison.OrdinalIgnoreCase);

      Client = new LakerProxyApiClient(new LakerProxyApiOptions()
      {
        ApiBaseUrl = settings.Url,
        ApiKey = settings.Username + settings.Password,
      });
    }


    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
      if (_cts == null)
      {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _ = Task.Run(() => ReadLoop(ct), ct);
      }
      return base.OnActivateAsync(cancellationToken);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
      if (close)
      {
        _cts?.Cancel();
        _cts = null;
      }
      await base.OnDeactivateAsync(close, cancellationToken);
    }


    private async Task ReadLoop(CancellationToken ct)
    {
      var backoff = TimeSpan.FromSeconds(1);
      while (!ct.IsCancellationRequested)
      {
        try
        {
          ConnectionStatus = "Connecting...";
          await foreach (var entry in Client.AcmeLogStream(StreamHistory, null, ct))
          {
            backoff = TimeSpan.FromSeconds(1);
            ConnectionStatus = "Live";
            var local = entry;
            OnUIThread(() => Append(local));
          }
          ConnectionStatus = "Stream closed by server, reconnecting...";
        }
        catch (OperationCanceledException)
        {
          break;
        }
        catch (Exception exception)
        {
          ConnectionStatus = $"Connection lost: {exception.Message} (retry in {backoff.TotalSeconds:0}s)";
        }

        try
        {
          await Task.Delay(backoff, ct);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
      }
      ConnectionStatus = "Stopped";
    }


    private void Append(Entity entry)
    {
      // dedup: history resend after a reconnect delivers ids we already have
      if (!string.IsNullOrEmpty(entry.Id))
      {
        if (!_seenIds.Add(entry.Id))
          return;
        _seenIdsOrder.Enqueue(entry.Id);
        while (_seenIdsOrder.Count > MaxEntries * 2)
          _seenIds.Remove(_seenIdsOrder.Dequeue());
      }

      if (Paused)
      {
        _pausedBuffer.Add(entry);
        return;
      }

      // newest on top
      Entries.Insert(0, entry);
      TrimEntries();
    }

    private void FlushPausedBuffer()
    {
      foreach (var entry in _pausedBuffer)
        Entries.Insert(0, entry);
      _pausedBuffer.Clear();
      TrimEntries();
    }

    private void TrimEntries()
    {
      while (Entries.Count > MaxEntries)
        Entries.RemoveAt(Entries.Count - 1);
    }

    public void Clear()
    {
      Entries.Clear();
      _pausedBuffer.Clear();
    }

  }
}
