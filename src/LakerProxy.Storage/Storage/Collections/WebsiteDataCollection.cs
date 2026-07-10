using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Storage;
using LakerProxy.Service;
using MongoDB.Driver;
using Entity = LakerProxy.Models.WebsiteData;

namespace LakerProxy.Storage.Collections
{
  using static Builders<Entity>;

  public class WebsiteDataCollection : ABaseCollection<Entity>
  {

    public Task Save(Entity entity, Context context, IAppTransaction? transaction = default)
    {
      return Collection.Save(entity, context, transaction);
    }

    public Task Delete(Entity entity, Context context, IAppTransaction? transaction = default)
    {
      return Collection.Delete(entity, context, transaction);
    }
  }
}
