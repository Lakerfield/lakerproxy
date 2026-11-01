using System;
using System.Collections.Generic;
using Lakerfield;

namespace LakerProxy.Models
{
  public class Website : IId, ICreatedModified
  {
    public ShortGuid Id { get; set; }

    public string Hostname { get; set; } = "";

    public ShortGuid AcmeAccount { get; set; }
    public CertificateScope Scope { get; set; }
    public AcmeChallengeType Challenges { get; set; } = AcmeChallengeType.Auto;
    public string AcmeProfile { get; set; } = "";
    public WebsiteKind Kind { get; set; }

    public string Destination { get; set; } = "";
    public DestinationProtocol DestinationProtocol { get; set; } = DestinationProtocol.Default;
    public ShortGuid WebsiteData { get; set; }

    public ShortGuid PrimaryServer { get; set; }
    private List<ShortGuid>? _servers;
    public List<ShortGuid> Servers
    {
      get => _servers ??= new List<ShortGuid>();
      set => _servers = value;
    }

    public string AcmeChallengeLabel { get; set; } = "";

    public FilterKind Filter { get; set; } = FilterKind.None;

    private List<string>? _filterAcl;
    public List<string> FilterAcl
    {
      get => _filterAcl ??= new List<string>();
      set => _filterAcl = value;
    }
    public string FilterDestination { get; set; } = "";


    public bool ForceRenew { get; set; }

    public long MaxRequestBodySize { get; set; } = 0;

    /// <summary>
    /// Maximum uncompressed size of the uploaded website zip (WebsiteData).
    /// 0 = default (20MB), -1 = max.
    /// </summary>
    public long MaxWebsiteDataSize { get; set; } = 0;

    public string Comments { get; set; } = "";

    /// <summary>
    /// Free-form words for grouping/filtering in the manager, no runtime meaning
    /// </summary>
    private List<string>? _tags;
    public List<string> Tags
    {
      get => _tags ??= new List<string>();
      set => _tags = value;
    }

    private List<CertificateData>? _certificates;
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

      /// <summary>
      /// Certificate in pfx format
      /// </summary>
      public byte[]? CertificatePfx { get; set; }

      /// <summary>
      /// Certificate-chain in pem format
      /// </summary>
      public string? Certificate { get; set; }

      /// <summary>
      /// Certificate private key in pem format
      /// </summary>
      public string? CertificateKey { get; set; }
    }

  }
}
