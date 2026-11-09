using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace LakerProxy.ProxyPlayground.Converters
{
  public class HumanizeConverter : IValueConverter
  {
    private static readonly Regex RegexUnderscore = new Regex(@"_", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex RegexCamel = new Regex(@"[a-z][A-Z]", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public static string SplitCamel(Match m)
    {
      string x = m.ToString();
      return x[0] + " " + x.Substring(1, x.Length - 1);
    }

    public static string Humanize(object value)
    {
      string s = null;
      if (value != null)
      {
        s = value.ToString();
        s = RegexUnderscore.Replace(s, " ");
        s = RegexCamel.Replace(s, new MatchEvaluator(SplitCamel));
      }
      return s;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      return Humanize(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException("unexpected Convertback");
    }
  }
}
