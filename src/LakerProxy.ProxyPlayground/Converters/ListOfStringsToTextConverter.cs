using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace LakerProxy.ProxyPlayground.Converters
{
  public class ListOfStringsToTextConverter : IMultiValueConverter
  {
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
      if (values[0] is IList<string> lines && lines.Count > 0)
        return string.Join(Environment.NewLine, lines);
      else
        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }

}
