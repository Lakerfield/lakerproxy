using System;
using System.Collections.Generic;
using System.Linq;

namespace LakerProxy.WebApi.Models
{
  public class Website
  {
    public string Id { get; set; } = "";

    public string Hostname { get; set; } = "";

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

    /// <summary>
    /// Read-only: true when an uploaded website zip (WebsiteData) is attached.
    /// </summary>
    public bool HasWebsiteData { get; set; }

    public string MaxRequestBodySize { get; set; } = "";

    /// <summary>
    /// Maximum uncompressed size of the uploaded website zip, e.g. '5MB',
    /// 'default' (20MB) or 'max'.
    /// </summary>
    public string MaxWebsiteDataSize { get; set; } = "";

    public string Comments { get; set; } = "";

    /// <summary>
    /// Free-form words for grouping/filtering in the manager, no runtime meaning
    /// </summary>
    private List<string> _tags;
    public List<string> Tags
    {
      get => _tags ??= new List<string>();
      set => _tags = value;
    }

    public WebsiteCertificate[]? Certificates { get; set; }


    public Website Clone()
    {
      return (Website)MemberwiseClone();
    }

    /// <summary>
    /// Tags are single lowercase words: split on whitespace, trim, drop empties and duplicates.
    /// Shared by the server (on save) and the manager (editor input) so both agree on the format.
    /// </summary>
    public static List<string> NormalizeTags(IEnumerable<string> tags)
    {
      if (tags == null)
        return new List<string>();

      return tags
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .SelectMany(t => t.Split((char[])null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Select(t => t.ToLowerInvariant())
        .Distinct()
        .ToList();
    }

    public static List<string> NormalizeTags(string tags)
    {
      return NormalizeTags(new[] { tags });
    }
  }

  public class WebsiteCertificate
  {
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTill { get; set; }

    public CertificateStates State { get; set; }

  }
}
