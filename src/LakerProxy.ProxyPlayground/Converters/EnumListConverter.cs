using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace LakerProxy.ProxyPlayground.Converters
{

  public class EnumListConverter<TEnumType> : ObservableCollection<EnumListConverter<TEnumType>.EnumListItem<TEnumType>>, IValueConverter where TEnumType : struct 
  {
    public EnumListConverter()
    {
      if (!typeof(TEnumType).IsEnum)
        throw new ArgumentException("TEnumType must be an enumerated type");

      var values = Enum.GetValues(typeof(TEnumType)).Cast<TEnumType>();

      foreach (var value in values)
        Add(new EnumListItem<TEnumType>(GetOmSchrijving(value), value));
    }

    private string GetOmSchrijving(TEnumType value)
    {
      // todo EnumDescriptionAttribute

      var name = value.ToString();
      return HumanizeConverter.Humanize(name);
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      return Items.FirstOrDefault(i => Equals(i.Value, value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      var v = (EnumListItem<TEnumType>)value;
      return v.Value;
    }



    public class EnumListItem<T>
    {
      public string Omschrijving { get; set; }
      public T Value { get; set; }

      public EnumListItem(string omschrijving, T value)
      {
        Omschrijving = omschrijving;
        Value = value;
      }

      public override string ToString()
      {
        return Omschrijving;
      }
    }



    // Werkt voor SelectedIndex...

    //public EnumListConverter()
    //{
    //  var hc = new HumanizeConverter();
    //  var names = Enum.GetNames(typeof(TEnumType));
    //  foreach (string s in names)
    //    Add(hc.Humanize(s));
    //}

    //public object Convert(object value, Type targetType,
    //       object parameter, System.Globalization.CultureInfo culture)
    //{
    //  var v = (int)value;
    //  return v;
    //}

    //public object ConvertBack(object value, Type targetType,
    //       object parameter, System.Globalization.CultureInfo culture)
    //{
    //  var v = (int)value;
    //  var values = Enum.GetValues(typeof(TEnumType));
    //  if ((v < 0) || (v >= values.Length))
    //    v = 0;
    //  var et = (TEnumType)values.GetValue(v);
    //  return et;
    //}

  }

}
