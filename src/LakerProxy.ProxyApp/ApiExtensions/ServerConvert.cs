using System.Threading.Tasks;
using Lakerfield.Runtime;
using Entity = LakerProxy.Models.Server;
using WebEntity = LakerProxy.WebApi.Models.Server;

namespace LakerProxy.ProxyApp.ApiExtensions
{
  public static class ServerConvert
  {

    public static async Task<WebEntity> ToWeb(this Entity entity, AppServices services)
    {
      if (entity == null)
        return null;

      var result = new WebEntity()
      {
        Id = entity.Id,
        Name = entity.Name ?? "",
        Hostname = entity.Hostname ?? "",
        AcmeChallengeZone = entity.AcmeChallengeZone ?? "",
        Services = (LakerProxy.WebApi.ServerServices)entity.Services,
        Comments = entity.Comments ?? "",
      };

      result.AcmeAccount = (await services.AcmeAccount().GetById(entity.AcmeAccount))?.Code ?? "";

      return result;
    }

  }
}
