using System;
using System.Windows.Data;

namespace LakerProxy.ProxyPlayground.Converters
{
  public class StringToCommaStringConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      var src = value as string;
      if (src == null)
        return string.Empty;

      return string.Join(", ", src.Split(["\r\n", "\n\r", "\r", "\n"], StringSplitOptions.None));
    }

    public object ConvertBack(object value, Type targetTypes, object parameter, System.Globalization.CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }

}
