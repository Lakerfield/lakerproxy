using System.Collections.Generic;
using System.Threading.Tasks;

namespace LakerProxy.ProxyApp.Yarp
{
  public static class AsyncRetryLoop
  {

    /// <summary>
    /// Retry 10 times, max add delay duration is 30 seconds
    /// 0, 750, 1250, 1750, 2250, 2750, 3250, 3750, 4250, 4750, 5250ms
    /// </summary>
    /// <returns></returns>
    public static async IAsyncEnumerable<int> Retry10Times()
    {
      for (int i = 0; i < 10; i++)
      {
        if (i != 0)
          await Task.Delay(250 + i * 500); // loop of 10 is max 30 seconds

        yield return i;
      }
    }

  }
}
