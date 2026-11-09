using System;
using System.Collections.Generic;
using Lakerfield;

namespace LakerProxy.Models
{
  public class Server : IId
  {
    public ShortGuid Id { get; set; }

    public string Name { get; set; }
    public string Hostname { get; set; }
    public string AcmeChallengeZone { get; set; }

    public ShortGuid AcmeAccount { get; set; }
    public ServerServices Services { get; set; }

    public string Comments { get; set; }

    private List<CertificateData> _certificates;
    public List<CertificateData> Certificates
    {
      get => _certificates ??= new List<CertificateData>();
      set => _certificates = value;
    }

    public ShortGuid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public ShortGuid ModifiedBy { get; set; }
    public DateTime ModifiedAt { get; set; }



    public class CertificateData
    {
      public DateTime ValidFrom { get; set; }
      public DateTime ValidTill { get; set; }

      public CertificateStates State { get; set; }

      public byte[] CertificatePfx { get; set; }
    }

  }
}
