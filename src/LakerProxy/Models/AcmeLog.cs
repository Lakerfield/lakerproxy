using System;
using Lakerfield;

namespace LakerProxy.Models
{
  public class AcmeLog : IId
  {
    public ShortGuid Id { get; set; }
    public DateTime Date { get; set; }
    public string Machine { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Step { get; set; } = "";
    public string Message { get; set; } = "";

    public class Filter : IFilter<AcmeLog>
    {
      public string? Domain { get; set; }
      public string? Machine { get; set; }
      public DateTime? DateFrom { get; set; }
      public DateTime? DateTill { get; set; }

      public bool IsMatch(AcmeLog entity)
      {
        var result = true;

        if (!string.IsNullOrEmpty(Domain))
          result &= entity.Domain == Domain;
        if (!string.IsNullOrEmpty(Machine))
          result &= entity.Machine == Machine;
        if (DateFrom.HasValue)
          result &= entity.Date >= DateFrom.Value;
        if (DateTill.HasValue)
          result &= entity.Date <= DateTill.Value;

        return result;
      }
    }
  }
}
