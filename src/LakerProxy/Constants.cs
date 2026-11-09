namespace LakerProxy
{
  public static partial class Constants
  {
    public static long BeginOfTimeInTicks = 624755808000000000; //1980-10-10

    public const int Byte = 1;
    public const int KiloByte = 1024 * Byte;
    public const int MegaByte = 1024 * KiloByte;
    public const int GigaByte = 1024 * MegaByte;
    public const long TeraByte = 1024 * (long)GigaByte;
    public const long PetaByte = 1024 * TeraByte;
  }



  public enum MongoDbConnectionMode
  {
    ConnectionString,
    DirectToServer,
  }

  public enum EntityChangedType
  {
    Reset,
    New,
    Update,
    Delete,
    Live,
  }

  public enum FloriAppLogLevel
  {
    /// <summary>
    /// 0 = Trace
    /// </summary>
    Trace = 0,

    /// <summary>
    /// 1 = Debug
    /// </summary>
    Debug = 1,

    /// <summary>
    /// 2 = Information
    /// </summary>
    Info = 2,

    /// <summary>
    /// 3 = Warning
    /// </summary>
    Warn = 3,

    /// <summary>
    /// 4 = Error
    /// </summary>
    Error = 4,

    /// <summary>
    /// 5 = Fatal
    /// </summary>
    Fatal = 5,
  }


}
