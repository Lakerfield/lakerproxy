using System;

namespace LakerProxy.WebApi.Models
{
  public class Certificate
  {
    public string Id { get; set; } = "";

    public string Hostname { get; set; } = "";

    public DateTime ValidFrom { get; set; }
    public DateTime ValidTill { get; set; }

    public CertificateStates State { get; set; }

    public string PemCertificate { get; set; } = "";
    public string PemCertificateKey { get; set; } = "";

    public byte[] PfxCertificate { get; set; } = {};

  }
}
