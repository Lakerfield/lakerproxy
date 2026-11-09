using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;

namespace LakerProxy.ProxyPlayground.Converters
{
  public class StringsToMultiLineConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      var src = value as IEnumerable<string>; ;
      if (src == null)
        return null;
      return string.Join(Environment.NewLine, src);
    }

    public object ConvertBack(object value, Type targetTypes, object parameter, System.Globalization.CultureInfo culture)
    {
      var src = value as string;
      if (string.IsNullOrEmpty(src))
        return new List<string>();
      return src.Split(new [] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToList();
    }
  }
}
