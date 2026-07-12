using Lakerfield;
using Lakerfield.Bson;
using LakerProxy.Models;
using MongoDB.Bson.Serialization;

namespace LakerProxy.Bson
{
  public class AppBsonConfiguration : ABsonConfig
  {
    public override BsonPriority Priority => BsonPriority.Application1;

    public override void RegisterSerializers()
    {
      //BsonSerializer.RegisterSerializer(typeof(DateTime), new DateTimeSerializer(DateTimeKind.Local));

    }

    public override void RegisterTypes()
    {
      BsonClassMap.RegisterClassMap<Log>(cm =>
      {
        AutoMapAndShortGuidId(cm);

        cm.GetMemberMap(c => c.Owner).SetElementName("o").SetDefaultValue(ShortGuid.Empty).SetIgnoreIfDefault(true);
        cm.GetMemberMap(c => c.Date).SetElementName("d");
        cm.GetMemberMap(c => c.Machine).SetElementName("m");
        cm.GetMemberMap(c => c.ProcessId).SetElementName("p");
        cm.GetMemberMap(c => c.Application).SetElementName("a");
        cm.GetMemberMap(c => c.LogLevel).SetElementName("l");
        //cm.GetMemberMap(c => c.Values);
      });

      BsonClassMap.RegisterClassMap<AcmeAccount>(cm =>
      {
        AutoMapAndShortGuidId(cm);

        cm.GetMemberMap(c => c.Default)
          .SetDefaultValue(false)
          .SetIgnoreIfDefault(true);
      });
      BsonClassMap.RegisterClassMap<AcmeLog>(AutoMapAndShortGuidId);
      BsonClassMap.RegisterClassMap<ApiUser>(AutoMapAndShortGuidId);
      BsonClassMap.RegisterClassMap<Server>(AutoMapAndShortGuidId);
      BsonClassMap.RegisterClassMap<Website>(cm =>
      {
        AutoMapAndShortGuidId(cm);

        cm.GetMemberMap(c => c.MaxRequestBodySize)
          .SetDefaultValue(0L);
        cm.GetMemberMap(c => c.MaxWebsiteDataSize)
          .SetDefaultValue(0L);
      });
      BsonClassMap.RegisterClassMap<WebsiteData>(AutoMapAndShortGuidId);
    }

  }
}
