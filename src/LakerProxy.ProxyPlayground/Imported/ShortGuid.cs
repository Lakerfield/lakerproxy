using System;

// ReSharper disable once CheckNamespace
namespace Lakerfield
{
  /// <summary>
  /// Represents a globally unique identifier (GUID) with a 
  /// shorter string value. Sguid
  /// </summary>
  public struct ShortGuid
  {
    #region Static

    /// <summary>
    /// A read-only instance of the ShortGuid class whose value 
    /// is guaranteed to be all zeroes. 
    /// </summary>
    public static readonly ShortGuid Empty = new ShortGuid(Guid.Empty);

    #endregion

    #region Fields

    private Guid _guid;
    private string _value;
    private const string EmptyValue = "AAAAAAAAAAAAAAAAAAAAAA";

    #endregion

    #region Contructors

    /// <summary>
    /// Creates a ShortGuid from a base64 encoded string
    /// </summary>
    /// <param name="value">The encoded guid as a base64 string</param>
    public ShortGuid(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
      {
        _guid = Guid.Empty;
        _value = Encode(_guid);
      }
      else
      {
        _value = value;
        _guid = Decode(value);
      }
    }

    /// <summary>
    /// Creates a ShortGuid from a Guid
    /// </summary>
    /// <param name="guid">The Guid to encode</param>
    public ShortGuid(Guid guid)
    {
      _value = Encode(guid);
      _guid = guid;
    }

    ///// <summary>
    ///// Creates a ShortGuid from a base32 encoded string
    ///// </summary>
    ///// <param name="value">The encoded guid as a base32 string</param>
    //public static ShortGuid FromBase32(string value)
    //{
    //  return DecodeFromBase32(value);
    //}

    #endregion

    #region Properties

    /// <summary>
    /// Gets/sets the underlying Guid
    /// </summary>
    public Guid Guid
    {
      get { return _guid; }
      set
      {
        if (value != _guid)
        {
          _guid = value;
          _value = Encode(value);
        }
      }
    }

    /// <summary>
    /// Gets/sets the underlying base64 encoded string
    /// </summary>
    public string Value
    {
      get { return _value ?? EmptyValue; }
      set
      {
        if (value != _value)
        {
          _value = value;
          _guid = Decode(value);
        }
      }
    }

    #endregion

    public bool IsEmpty()
    {
      return IsEmpty(this);
    }

    public static bool IsEmpty(ShortGuid shortGuid)
    {
      if (shortGuid.Guid == Guid.Empty)
        return true;
      return false;
    }

    public int Version()
    {
      return Version(this);
    }

    public static int Version(ShortGuid shortGuid)
    {
      if (shortGuid == null)
        return 0;

      var bytes = shortGuid.Guid.ToByteArray();
      return bytes[7] >> 4;
    }

    public int Variant()
    {
      return Variant(this);
    }

    public static int Variant(ShortGuid shortGuid)
    {
      if (shortGuid == null)
        return 0;

      var bytes = shortGuid.Guid.ToByteArray();
      return bytes[8] >> 5;
    }

    #region ToString

    /// <summary>
    /// Returns the base64 encoded guid as a string
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return _value ?? EmptyValue;
    }

    ///// <summary>
    ///// Returns the base32 encoded guid as a string
    ///// </summary>
    ///// <returns></returns>
    //public string ToBase32String(bool upper = true)
    //{
    //  return EncodeToBase32(_guid, upper);
    //}

    #endregion

    #region Equals

    /// <summary>
    /// Returns a value indicating whether this instance and a 
    /// specified Object represent the same type and value.
    /// </summary>
    /// <param name="obj">The object to compare</param>
    /// <returns></returns>
    public override bool Equals(object? obj)
    {
      if (obj is ShortGuid shortGuid)
        return _guid.Equals(shortGuid._guid);
      if (obj is Guid guid)
        return _guid.Equals(guid);
      if (obj is string)
        return _guid.Equals(((ShortGuid)obj)._guid);
      return false;
    }

    #endregion

    #region GetHashCode

