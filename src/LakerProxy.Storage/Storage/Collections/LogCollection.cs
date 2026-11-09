using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Reactive;
using Lakerfield.Runtime;
using Lakerfield.Settings;
using Lakerfield.Storage;
using LakerProxy.Service;
using MongoDB.Driver;
using Entity = LakerProxy.Models.Log;

namespace LakerProxy.Storage.Collections
{
  using static Builders<Entity>;

  public class LogCollection : ABaseInsertCollection<Entity>, ILogCollection
  {
    protected IDataObservable<Entity> ChangesObservable;

    protected override async Task Initialize(IMongoDatabase database, AppServices services)
    {
      await base.Initialize(database, services);

      var exists = await Collection.Exists().ConfigureAwait(false);
      if (!exists)
        await Insert(new Entity()
        {
          Owner = ShortGuid.Empty,
          Date = DateTime.Now,
          Machine = "",
          Application = "",
          LogLevel = FloriAppLogLevel.Warn,
          Message = "Initial message",
        }).ConfigureAwait(false);

      var isCapped = await Collection.IsCappedCollection().ConfigureAwait(false);
      if (!isCapped)
        await Collection.ConvertCollectionToCapped();

      await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Entity>(
        IndexKeys
          .Ascending(e => e.Owner))).ConfigureAwait(false);


      ChangesObservable = Observable
        .Defer(Collection.CreateChangesObservable)
        .Publish()
        .RefCount()
        .AsDataObservable();
    }

    public Entity New(string? message)
    {
      var info = Services.Settings().Info;

      return new Entity()
      {
        Id = ShortGuid.NewGuid(),
        Date = DateTime.Now,
        LogLevel = FloriAppLogLevel.Trace,
        Machine = info?.MachineName ?? "unknown",
        Application = info?.ApplicationName ?? "unknown",
        ProcessId = info?.ProcessId ?? 0,
        Message = message ?? "unknown"
      };
    }

    public async Task<IEnumerable<Entity>> FindBy(Entity.Filter filter)
    {
      var andQuerys = new List<FilterDefinition<Entity>>();
      if (filter != null)
      {
        if (filter.Owner.HasValue)
          andQuerys.Add(Filter.Eq(o => o.Owner, filter.Owner.Value));
        if (filter.DateFrom.HasValue)
          andQuerys.Add(Filter.Gte(o => o.Date, filter.DateFrom.Value));
        if (filter.DateTill.HasValue)
          andQuerys.Add(Filter.Lte(o => o.Date, filter.DateTill.Value));
        if (filter.LogLevel.HasValue)
          andQuerys.Add(Filter.Gte(o => o.LogLevel, filter.LogLevel.Value));
      }

      if (andQuerys.Count > 0)
        return await Collection.Find(Filter.And(andQuerys)).ToListAsync().ConfigureAwait(false);
      else
        return new Entity[] { };
    }

    public IDataObservable<Entity> Observe(Entity.Filter filter)
    {
      return Observable.Concat(
        ObservableMarkers<Entity>.Reset(DataResetKind.StartFromDb),
        ObservableMarkers<Entity>.Live(),
        ChangesObservable
          .Where(change => change.MatchFilter(filter)))
          .AsDataObservable();
    }


  }
}
