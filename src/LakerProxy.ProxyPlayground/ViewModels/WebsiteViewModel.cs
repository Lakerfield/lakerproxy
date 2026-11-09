using System.Linq;
using Caliburn.Micro;
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