    /// <summary>
    /// Returns the HashCode for underlying Guid.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
      return _guid.GetHashCode();
    }

    #endregion

    #region NewGuid

    /// <summary>
    /// Initialises a new instance of the ShortGuid class
    /// </summary>
    /// <returns></returns>
    public static ShortGuid NewGuid()
    {
      return new ShortGuid(Guid.NewGuid());
    }

    #endregion

    #region Encode

    /// <summary>
    /// Creates a new instance of a Guid using the string value, 
    /// then returns the base64 encoded version of the Guid.
    /// </summary>
    /// <param name="value">An actual Guid string (i.e. not a ShortGuid)</param>
    /// <returns></returns>
    public static string Encode(string value)
    {
      Guid guid = new Guid(value);
      return Encode(guid);
    }

    /// <summary>
    /// Encodes the given Guid as a base64 string that is 22 
    /// characters long.
    /// </summary>
    /// <param name="guid">The Guid to encode</param>
    /// <returns></returns>
    public static string Encode(Guid guid)
    {
      string encoded = Convert.ToBase64String(guid.ToByteArray());
      encoded = encoded
        .Replace("/", "_")
        .Replace("+", "-");
      return encoded.Substring(0, 22);
    }

    ///// <summary>
    ///// Encodes the given Guid as a base32 string that is 26 
    ///// characters long.
    ///// </summary>
    ///// <param name="guid">The Guid to encode</param>
    ///// <param name="upper">Upper or lowercase characters</param>
    ///// <returns></returns>
    //public static string EncodeToBase32(Guid guid, bool upper = true)
    //{
    //  string encoded = Tools.Base32Encoding.ToString(guid.ToByteArray(), upper);
    //  return encoded.Substring(0, 26);
    //}

    #endregion

    #region Decode

    /// <summary>
    /// Decodes the shortguid given a base64, base32 or guid string
    /// </summary>
    /// <param name="value">The encoded string of a Guid</param>
    /// <returns>A new Guid</returns>
    public static Guid Decode(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return Guid.Empty;

      //123456789012345678901234567890
      //XgnLA7hz-0W2L-doUJgg3Q
      if (value.Length == 22)
        return DecodeFromBase64(value);

      ////LYE4WA5YOP5ULNRP45UFBGBA3U
      //if (value.Length == 26)
      //  return DecodeFromBase32(value);

      //03cb095e-73b8-45fb-b62f-e768509820dd
      return Guid.Parse(value);
    }

    /// <summary>
    /// Decodes the given base64 string
    /// </summary>
    /// <param name="value">The base64 encoded string of a Guid</param>
    /// <returns>A new Guid</returns>
    public static Guid DecodeFromBase64(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return Guid.Empty;
      value = value
        .Replace("_", "/")
        .Replace("-", "+");
      byte[] buffer = Convert.FromBase64String(value + "==");
      return new Guid(buffer);
    }

    /// <summary>
    /// Decodes the given base64 string
    /// </summary>
    /// <param name="value">The base64 encoded string of a Guid</param>
    /// <param name="guid">A parsed Guid</param>
    /// <returns>success</returns>
    public static bool TryDecodeFromBase64(string value, out Guid guid)
    {
      guid = Guid.Empty;
      if (string.IsNullOrWhiteSpace(value) || value.Length != 22)
        return false;

      value = value
        .Replace("_", "/")
        .Replace("-", "+") + "==";

      byte[] buffer = new byte[16];//((value.Length * 3) + 3) / 4 - (value.Length > 0 && value[^1] == '=' ? value.Length > 1 && value[^2] == '=' ? 2 : 1 : 0)];
      int written;
      bool success = Convert.TryFromBase64String(value, buffer, out written);
      if (success)
        guid = new Guid(buffer);
      return success;
    }

    ///// <summary>
    ///// Decodes the given base32 string
    ///// </summary>
    ///// <param name="value">The base32 encoded string of a Guid</param>
    ///// <returns>A new Guid</returns>
    //public static Guid DecodeFromBase32(string value)
    //{
    //  byte[] buffer = Tools.Base32Encoding.ToBytes(value);// Convert.FromBase64String(value + "==");
    //  return new Guid(buffer);
    //}

    /// <summary>
    /// Decodes the given base32 string
    /// </summary>
    /// <param name="value">The base32 encoded string of a Guid</param>
    /// <returns>A new Guid</returns>
    public static bool TryDecodeFromBase32(string value, out Guid guid)
    {
      guid = Guid.Empty;
      if (string.IsNullOrWhiteSpace(value) || value.Length != 26)
        return false;

      throw new NotImplementedException();
      //byte[] buffer = Tools.Base32Encoding.ToBytes(value);
      //return new Guid(buffer);
    }

    #endregion

    #region Operators

    /// <summary>
    /// Determines if both ShortGuids have the same underlying 
    /// Guid value.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public static bool operator ==(ShortGuid x, ShortGuid y)
    {
      if ((object)x == null) return (object)y == null;
      return x._guid == y._guid;
    }

    /// <summary>
    /// Determines if both ShortGuids do not have the 
    /// same underlying Guid value.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public static bool operator !=(ShortGuid x, ShortGuid y)
    {
      return !(x == y);
    }

    /// <summary>
    /// Implicitly converts the ShortGuid to it's string equivilent
    /// </summary>
    /// <param name="shortGuid"></param>
    /// <returns></returns>
    public static implicit operator string(ShortGuid shortGuid)
    {
      return shortGuid._value ?? EmptyValue;
    }

    /// <summary>
    /// Implicitly converts the ShortGuid to it's Guid equivilent
    /// </summary>
    /// <param name="shortGuid"></param>
    /// <returns></returns>
    public static implicit operator Guid(ShortGuid shortGuid)
    {
      return shortGuid._guid;
    }

    /// <summary>
    /// Implicitly converts the string to a ShortGuid
    /// </summary>
    /// <param name="shortGuid"></param>
    /// <returns></returns>
    public static implicit operator ShortGuid(string shortGuid)
    {
      return new ShortGuid(shortGuid);
    }

    /// <summary>
    /// Implicitly converts the Guid to a ShortGuid 
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    public static implicit operator ShortGuid(Guid guid)
    {
      return new ShortGuid(guid);
    }

    #endregion


    public string ToLuuidString()
    { //0 2 4 6  8 10 1214 16   20
      //03020100-0504-0706-0809-0a0b0c0d0e0f
      var x = Guid.ToString().Replace("-", "");

      return $"{x.Substring(6, 2)}{x.Substring(4, 2)}{x.Substring(2, 2)}{x.Substring(0, 2)}-{x.Substring(10, 2)}{x.Substring(8, 2)}-{x.Substring(14, 2)}{x.Substring(12, 2)}-{x.Substring(16, 4)}-{x.Substring(20, 12)}";
    }

    public static ShortGuid FromLuuidString(string input)
    { //0 2 4 6  8 10 1214 16   20
      //03020100-0504-0706-0809-0a0b0c0d0e0f
      var x = input.Replace("-", "");

      return new ShortGuid($"{x.Substring(6, 2)}{x.Substring(4, 2)}{x.Substring(2, 2)}{x.Substring(0, 2)}-{x.Substring(10, 2)}{x.Substring(8, 2)}-{x.Substring(14, 2)}{x.Substring(12, 2)}-{x.Substring(16, 4)}-{x.Substring(20, 12)}");
    }

    public static bool TryParse(string? value, out ShortGuid result)
    {
      result = Empty;
      if (string.IsNullOrWhiteSpace(value))
      {
        return true;
      }
      else
      {
        // Decode
        if (string.IsNullOrWhiteSpace(value))
          return true;

        //123456789012345678901234567890
        //XgnLA7hz-0W2L-doUJgg3Q
        if (value.Length == 22)
        {
          System.Guid guid;
          if (TryDecodeFromBase64(value, out guid))
          {
            result = guid;
            return true;
          }
        }

        //LYE4WA5YOP5ULNRP45UFBGBA3U
        if (value.Length == 26)
        {
          //TODO: return TryDecodeFromBase32(value);
        }

        //03cb095e-73b8-45fb-b62f-e768509820dd
        if (Guid.TryParse(value, out var guidResult))
        {
          result = guidResult;
          return true;
        }

        return false;
      }


    }
  }
}
