using System;
using Lakerfield;

namespace LakerProxy.Extensions
{
  public static class StringExtensions
  {
    public static bool ToBool(this string input, bool @default = false)
    {
      if (string.IsNullOrWhiteSpace(input))
        return @default;
      if (bool.TryParse(input, out var result))
        return result;
      return @default;
    }

    public static int ToInt(this string input, int @default = 0)
    {
      if (int.TryParse(input, out var result))
        return result;
      return @default;
    }

    public static DateTime ToDateTime(this string input, DateTime @default = default(DateTime))
    {
      if (string.IsNullOrWhiteSpace(input))
        return @default;

      switch (input.Length)
      {
        case 8: //yyyyMMdd
          return new DateTime(
            input.Substring(0, 4).ToInt(),
            input.Substring(4, 2).ToInt(),
            input.Substring(6, 2).ToInt());

        case 12://yyyyMMddHHmm
          return new DateTime(
            input.Substring(0, 4).ToInt(),
            input.Substring(4, 2).ToInt(),
            input.Substring(6, 2).ToInt(),
            input.Substring(8, 2).ToInt(),
            input.Substring(10, 2).ToInt(),
            0);
      }

      return @default;
    }

    public static string? LimitTo(this string? input, int length)
    {
      if (input == null)
        return null;
      if (input.Length > length)
        return input.Substring(0, length);
      return input;
    }

    public static bool IsNotNullOrWhiteSpace(this string? input)
    {
      return !string.IsNullOrWhiteSpace(input);
    }

    public static bool IsNullOrWhiteSpace(this string? input)
    {
      return string.IsNullOrWhiteSpace(input);
    }

    public static string IsNullOrWhiteSpaceThen(this string? input, string @default)
    {
      if (string.IsNullOrWhiteSpace(input))
        return @default;
      return input;
    }

    public static string IsNullOrWhiteSpaceThenThrow(this string? input)
    {
      if (string.IsNullOrWhiteSpace(input))
        throw new ArgumentNullException("String cannot be null or whitespace");
      return input;
    }

    public static bool IsDigitsOnly(this string @string)
    {
      foreach (var c in @string)
        if (c < '0' || c > '9')
          return false;
      return true;
    }

    public static ShortGuid? ToNullableShortGuid(this string input)
    {
      if (string.IsNullOrWhiteSpace(input))
        return null;
      return input;
    }

  }
}
