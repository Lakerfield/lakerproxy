using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Storage;
using LakerProxy.Service;
using MongoDB.Driver;
using Entity = LakerProxy.Models.WebsiteData;

namespace LakerProxy.Storage.Collections
{
  using static Builders<Entity>;

  public class WebsiteDataCollection : ABaseCollection<Entity>, IWebsiteDataCollection
  {

    public virtual Task<Entity?> GetLatest()
    {
      return Collection
        .Find(Filter.Empty)
        .SortByDescending(e => e.Date)
        .Limit(1)
        .FirstOrDefaultAsync();
    }

    public Task Save(Entity entity, Context context, IAppTransaction? transaction = default)
    {
      return Collection.Save(entity, context, transaction);
    }

    public async Task DeleteOldest()
    {
      var latest = await GetLatest();
      if (latest == null)
        return;

      await Collection.DeleteManyAsync(Filter.Ne(e => e.Id, latest.Id));
    }
  }
}
