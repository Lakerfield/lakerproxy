using Entity = LakerProxy.Models.Log;
using WebEntity = LakerProxy.WebApi.Models.LogEntry;

namespace LakerProxy.ProxyApp.ApiExtensions
{
  public static class LogConvert
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
        Application = entity.Application ?? "",
        ProcessId = entity.ProcessId,
        LogLevel = entity.LogLevel.ToString(),
        Message = entity.Message ?? "",
        Owner = entity.Owner,
      };
    }

  }
}
