using System;
using System.Linq;
using System.Threading.Tasks;
using Lakerfield.Runtime;
using Entity = LakerProxy.Models.Website;
using WebEntity = LakerProxy.WebApi.Models.Website;
using CertificateEntity = LakerProxy.Models.Website.CertificateData;
using CertificateWebEntity = LakerProxy.WebApi.Models.WebsiteCertificate;

namespace LakerProxy.ProxyApp.ApiExtensions
{
  public static class WebsiteConvert
  {

    public static async Task<WebEntity> ToWeb(this Entity entity, AppServices services)
    {
      if (entity == null)
        return null;

      var result = new WebEntity()
      {
        Id = entity.Id,
        Hostname = entity.Hostname,
        Fallback = entity.Fallback,
        ForceRenew = entity.ForceRenew,
        AcmeChallengeLabel = entity.AcmeChallengeLabel ?? "",
        Scope = (LakerProxy.WebApi.CertificateScope)entity.Scope,
        Kind = (LakerProxy.WebApi.WebsiteKind)entity.Kind,
        Destination = entity.Destination,
        DestinationProtocol = (LakerProxy.WebApi.DestinationProtocol)entity.DestinationProtocol,
        Filter = (LakerProxy.WebApi.FilterKind)entity.Filter,
        FilterAcl = entity.FilterAcl,
        FilterDestination = entity.FilterDestination,
        Comments = entity.Comments,
        Certificates = entity.Certificates.Select(ToWeb).ToArray()
      };

      foreach (var server in entity.Servers)
      {
        var name = (await services.Server().GetById(server))?.Name;
        if (!string.IsNullOrWhiteSpace(name))
          result.Servers.Add(name);
      }

      result.AcmeAccount = (await services.AcmeAccount().GetById(entity.AcmeAccount))?.Code ?? "";

      result.MaxRequestBodySize = ConvertToMaxRequestBodySizeString(entity.MaxRequestBodySize);

      return result;
    }

    public static CertificateWebEntity ToWeb(this CertificateEntity entity)
    {
      return new CertificateWebEntity()
      {
        ValidFrom = entity.ValidFrom,
        ValidTill = entity.ValidTill,
        State = (LakerProxy.WebApi.CertificateStates)entity.State,
      };
    }

    public const string Default = "default";
    public const string Max = "max";
    public const string Unlimited = "unlimited";
    private const long MB = 1024 * 1024;
    private const long KB = 1024;
    public const long DefaultValue = 0;
    public const long MaxValue = -1;
    public const long UnlimitedValue = -2;

    public static string ConvertToMaxRequestBodySizeString(long bytes)
    {
      if (bytes == UnlimitedValue)
        return Unlimited;

      if (bytes == MaxValue || bytes >= Program.MaxRequestBodySize)
        return Max;

      if (bytes == DefaultValue || bytes == Program.DefaultRequestBodySize || bytes < UnlimitedValue)
        return Default;

      if (bytes % MB == 0)
        return $"{bytes / MB} MB";

      if (bytes % KB == 0)
        return $"{bytes / KB} KB";

      return $"{bytes}";
    }

    public static long ConvertToMaxRequestBodySize(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return DefaultValue;

      value = value.Trim();

      if (value.Equals(Default, StringComparison.OrdinalIgnoreCase))
        return DefaultValue;

      if (value.Equals(Max, StringComparison.OrdinalIgnoreCase))
        return MaxValue;

      if (value.Equals(Unlimited, StringComparison.OrdinalIgnoreCase))
        return UnlimitedValue;

      if (value.EndsWith("MB", StringComparison.OrdinalIgnoreCase) && long.TryParse(value[..^2].Trim(), out var mb))
        return mb * MB;

      if (value.EndsWith("KB", StringComparison.OrdinalIgnoreCase) && long.TryParse(value[..^2].Trim(), out var kb))
        return kb * KB;

      if (long.TryParse(value, out var bytes))
        if (bytes > 0 && bytes < Program.MaxRequestBodySize)
          return bytes;

      throw new ArgumentException($"Invalid MaxRequestBodySize value: '{value}'. Use a format like '100KB', '10MB', 'default', or 'max'.");
    }
  }
}
