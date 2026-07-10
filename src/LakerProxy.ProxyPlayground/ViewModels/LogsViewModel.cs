using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using LakerProxy.ProxyPlayground.Settings;
using LakerProxy.WebApi;
using Entity = LakerProxy.WebApi.Models.LogEntry;
using EntityVm = LakerProxy.ProxyPlayground.ViewModels.LogEntryViewModel;

namespace LakerProxy.ProxyPlayground.ViewModels
{
  public class LogsViewModel : BaseTabViewModel<ProxyServer>
  {
    private const int MaxEntries = 2000;
    private const int StreamHistory = 100;

    public override string DisplayName
    {
      get { return $"Logs on {Settings.Omschrijving}"; }
      set { }
    }

    public new ProxyServer Settings => (ProxyServer)base.Settings;

    public LakerProxyApiClient Client { get; private set; }

    public ObservableCollection<EntityVm> Entries { get; }
    private readonly ICollectionView _entriesView;

    public string[] Levels { get; } = { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" };

    private string _minimumLevel = "Trace";
    public string MinimumLevel
    {
      get { return _minimumLevel; }
      set
      {
        if (Equals(value, _minimumLevel)) return;
        _minimumLevel = value;
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

    private readonly List<EntityVm> _pausedBuffer = new List<EntityVm>();
    private readonly HashSet<string> _seenIds = new HashSet<string>();
    private readonly Queue<string> _seenIdsOrder = new Queue<string>();
    private CancellationTokenSource _cts;


    public LogsViewModel(ProxyServer settings) : base(settings)
    {
      Entries = new ObservableCollection<EntityVm>();
      _entriesView = CollectionViewSource.GetDefaultView(Entries);
      _entriesView.Filter = o => LevelValue(((EntityVm)o).Entity.LogLevel) >= LevelValue(MinimumLevel);

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
          await foreach (var entry in Client.LogStream(StreamHistory, ct))
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

      var vm = new EntityVm(entry);
      if (Paused)
      {
        _pausedBuffer.Add(vm);
        return;
      }

      // newest on top
      Entries.Insert(0, vm);
      TrimEntries();
    }

    private void FlushPausedBuffer()
    {
      foreach (var vm in _pausedBuffer)
        Entries.Insert(0, vm);
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


    private static int LevelValue(string level)
    {
      switch (level)
      {
        case "Trace": return 0;
        case "Debug": return 1;
        case "Info": return 2;
        case "Warn": return 3;
        case "Error": return 4;
        case "Fatal": return 5;
        default: return 2;
      }
    }

  }
}
