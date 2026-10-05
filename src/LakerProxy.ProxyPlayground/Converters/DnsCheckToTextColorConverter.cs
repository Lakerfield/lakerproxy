using System;
using System.Globalization;
using System.Net.Sockets;
using System.Windows.Data;
using System.Windows.Media;
using LakerProxy.ProxyPlayground.Tools.DnsCheck;

namespace LakerProxy.ProxyPlayground.Converters
{
  public class DnsCheckToTextColorConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      var result = value as DnsCheckResult;
      if (result == null)
        return Brushes.Black;

      var isV6 = result.Family == AddressFamily.InterNetworkV6;

      switch (result.Status)
      {
        case DnsCheckStatus.Ok:
          return Brushes.DarkGreen;

        case DnsCheckStatus.Partial:
          return Brushes.Orange;

        case DnsCheckStatus.Mismatch:
        case DnsCheckStatus.NoDomain:
          return Brushes.Red;

        case DnsCheckStatus.None:
          // no AAAA record is allowed, no A record is not
          return isV6 ? Brushes.Gray : Brushes.Red;

        case DnsCheckStatus.Unknown:
          // records found but no linked server has an address of this family;
          // for IPv6 this is worth a look (IPv6 clients go elsewhere), for IPv4 it is a server config problem
          return isV6 ? Brushes.Orange : Brushes.DarkRed;

        case DnsCheckStatus.Error:
          return Brushes.DarkRed;

        case DnsCheckStatus.Checking:
        case DnsCheckStatus.Skipped:
          return Brushes.Gray;

        case DnsCheckStatus.NotChecked:
        default:
          return Brushes.Black;
      }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }
}
