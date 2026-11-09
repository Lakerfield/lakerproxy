using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;
using LakerProxy.WebApi;
using LakerProxy.WebApi.Models;

namespace LakerProxy.ProxyPlayground.Converters
{
  public class CertificateToTextColorConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      var entity = (value as Website);
      if (entity == null)
        return Brushes.Black;

      if (entity.Kind == WebsiteKind.Offline)
        return Brushes.Gray;

      if (entity.Certificates == null)
        return "- unknown -";
      var certificates = entity.Certificates.OrderByDescending(c => c.ValidTill).ToArray();
      var certificate = certificates.FirstOrDefault(c => c.State == CertificateStates.Active);
      if (certificate == null)
        certificate = certificates.FirstOrDefault(c => c.State == CertificateStates.Requesting);
      if (certificate == null)
        certificate = certificates.FirstOrDefault(c => c.State == CertificateStates.Expired);
      if (certificate == null)
        return Brushes.Black;

      switch (certificate.State)
      {
        case CertificateStates.Active:
          var now = DateTime.Now;
          if (certificate.ValidTill <= now)
            return Brushes.Red;

          var lifeSpan = certificate.ValidTill - certificate.ValidFrom;
          var oneThirthOfLifeSpan = lifeSpan / 3;

          if (certificate.ValidTill < now.AddDays(-14))
            return Brushes.OrangeRed;

          if ((certificate.ValidTill - oneThirthOfLifeSpan) < now)
            return Brushes.Orange;

          return Brushes.DarkGreen;
        
        case CertificateStates.Requesting:
          return Brushes.LightGreen;

        case CertificateStates.Expired:
          return Brushes.Red ;

        case CertificateStates.Unknown:
        default:
          return Brushes.DarkRed;
      }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }

}
