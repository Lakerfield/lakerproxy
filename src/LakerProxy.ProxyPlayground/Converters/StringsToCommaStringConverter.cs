using System;
using System.Collections.Generic;
using System.Windows.Data;

namespace LakerProxy.ProxyPlayground.Converters
{
  public class StringsToCommaStringConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      var src = value as IEnumerable<string>;
      if (src == null)
        return string.Empty;
      return string.Join(", ", src);
    }

    public object ConvertBack(object value, Type targetTypes, object parameter, System.Globalization.CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }

}
