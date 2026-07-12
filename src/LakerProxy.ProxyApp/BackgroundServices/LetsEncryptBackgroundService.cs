using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lakerfield.Acme;
using Lakerfield.Runtime;
using Lakerfield.Settings;
using LakerProxy.Models;
using LakerProxy.ProxyApp.Yarp;
using LakerProxy.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LakerProxy.ProxyApp.BackgroundServices
{
  public class LetsEncryptBackgroundService : BackgroundService
  {
    private readonly AppServices _services;
    private readonly WebsiteSelector _websiteSelector;
    private readonly LetsEncryptTrigger _letsEncryptTrigger;
    private readonly IAcmeDnsChallengeStore _dnsChallengeStore;
    private readonly ILogger<LetsEncryptBackgroundService> _logger;

    public LetsEncryptBackgroundService(
      AppServices services,
      WebsiteSelector websiteSelector,
      LetsEncryptTrigger letsEncryptTrigger,
      IAcmeDnsChallengeStore dnsChallengeStore,
      ILogger<LetsEncryptBackgroundService> logger)
    {
      _services = services;
      _websiteSelector = websiteSelector;
      _letsEncryptTrigger = letsEncryptTrigger;
      _dnsChallengeStore = dnsChallengeStore;
      _logger = logger;
    }

    private Task WriteLog(string domain, string step, string message)
    {
      var log = _services.AcmeLog().New(domain, step, message);
      return _services.AcmeLog().Insert(log);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _logger.LogDebug($"{nameof(LetsEncryptBackgroundService)} is starting.");

      stoppingToken.Register(() =>
        _logger.LogDebug($" {nameof(LetsEncryptBackgroundService)} background task is stopping."));

      await Task.Delay(TimeSpan.FromMinutes(1));

      var isFirstRun = true;
      while (!stoppingToken.IsCancellationRequested)
      {
        _logger.LogDebug($"{nameof(LetsEncryptBackgroundService)} is checking certificates.");
        if (_letsEncryptTrigger.Token.IsCancellationRequested)
          _letsEncryptTrigger.Reset();
        
        var allWebsites = await _services.Website().GetAll();
        var websites = allWebsites.Where(w =>
          w.Scope is CertificateScope.SingleDomain or CertificateScope.Wildcard or CertificateScope.WildcardWithApex &&
          w.PrimaryServer == Program.Server.Id &&
          !string.Equals(w.Hostname, "localhost", StringComparison.OrdinalIgnoreCase)).ToArray();
        await WriteLog("", "", $"certificate check started for {websites.Length} of {allWebsites.Count}");
        allWebsites.Clear();

        foreach (var website in websites)
        {
          var activeCertificate = website.GetActiveCertificate();
          if (!MustBeUpdated(website, activeCertificate, isFirstRun))
            continue;

          _logger.LogWarning($"{nameof(LetsEncryptBackgroundService)} {website.Hostname} certificate must be renewed.");
          if (activeCertificate == null)
            await WriteLog(website.Hostname, "1", $"certificate must requested");
          else
            await WriteLog(website.Hostname, "1", $"certificate must be renewed, valid till {activeCertificate.ValidTill:O}");
          try
          {
            var websiteContext = _websiteSelector.GetByWebsiteId(website.Id);
            if (websiteContext == null)
              throw new Exception($"WebsiteContext not found for {website.Hostname} ({website.Id})");

            var letsEncrypt = new AcmeLetsEncrypt(_websiteSelector, websiteContext, WebsiteContextExtensions.PfxPassword, _dnsChallengeStore);

            await letsEncrypt.Execute(x => WriteLog(x.domain, x.step, x.message));

            _logger.LogWarning($"{nameof(LetsEncryptBackgroundService)} {website.Hostname} certificate is renewed.");
            await WriteLog(website.Hostname, "9", "certificate request is completed");
          }
          catch (Exception e)
          {
            _logger.LogError(e, $"{nameof(LetsEncryptBackgroundService)} exception while renewing {website.Hostname}.");
            await WriteLog(website.Hostname, "E", e.Message);
          }
        }

        await WriteLog("", "", "certificate check completed");

        isFirstRun = false;
        if (_letsEncryptTrigger.Token.IsCancellationRequested)
        {
          _letsEncryptTrigger.Reset();
          continue;
        }

        try
        {
          await Task.Delay(TimeSpan.FromHours(4), CancellationTokenSource.CreateLinkedTokenSource(_letsEncryptTrigger.Token, stoppingToken).Token);
        }
        catch (TaskCanceledException)
        {
        }
      }

      _logger.LogDebug($"{nameof(LetsEncryptBackgroundService)} background task is stopped.");
    }

    private bool MustBeUpdated(Website website, Website.CertificateData? certificate, bool isFirstRun)
    {
      if (website.ForceRenew)
        return true;

      if (certificate == null)
        return true;

      // always update in initial run if valid less than a week
      if (isFirstRun && certificate.ValidTill <= DateTime.Now.AddDays(7))
        return true;

      // check if is almost expiring, 2/3 lets encrypt rule
      var lifetime = certificate.ValidTill - certificate.ValidFrom;
      var renewFrom = certificate.ValidFrom + (lifetime * 2 / 3);

      return renewFrom < DateTime.Now;
    }

  }
}
