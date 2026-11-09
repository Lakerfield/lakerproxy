using System;
using System.Threading;
using System.Threading.Tasks;

namespace LakerProxy.ProxyApp.Yarp
{
  public class AcmeValidation : IDisposable
  {
    private readonly string _token;
    private readonly string _challenge;
    private readonly TaskCompletionSource<bool> _completeTaskCompletionSource;
    private int _validationCount;

    public Task Task => _completeTaskCompletionSource.Task;

    public AcmeValidation(string token, string challenge, int validationCount = 4)
    {
      _token = token;
      _challenge = challenge;
      _validationCount = validationCount;
      _completeTaskCompletionSource = new TaskCompletionSource<bool>();
    }

    public string Validate(string token)
    {
      if (token == _token)
      {
        var tcs = _completeTaskCompletionSource;
        if (tcs != null)
          if (Interlocked.Decrement(ref _validationCount) <= 0)
            tcs.TrySetResult(true);
        return _challenge;
      }
      return string.Empty;
    }

    public void Dispose()
    {
      _completeTaskCompletionSource?.TrySetCanceled();
    }
  }
}
