using System;
using Lakerfield;

namespace LakerProxy.Models
{
  public class ApiUser : IId, ICreatedModified
  {
    public ShortGuid Id { get; set; }

    public string Name { get; set; }
    public ApiUserRights Rights { get; set; }

    public string Hostname { get; set; }
    public ShortGuid PrimaryServer { get; set; }

    public string ApiKey { get; set; }

    public ShortGuid AcmeAccount { get; set; }

    public string Comments { get; set; }

    public ShortGuid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public ShortGuid ModifiedBy { get; set; }
    public DateTime ModifiedAt { get; set; }

  }
}
