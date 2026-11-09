using System.Threading;

namespace LakerProxy.ProxyApp.BackgroundServices
{
  public class LetsEncryptTrigger
  {
    private CancellationTokenSource _trigger;
    public CancellationToken Token => _trigger.Token;

    public LetsEncryptTrigger()
    {
      Reset();
    }

    public void Trigger()
    {
      _trigger?.Cancel();
    }

    public void Reset()
    {
      _trigger = new CancellationTokenSource();
    }
  }
}
