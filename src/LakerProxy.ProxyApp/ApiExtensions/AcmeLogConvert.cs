using Entity = LakerProxy.Models.AcmeLog;
using WebEntity = LakerProxy.WebApi.Models.AcmeLogEntry;

namespace LakerProxy.ProxyApp.ApiExtensions
{
  public static class AcmeLogConvert
  {

    public static WebEntity ToWeb(this Entity entity)
    {
      if (entity == null)
        return null;

      return new WebEntity()
      {
        Id = entity.Id,
        Date = entity.Date,
        Machine = entity.Machine ?? "",
        Domain = entity.Domain ?? "",
        Step = entity.Step ?? "",
        Message = entity.Message ?? "",
      };
    }

  }
}
