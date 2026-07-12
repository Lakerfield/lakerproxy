using System;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Runtime;
using Lakerfield.Settings;
using Lakerfield.Storage;
using LakerProxy.Service;
using LakerProxy.Settings;
using MongoDB.Driver;
using Entity = LakerProxy.Models.Server;

namespace LakerProxy.Storage.Collections
{
  using static Builders<Entity>;

  public class ServerCollection : ABaseObservableCollection<Entity>, IServerCollection
  {
    protected override async Task Initialize(IMongoDatabase database, AppServices services)
    {
      await base.Initialize(database, services);

      var settings = services.Settings().LakerProxy();

      await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Entity>(
        IndexKeys
          .Ascending(e => e.Name),
            new CreateIndexOptions()
            {
              Unique = true,
            })).ConfigureAwait(false);
    }

    public Task<Entity> Find(string name)
    {
      name = name.ToLower().Trim();
      return Collection.Find(Filter.Eq(e => e.Name, name)).FirstOrDefaultAsync();
    }

    public async Task<Entity> FindOrCreate(string name, ServerServices services = ServerServices.None)
    {
      name = name.ToLower().Trim();
      var result = await Collection.Find(Filter.Eq(e => e.Name, name)).FirstOrDefaultAsync();
      if (result == null)
      {
        result = new Entity()
        {
          Name = name,
          Hostname = name,
          AcmeAccount = (await Services.AcmeAccount().FindDefault())?.Id ?? ShortGuid.Empty,
          AcmeChallengeZone = $"acme.{name}",
          Comments = "auto created on launch",
          Services = services,
          CreatedAt = DateTime.Now,
        };
        await Save(result, Context.Empty);
      }
      return result;
    }

    public Task Save(Entity entity, Context context, IAppTransaction? transaction = default)
    {
      entity.Name = entity.Name.ToLower().Trim();
      entity.Hostname = entity.Hostname.ToLower().Trim();
      entity.AcmeChallengeZone = entity.AcmeChallengeZone.ToLower().Trim();

      return Collection.Save(entity, context, transaction);
    }
  }
}
