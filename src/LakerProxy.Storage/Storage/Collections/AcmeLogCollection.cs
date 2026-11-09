using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Reactive;
using Lakerfield.Runtime;
using Lakerfield.Settings;
using Lakerfield.Storage;
using MongoDB.Driver;
using Entity = LakerProxy.Models.AcmeLog;

namespace LakerProxy.Storage.Collections
{
  using static Builders<Entity>;

  public class AcmeLogCollection : ABaseInsertCollection<Entity>
  {
    protected IDataObservable<Entity> ChangesObservable;

    protected override async Task Initialize(IMongoDatabase database, AppServices services)
    {
      await base.Initialize(database, services);

      var exists = await Collection.Exists().ConfigureAwait(false);
      if (!exists)
        await Insert(new Entity()
        {
          Date = DateTime.Now,
          Domain = "",
          Step = "",
          Message = "Initial message",
        }).ConfigureAwait(false);

      var isCapped = await Collection.IsCappedCollection().ConfigureAwait(false);
      if (!isCapped)
        await Collection.ConvertCollectionToCapped();

      await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Entity>(
        IndexKeys
          .Ascending(e => e.Date))).ConfigureAwait(false);

      await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Entity>(
        IndexKeys
          .Ascending(e => e.Domain))).ConfigureAwait(false);

      ChangesObservable = Observable
        .Defer(Collection.CreateChangesObservable)
        .Publish()
        .RefCount()
        .AsDataObservable();
    }

    public Entity New(string domain, string step, string message)
    {
      var info = Services.Settings().Info;

      return new Entity()
      {
        Id = ShortGuid.NewGuid(),
        Date = DateTime.Now,
        Machine = info?.MachineName ?? "unknown",
        Domain = domain ?? "",
        Step = step ?? "",
        Message = message ?? ""
      };
    }

    public async Task<IEnumerable<Entity>> FindBy(Entity.Filter filter)
    {
      var andQuerys = new List<FilterDefinition<Entity>>();
      if (filter != null)
      {
        if (!string.IsNullOrEmpty(filter.Machine))
          andQuerys.Add(Filter.Eq(o => o.Machine, filter.Machine));
        if (!string.IsNullOrEmpty(filter.Domain))
          andQuerys.Add(Filter.Eq(o => o.Domain, filter.Domain));
        if (filter.DateFrom.HasValue)
          andQuerys.Add(Filter.Gte(o => o.Date, filter.DateFrom.Value));
        if (filter.DateTill.HasValue)
          andQuerys.Add(Filter.Lte(o => o.Date, filter.DateTill.Value));
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
