using System;
using System.Collections.Generic;
using System.Text;
using Lakerfield;

namespace LakerProxy
{
  public interface IFloriAppLog
  {
    void UpgradeLogLevel(FloriAppLogLevel level);
    object Get(string key);
    void Set(string key, object value);
    IFloriAppLog Add(string key);
  }

  public interface IFloriAppRootLog : IFloriAppLog
  {
    void SetOwner(ShortGuid owner);
    //void SetOwner(Company owner);
  }

}
