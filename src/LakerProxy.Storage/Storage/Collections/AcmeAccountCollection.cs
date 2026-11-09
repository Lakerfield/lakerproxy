using System;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Runtime;
using Lakerfield.Settings;
using Lakerfield.Storage;
using LakerProxy.Extensions;
using LakerProxy.Service;
using LakerProxy.Settings;
using MongoDB.Driver;
using Entity = LakerProxy.Models.AcmeAccount;

namespace LakerProxy.Storage.Collections
{
  using static Builders<Entity>;

  public class AcmeAccountCollection : ABaseObservableCollection<Entity>, IAcmeAccountCollection
  {
    protected override async Task Initialize(IMongoDatabase database, AppServices services)
    {
      await base.Initialize(database, services);

      var settings = services.Settings().LakerProxy();
      if (string.IsNullOrWhiteSpace(settings.AcmeMail) || !settings.AcmeMail.Contains('@'))
        throw new Exception($"Invalid acme mail configured {settings.AcmeMail}");

      await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Entity>(
        IndexKeys
          .Ascending(e => e.Default),
            new CreateIndexOptions()
            {
              Unique = true,
              Sparse = true,
            })).ConfigureAwait(false);

      if (await Collection.CountDocumentsAsync(FilterDefinition<Entity>.Empty) == 0)
      {
        var entity = new Entity()
        {
          Code = "test",
          Name = $"LetsEncrypt Staging {settings.AcmeMail}",
          Type = AcmeAccountTypes.LetsEncryptStaging,
          Mail = settings.AcmeMail,
          Default = true,
          CertificateCountryName = settings.AcmeCountry.IsNullOrWhiteSpaceThenThrow(),
          CertificateState = settings.AcmeState.IsNullOrWhiteSpaceThenThrow(),
          CertificateLocality = settings.AcmeLocality.IsNullOrWhiteSpaceThenThrow(),
          CertificateOrganization = settings.AcmeOrganization.IsNullOrWhiteSpaceThenThrow(),
          CertificateOrganizationUnit = settings.AcmeOrganizationUnit.IsNullOrWhiteSpaceThenThrow(),
        };
        await Collection.SaveAsync(null, entity).ConfigureAwait(false);

        entity = new Entity()
        {
          Code = "live",
          Name = $"LetsEncrypt {settings.AcmeMail}",
          Type = AcmeAccountTypes.LetsEncrypt,
          Mail = settings.AcmeMail,
          //PreferredRootCertificate = "ISRG Root X1",
          Default = false,
          CertificateCountryName = settings.AcmeCountry.IsNullOrWhiteSpaceThenThrow(),
          CertificateState = settings.AcmeState.IsNullOrWhiteSpaceThenThrow(),
          CertificateLocality = settings.AcmeLocality.IsNullOrWhiteSpaceThenThrow(),
          CertificateOrganization = settings.AcmeOrganization.IsNullOrWhiteSpaceThenThrow(),
          CertificateOrganizationUnit = settings.AcmeOrganizationUnit.IsNullOrWhiteSpaceThenThrow(),
        };
        await Collection.SaveAsync(null, entity).ConfigureAwait(false);
      }

    }

    public Task<Entity?> FindDefault()
    {
      return Collection.Find(Filter.Eq(e => e.Default, true)).FirstOrDefaultAsync();
    }

    public Task<Entity?> Find(string code)
    {
      code = code.ToLower();
      return Collection.Find(Filter.Eq(e => e.Code, code)).FirstOrDefaultAsync();
    }


    public Task Save(Entity entity, Context context, IAppTransaction? transaction = default)
    {
      entity.Code = entity.Code.Trim().ToLower();
      return Collection.Save(entity, context, transaction);
    }
  }
}
