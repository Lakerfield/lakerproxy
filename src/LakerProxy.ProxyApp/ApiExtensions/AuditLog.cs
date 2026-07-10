using System.Net;
using System.Threading.Tasks;
using Lakerfield.Runtime;
using LakerProxy.Models;

namespace LakerProxy.ProxyApp.ApiExtensions
{
  /// <summary>
  /// Writes audit entries (who did what from where) for mutations to the Log
  /// collection. Best effort: a failing audit write never fails the operation.
  /// </summary>
  public static class AuditLog
  {
    public static async Task Write(AppServices services, ApiUser user, IPAddress remoteIp, string message)
    {
      try
      {
        var log = services.Log().New($"[audit] {message}");
        log.UpgradeLogLevel(FloriAppLogLevel.Info);

        if (user != null)
        {
          log.SetOwner(user.Id);
          log.Set("user", user.Name);
        }

        var ip = remoteIp?.ToString().Replace("::ffff:", "");
        if (!string.IsNullOrWhiteSpace(ip))
          log.Set("ip", ip);

        await services.Log().Insert(log);
      }
      catch
      {
        // audit logging is best effort and must never break the operation
      }
    }
  }
}
