using System.Linq;
using Caliburn.Micro;
using LakerProxy.ProxyPlayground.Tools.DnsCheck;
using LakerProxy.WebApi;
using Entity = LakerProxy.WebApi.Models.Website;

namespace LakerProxy.ProxyPlayground.ViewModels
{
  public class WebsiteViewModel : Screen
  {
    public override string DisplayName
    {
      get { return typeof(Entity).Name; }
      set { }
    }

    private Entity _entity;
    public Entity Entity
    {
      get { return _entity; }
      set
      {
        _entity = value;
        NotifyOfPropertyChange();
      }
    }

    public WebsiteViewModel(Entity entity)
    {
      Entity = entity;
    }

    public WebsiteViewModel Clone()
    {
      return new WebsiteViewModel(Entity.Clone());

    }


    // DNS check results per address family; filled by WebsitesViewModel.CheckDns,
    // null until a check has run
    private DnsCheckResult _dnsV4;
    public DnsCheckResult DnsV4
    {
      get { return _dnsV4; }
      set
      {
        _dnsV4 = value;
        NotifyOfPropertyChange();
        NotifyOfPropertyChange(() => DnsV4Info);
        NotifyOfPropertyChange(() => DnsV4ToolTip);
      }
    }

    private DnsCheckResult _dnsV6;
    public DnsCheckResult DnsV6
    {
      get { return _dnsV6; }
      set
      {
        _dnsV6 = value;
        NotifyOfPropertyChange();
        NotifyOfPropertyChange(() => DnsV6Info);
        NotifyOfPropertyChange(() => DnsV6ToolTip);
      }
    }

    public string DnsV4Info => DnsV4?.ShortText ?? "";
    public string DnsV4ToolTip => DnsV4?.ToolTipText;
    public string DnsV6Info => DnsV6?.ShortText ?? "";
    public string DnsV6ToolTip => DnsV6?.ToolTipText;

    public string CertificateInfo
    {
      get
      {
        if (Entity.Certificates == null)
          return "- unknown -";
        var certificates = Entity.Certificates.OrderByDescending(c => c.ValidTill).ToArray();
        var certificate = certificates.FirstOrDefault(c => c.State == CertificateStates.Active);
        if (certificate == null)
          certificate = certificates.FirstOrDefault(c => c.State == CertificateStates.Requesting);
        if (certificate == null)
          certificate = certificates.FirstOrDefault(c => c.State == CertificateStates.Expired);
        if (certificate == null)
          return "- none -";
        switch (certificate.State)
        {
          case CertificateStates.Active:
            return $"{certificate.ValidTill:yyyy-MM-dd HH:mm}";

          default:
          case CertificateStates.Unknown:
          case CertificateStates.Requesting:
          case CertificateStates.Expired:
            return $"{certificate.ValidTill:yy-MM-dd} {certificate.State}";
        }
      }
    }


    /// <summary>
    /// Name of the CNAME record to add to the DNS zone of the website hostname
    /// for dns-01 validation, e.g. _acme-challenge.www.example.com
    /// </summary>
    public string DnsCnameName
    {
      get
      {
        var hostname = (Entity.Hostname ?? "").Trim().TrimEnd('.');
        if (hostname.StartsWith("*."))
          hostname = hostname.Substring(2);
        if (hostname.Length == 0)
          return "";
        return $"_acme-challenge.{hostname}";
      }
    }

    /// <summary>
    /// Target of the CNAME record: the acme challenge label computed by the
    /// server on save ({hostname-prefix}.{server AcmeChallengeZone})
    /// </summary>
    public string DnsCnameValue
    {
      get
      {
        if (string.IsNullOrWhiteSpace(Entity.AcmeChallengeLabel))
          return "- available after first save -";
        return Entity.AcmeChallengeLabel;
      }
    }

    public void CopyDnsName()
    {
      System.Windows.Clipboard.SetText(DnsCnameName);
    }

    public bool CanCopyDnsName
    {
      get { return !string.IsNullOrWhiteSpace(DnsCnameName); }
    }

    public void CopyDnsValue()
    {
      System.Windows.Clipboard.SetText(Entity.AcmeChallengeLabel);
    }

    public bool CanCopyDnsValue
    {
      get { return !string.IsNullOrWhiteSpace(Entity.AcmeChallengeLabel); }
    }


    public async void Ok()
    {
      if (!CanOk)
        return;
      await TryCloseAsync(true);
    }

    public bool CanOk
    {
      get { return true; }
    }

    public async void Cancel()
    {
      await TryCloseAsync(false);
    }

  }
}
