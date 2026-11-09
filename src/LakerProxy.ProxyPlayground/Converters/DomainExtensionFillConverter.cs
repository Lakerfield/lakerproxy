using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace LakerProxy.ProxyPlayground.Converters
{
  public class DomainExtensionFillConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      string s = null;
      if (value != null)
      {
        s = value.ToString();
        if (string.IsNullOrEmpty(s))
          return s;
        var parts = s.Split('.', StringSplitOptions.TrimEntries);
        var extension = parts.Last();
        switch (extension.Length)
        {
          case 1:
            return $"{s}  ";

          case 2:
            return $"{s} ";

          default:
            return s;
        }
      }
      return s;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException("unexpected Convertback");
    }
  }
}
