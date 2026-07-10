using System;
using System.Collections.Generic;

namespace LakerProxy.WebApi.Models
{
  public class Website
  {
    public string Id { get; set; } = "";

    public string Hostname { get; set; } = "";

    public bool Fallback { get; set; }
    public string AcmeAccount { get; set; } = "";
    public CertificateScope Scope { get; set; }
    public WebsiteKind Kind { get; set; }

    public string Destination { get; set; } = "";
    public DestinationProtocol DestinationProtocol { get; set; } = DestinationProtocol.Default;

    private List<string> _servers;
    public List<string> Servers
    {
      get => _servers ??= new List<string>();
      set => _servers = value;
    }

    public FilterKind Filter { get; set; }

    private List<string> _filterAcl;
    public List<string> FilterAcl
    {
      get => _filterAcl ??= new List<string>();
      set => _filterAcl = value;
    }
    public string FilterDestination { get; set; } = "";

    public bool ForceRenew { get; set; }

    /// <summary>
    /// Read-only: computed by the server on save as {hostname-prefix}.{server AcmeChallengeZone}.
    /// Target of the _acme-challenge CNAME record for dns-01 validation.
    /// </summary>
    public string AcmeChallengeLabel { get; set; } = "";

    public string MaxRequestBodySize { get; set; } = "";

    public string Comments { get; set; } = "";

    public WebsiteCertificate[]? Certificates { get; set; }


    public Website Clone()
    {
      return (Website)MemberwiseClone();
    }
  }

  public class WebsiteCertificate
  {
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTill { get; set; }

    public CertificateStates State { get; set; }

  }
}
