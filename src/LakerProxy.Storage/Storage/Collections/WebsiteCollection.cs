using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Acme;
using Lakerfield.Runtime;
using Lakerfield.Settings;
using Lakerfield.Storage;
using LakerProxy.Service;
using LakerProxy.Settings;
using MongoDB.Driver;
using Entity = LakerProxy.Models.Website;

namespace LakerProxy.Storage.Collections
{
  using static Builders<Entity>;

  public class WebsiteCollection : ABaseObservableCollection<Entity>, IWebsiteCollection
  {
    protected override async Task Initialize(IMongoDatabase database, AppServices services)
    {
      await base.Initialize(database, services);

      var settings = services.Settings().LakerProxy();

      await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Entity>(
        IndexKeys
          .Ascending(e => e.Hostname),
            new CreateIndexOptions()
            {
              Unique = true,
            })).ConfigureAwait(false);

      if (await Collection.CountDocumentsAsync(FilterDefinition<Entity>.Empty) == 0)
      {
        var entity = new Entity()
        {
          Hostname = settings.InitialHostname,
          Comments = "created at startup",
          Kind = WebsiteKind.ProxyApi,
          Scope = settings.InitialHostnameWithCertificate ? CertificateScope.SingleDomain : CertificateScope.None,
          AcmeAccount = (await Services.AcmeAccount().FindDefault())?.Id ?? ShortGuid.Empty,
          Servers = new List<ShortGuid>()
          {
            (await Services.Server().FindOrCreate(Environment.MachineName, ServerServices.Proxy | ServerServices.LetsEncrypt)).Id
          },
        };
        await Collection.SaveAsync(null, entity).ConfigureAwait(false);
      }

    }

    public Task<Entity?> FindByHostname(string hostname)
    {
      hostname = hostname.ToLower().Trim();
      return Collection.Find(Filter.Eq(e => e.Hostname, hostname)).FirstOrDefaultAsync();
    }

    public async Task Save(Entity entity, Context context, IAppTransaction? transaction = default)
    {
      entity.Hostname = entity.Hostname.ToLower().Trim();
      entity.PrimaryServer = entity.Servers.FirstOrDefault();

      var server = await Services.Server().GetById(entity.PrimaryServer);
      if (server != null)
        entity.AcmeChallengeLabel = $"{AcmeDnsNameHelper.DomainToPrefix(entity.Hostname)}.{server.AcmeChallengeZone.Trim('.')}";

      await Collection.Save(entity, context, transaction);
    }

    public Task Delete(Entity entity, Context context, IAppTransaction? transaction = default)
    {
      return Collection.Delete(entity, context, transaction);
    }
  }
}
