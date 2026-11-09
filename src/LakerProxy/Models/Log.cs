using System;
using System.Collections.Generic;
using Lakerfield;

namespace LakerProxy.Models
{
  public class Log : IId, IFloriAppRootLog
  {
    public ShortGuid Id { get; set; }

    public ShortGuid Owner { get; set; }
    public DateTime Date { get; set; }

    public string Machine { get; set; }
    public int ProcessId { get; set; }
    public string Application { get; set; }
    public FloriAppLogLevel LogLevel { get; set; }

    public string Message { get; set; }

    private Dictionary<string, Dictionary<string, object>> _values;
    public Dictionary<string, Dictionary<string, object>> Values
    {
      get => _values ?? (_values = new Dictionary<string, Dictionary<string, object>>());
      set => _values = value;
    }

    private IFloriAppLog _defaultLog;
    private IFloriAppLog GetDefaultLog() => _defaultLog ?? (_defaultLog = Add("default"));

    public void UpgradeLogLevel(FloriAppLogLevel level)
    {
      if (LogLevel < level)
        LogLevel = level;
    }

    public object Get(string key)
    {
      return GetDefaultLog().Get(key);
    }

    public void Set(string key, object value)
    {
      GetDefaultLog().Set(key, value);
    }

    public void SetOwner(ShortGuid owner)
    {
      Owner = owner;
    }

    public IFloriAppLog Add(string key)
    {
      var values = Values;
      var count = 1;
      while (true)
      {
        var name = $"{key}{count}";
        if (!values.ContainsKey(name))
        {
          var dictionary = new Dictionary<string, object>();
          values.Add(name, dictionary);
          return new ScopedFloriAppLog(this, dictionary);
        }
        count++;
      }
    }



    public class Filter : IFilter<Log>
    {
      public ShortGuid? Owner { get; set; }
      public FloriAppLogLevel? LogLevel { get; set; }
      public DateTime? DateFrom { get; set; }
      public DateTime? DateTill { get; set; }

      public bool IsMatch(Log entity)
      {
        var result = true;

        if (Owner.HasValue)
          result &= entity.Owner == Owner.Value;
        if (LogLevel.HasValue)
          result &= entity.LogLevel == LogLevel.Value;
        if (DateFrom.HasValue)
          result &= entity.Date >= DateFrom.Value;
        if (DateTill.HasValue)
          result &= entity.Date <= DateTill.Value;

        return result;
      }
    }



    internal class ScopedFloriAppLog : IFloriAppLog
    {
      private readonly IFloriAppLog _log;
      private readonly Dictionary<string, object> _dictionary;

      internal ScopedFloriAppLog(IFloriAppLog log, Dictionary<string, object> dictionary)
      {
        _log = log;
        _dictionary = dictionary;
      }

      public void UpgradeLogLevel(FloriAppLogLevel level)
      {
        _log.UpgradeLogLevel(level);
      }

      public object Get(string key)
      {
        if (_dictionary.TryGetValue(key, out var result))
          return result;
        return null;
      }

      public void Set(string key, object value)
      {
        var values = _dictionary;

        if (values.ContainsKey(key))
          values[key] = value;
        else
          values.Add(key, value);
      }

      public IFloriAppLog Add(string key)
      {
        return _log.Add(key);
      }
    }


  }
}
