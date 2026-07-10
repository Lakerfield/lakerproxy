using System.Threading.Tasks;
using Lakerfield.Runtime;
using Entity = LakerProxy.Models.ApiUser;
using WebEntity = LakerProxy.WebApi.Models.ApiUser;

namespace LakerProxy.ProxyApp.ApiExtensions
{
  public static class ApiUserConvert
  {

    public static async Task<WebEntity> ToWeb(this Entity entity, AppServices services)
    {
      if (entity == null)
        return null;

      var result = new WebEntity()
      {
        Id = entity.Id,
        Name = entity.Name,
        Hostname = entity.Hostname,
        ApiKey = "",
        Server = entity.PrimaryServer,
        Rights = (LakerProxy.WebApi.ApiUserRights)entity.Rights,
        Comments = entity.Comments,
      };

      var name = (await services.Server().GetById(entity.PrimaryServer))?.Name;
      if (!string.IsNullOrWhiteSpace(name))
        result.Server = name;

      result.Website = (await services.Website().GetById(entity.Website))?.Hostname ?? "";

      result.AcmeAccount = (await services.AcmeAccount().GetById(entity.AcmeAccount))?.Code ?? "";

      return result;
    }

  }
}
