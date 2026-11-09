using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LakerProxy.ProxyPlayground.Controls
{
  public static class TextBlockProperties
  {
    public static readonly DependencyProperty FormattedTextProperty =
      DependencyProperty.RegisterAttached(
        "FormattedText",
        typeof(string),
        typeof(TextBlockProperties),
        new PropertyMetadata(string.Empty, OnFormattedTextChanged));

    public static void SetFormattedText(DependencyObject textBlock, string value)
    {
      textBlock.SetValue(FormattedTextProperty, value);
    }

    public static string GetFormattedText(DependencyObject textBlock)
    {
      return (string)textBlock.GetValue(FormattedTextProperty);
    }

    private static void OnFormattedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      var textBlock = d as TextBlock;
      if (textBlock == null) return;

      string domain = e.NewValue as string;
      if (string.IsNullOrEmpty(domain))
        return;

      domain = AlignDomainExtension(domain);

      textBlock.Inlines.Clear();

      var parts = domain.Split('.');
      for (int i = 0; i < parts.Length; i++)
      {
        if (i > 0)
        {
          textBlock.Inlines.Add(new Run("."));
        }
        if (i == parts.Length - 2)
          textBlock.Inlines.Add(new Run(parts[i]) { FontWeight = FontWeights.ExtraBold });
        else if (i == parts.Length - 1)
          textBlock.Inlines.Add(new Run(parts[i]) { Foreground = Brushes.DarkGray, FontWeight = FontWeights.ExtraBold });
        else
          textBlock.Inlines.Add(new Run(parts[i]) { Foreground = Brushes.DarkGray });
      }
    }

    private static string AlignDomainExtensionRight(string domain)
    {
      if (string.IsNullOrEmpty(domain))
        return domain;

      var parts = domain.Split('.', StringSplitOptions.TrimEntries);
      var extension = parts.Last();
      switch (extension.Length)
      {
        case 1:
          return $"{domain}    ";

        case 2:
          return $"{domain}   ";

        case 3:
          return $"{domain}  ";

        case 4:
          return $"{domain} ";

        default:
          return domain;
      }
    }

    private static string Padding = "                        ";
    private static string AlignDomainExtension(string domain)
    {
      if (string.IsNullOrEmpty(domain))
        return domain;

      //24.24.6
      var domainIndex = GetSecondToLastIndexOfDot(domain);

      if (domainIndex == -1)
        domain = Padding + " " + domain;
      else if (domainIndex >= 0 && domainIndex < Padding.Length)
        domain = Padding.Substring(0, Padding.Length - domainIndex) + domain;

      return domain;

      static int GetSecondToLastIndexOfDot(string value)
      {
        bool foundLast = false;
        for (int i = value.Length - 1; i >= 0; i--)
        {
          if (value[i] != '.')
            continue;

          if (foundLast)
            return i;

          foundLast = true;
        }
        return -1;
      }
    }

  }
}
