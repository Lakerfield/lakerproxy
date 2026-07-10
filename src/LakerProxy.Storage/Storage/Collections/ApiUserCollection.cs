using System;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Runtime;
using Lakerfield.Settings;
using Lakerfield.Storage;
using LakerProxy.Service;
using LakerProxy.Settings;
using MongoDB.Driver;
using Entity = LakerProxy.Models.ApiUser;

namespace LakerProxy.Storage.Collections
{
  using static Builders<Entity>;

  public class ApiUserCollection : ABaseObservableCollection<Entity>, IApiUserCollection
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

      await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Entity>(
        IndexKeys
          .Ascending(e => e.ApiKey),
            new CreateIndexOptions()
            {
              Unique = true,
            })).ConfigureAwait(false);

      var count = await Collection.CountDocumentsAsync(Filter.Empty);
      if (count == 0)
      {
        if (settings.InitialAdminToken.IsNullOrEmpty())
          throw new OperationAbortedException("Cannot create a initial admin user without a InitialAdminToken in the config");

        var user = new Entity()
        {
          Name = "Default Admin",
          Hostname = "",
          Rights = ApiUserRights.RoleAdministrator,
          ApiKey = settings.InitialAdminToken,
          AcmeAccount = (await Services.AcmeAccount().FindDefault())?.Id ?? ShortGuid.Empty,
          Comments = "auto created",
        };
        await Save(user, Context.Empty);
      }
    }

    public Task Save(Entity entity, Context context, IAppTransaction? transaction = default)
    {
      entity.Name = entity.Name.ToLower().Trim();
      entity.Hostname = entity.Hostname.ToLower().Trim();
      entity.ApiKey = entity.ApiKey.Trim();
      if (entity.ApiKey.Length < 20)
        throw new ArgumentOutOfRangeException(nameof(entity), "ApiKey must be at least 20 long");

      return Collection.Save(entity, context, transaction);
    }

    public Task Delete(Entity entity, Context context, IAppTransaction? transaction = default)
    {
      return Collection.Delete(entity, context, transaction);
    }

    public async Task<Entity?> Login(string apiKey)
    {
      if (string.IsNullOrWhiteSpace(apiKey))
        return null;

      apiKey = apiKey.Trim();
      if (apiKey.Length < 20)
        return null;

      var apiUser = await Collection.Find(Filter.Eq(e => e.ApiKey, apiKey)).FirstOrDefaultAsync();
      if (apiUser == null)
        return null;

      if (apiUser.Rights.HasFlag(ApiUserRights.Login))
        return apiUser;

      return null;
    }
  }
}
