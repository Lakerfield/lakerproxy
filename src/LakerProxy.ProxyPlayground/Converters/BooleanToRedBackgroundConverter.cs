using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LakerProxy.ProxyPlayground.Converters
{
  public class BooleanToRedBackgroundConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      var bit = (bool)value;
      if (bit)
        return Brushes.Green;
      return Brushes.DarkRed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }

}
