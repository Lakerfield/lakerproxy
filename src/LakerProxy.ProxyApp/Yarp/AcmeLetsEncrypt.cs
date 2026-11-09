using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Acme;
using Lakerfield.Acme.Models;
using LakerProxy.Models;

namespace LakerProxy.ProxyApp.Yarp
{
  public class AcmeLetsEncrypt
  {
    private readonly WebsiteSelector _provider;
    private readonly WebsiteWithCertificate _context;
    private readonly string _pfxPassword;
    private readonly IAcmeDnsChallengeStore _dnsChallengeStore;

    public AcmeLetsEncrypt(WebsiteSelector provider, WebsiteWithCertificate context, string pfxPassword, IAcmeDnsChallengeStore dnsChallengeStore)
    {
      _provider = provider;
      _context = context;
      _pfxPassword = pfxPassword;
      _dnsChallengeStore = dnsChallengeStore;
    }

    private string[] GetHostnamesForScope(string hostname, CertificateScope scope)
    {
      switch (scope)
      {
        case CertificateScope.SingleDomain:
          return [hostname];

        case CertificateScope.Wildcard:
          return [$"*.{hostname}"];

        case CertificateScope.WildcardWithApex:
          return [hostname, $"*.{hostname}"];

        default:
          throw new ArgumentOutOfRangeException(nameof(scope), _context.Website.Scope, "Unknown certificate scope");
      }
    }

    public async Task Execute(Func<(string domain, string step, string message), Task> writeLog)
    {
      var website = _context.Website;
      var hostname = website.Hostname;

      var account = await GetAccount();
      var acmeClient = await GetInitializedClient(account);
      await acmeClient.LoadAccountAsync(account.AccountUrl, account.Key.FromPem());

      var hostnames = GetHostnamesForScope(hostname, website.Scope);

      await writeLog((hostname, "1", $"Creating order for {string.Join(", ", hostnames)}..."));
      var acmeOrder = await acmeClient.CreateOrderAsync(hostnames);

      await writeLog((hostname, "2", "Processing authorizations..."));
      for (int i = 0; i < acmeOrder.Authorizations.Count; i++)
      {
        await writeLog((hostname, "2", $"Processing authorization {i+1} of {acmeOrder.Authorizations.Count}..."));

        var authz = await acmeClient.GetAuthorizationAsync(acmeOrder.Authorizations[i]);
        if (authz.Status == "valid")
        {
          await writeLog((hostname, "2", $"Authorization {i + 1} already valid, skipping challenges"));
          continue;
        }

        Challenge? http01Challenge = null;
        Challenge? dns01Challenge = null;
        Challenge? tlsAlpn01Challenge = null;
        foreach (var challenge in authz.Challenges)
        {
          await writeLog((hostname, "2-a", $"Challenge type: {challenge.Type}, status: {challenge.Status}"));

          // Select the HTTP-01 challenge
          if (challenge.Type == "http-01")
            http01Challenge = challenge;

          // Select the DNS-01 challenge
          if (challenge.Type == "dns-01")
            dns01Challenge = challenge;

          // Select the TLS-ALPN-01 challenge
          if (challenge.Type == "tls-alpn-01")
            tlsAlpn01Challenge = challenge;
        }

        if (http01Challenge != null)
        {
          var token = http01Challenge.Token!;
          var keyAuthValue = acmeClient.GetHttpChallengeValue(token);
          var challengeUrl = $"http://{authz.Identifier}/.well-known/acme-challenge/{token}";

          await writeLog((hostname, "3", $"HTTP-01 Challenge: {challengeUrl}"));
          if (http01Challenge.Status == "valid")
          {//????
            Console.WriteLine($"  Challenge already valid, skipping validation.");
            continue;
          }

          // Register the token with the local web app so the ACME server can fetch it.
          var acmeValidation = new AcmeValidation(
            token: http01Challenge.Token,
            challenge: keyAuthValue,
            validationCount: 3);
          _context.AcmeValidation = acmeValidation;
          try
          {
            Console.WriteLine($"  Validating challenge...");
            await acmeClient.ValidateChallengeAsync(http01Challenge.Url!);
            Console.WriteLine($"  Waiting for challenge validation...");

            var validatedChallenge = await acmeClient.WaitForChallengeValidAsync(http01Challenge.Url!);
            Console.WriteLine($"  Challenge status: {validatedChallenge.Status}");
          }
          finally
          {
            _context.AcmeValidation = default;
            acmeValidation.Dispose();//TODO remove taskcompletionsource
          }
          continue;
        }

        // DNS-01 example
        if (dns01Challenge != null)
        {
          //TODO test CNAME forward

          var dnsValue = dns01Challenge.Token != null
            ? acmeClient.GetDnsChallengeValue(dns01Challenge.Token!)
            : "n/a";
          var dnsDomain = LakerfieldAcmeClient.GetDnsValidationDomain(authz.Identifier);
          var forwardLabel = LakerfieldAcmeClient.GetDnsValidationForwardLabel(authz.Identifier);
          var forwardedTxtRecord = $"{forwardLabel}.{Program.Server.AcmeChallengeZone}";

          if (dns01Challenge.Status == "valid")
          {//?????
            Console.WriteLine($"  Challenge already valid, skipping validation.");
            continue;
          }

          // Register the token with the local dns so the ACME server can fetch it.
          _dnsChallengeStore.SetTxtRecord(
            forwardedTxtRecord,
            dnsValue,
            ttl: 30,
            validFor: TimeSpan.FromMinutes(10));
          try
          {
            Console.WriteLine($"  Validating challenge...");
            await acmeClient.ValidateChallengeAsync(dns01Challenge.Url!);
            Console.WriteLine($"  Waiting for challenge validation...");
            var validatedChallenge = await acmeClient.WaitForChallengeValidAsync(dns01Challenge.Url!);
            Console.WriteLine($"  Challenge status: {validatedChallenge.Status}");
          }
          finally
          {
            _dnsChallengeStore.RemoveRecord(forwardedTxtRecord);
          }
          continue;
        }

        // TLS-ALPN-01 example
        if (tlsAlpn01Challenge != null)
        {
          // TODO: implement when needed
        }
      }


      Console.WriteLine("  Waiting for order to become 'ready'...");
      var readyOrder = await acmeClient.WaitForOrderReadyAsync(acmeOrder.Url!);
      Console.WriteLine($"  Order status: {readyOrder.Status}");

      Console.WriteLine("  Finalizing order (submitting CSR)...");
      var (pendingOrder, certPrivateKey) = await acmeClient.FinalizeOrderAsync(readyOrder, hostnames);
      Console.WriteLine($"  Order status after finalize: {pendingOrder.Status}");

      Console.WriteLine("  Waiting for order to become 'valid'...");
      var validOrder = await acmeClient.WaitForOrderValidAsync(pendingOrder.Url!);
      Console.WriteLine($"  Order status: {validOrder.Status}");

      Console.WriteLine("  Downloading certificate...");
      var preferredRootCertificate = account.PreferredRootCertificate.IsNullOrWhiteSpaceThen(null);
      var pemChain = await acmeClient.DownloadCertificateAsync(validOrder);// TODO: preferredRootCertificate

      var pemKey = certPrivateKey.ToPem();

      // windows bug
      // https://github.com/dotnet/runtime/issues/23749
      // workaround: new X509Certificate2(certificate.Export(X509Certificates.X509ContentType.Pkcs12));
      //var certificate = X509Certificate2.CreateFromPem(pem, pem);

      //var pfx = certificateChain
      //  .ToPfx(privateKey)
      //  .Build(hostnames.First(), _pfxPassword);
      //var certificate = new X509Certificate2(pfx, _pfxPassword);

      // Leaf-certificaat koppelen aan private key
      using var certWithKey = X509Certificate2.CreateFromPem(pemChain, pemKey);

      // Alle CERTIFICATE blocks uit de chain laden
      var chainCerts = new X509Certificate2Collection();
      chainCerts.ImportFromPem(pemChain);

      // PFX-collectie opbouwen: leaf+key eerst, daarna intermediates
      var pfxCerts = new X509Certificate2Collection
      {
        certWithKey
      };

      foreach (var cert in chainCerts)
        if (!string.Equals(cert.Thumbprint, certWithKey.Thumbprint, StringComparison.OrdinalIgnoreCase))
          pfxCerts.Add(cert);

      var pfx = pfxCerts.Export(X509ContentType.Pkcs12, _pfxPassword);

      await _provider.Save(_context, pfx, certWithKey, pemChain, pemKey);
    }


    private async Task Update(string[] hostnames, Func<AcmeOrder, LakerfieldAcmeClient, Task> validate)
    {
      //try
      //{
      //  var account = await GetAccount();
      //  var acmeClient = await GetInitializedClient(account);
      //  await acmeClient.LoadAccountAsync(account.AccountUrl, account.Key.FromPem());

      //  var acmeOrder = await acmeClient.CreateOrderAsync(hostnames);

      //  await validate(acmeOrder, acmeClient);

      //  var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);

      //  var csr = await order.CreateCsr(privateKey);// auto fill csr with SubjectAlternativeNames from order
      //  csr.AddName($"C={account.CertificateCountryName}, ST={account.CertificateState}, L={account.CertificateLocality}, O={account.CertificateOrganization}, OU={account.CertificateOrganizationUnit}, CN={hostnames[0]}");
      //  var der = csr.Generate();
      //  var finalizedOrder = await order.Finalize(der);
      //  var preferredRootCertificate = account.PreferredRootCertificate.IsNullOrWhiteSpaceThen(null);
      //  var certificateChain = await order.Download(preferredRootCertificate);

      //  // custom lets encrypt chain ivm dual android signing with expired root
      //  string pemChain;
      //  if (account.Type == AcmeAccountTypes.LetsEncrypt || account.Type == AcmeAccountTypes.LetsEncryptStaging)
      //    pemChain = CustomChainToPem(certificateChain);
      //  else
      //    pemChain = certificateChain.ToPem();

      //  var pemKey = privateKey.ToPem();
      //  // windows bug
      //  // https://github.com/dotnet/runtime/issues/23749
      //  // workaround: new X509Certificate2(certificate.Export(X509Certificates.X509ContentType.Pkcs12));
      //  //var certificate = X509Certificate2.CreateFromPem(pem, pem);

      //  var pfx = certificateChain
      //    .ToPfx(privateKey)
      //    .Build(hostnames.First(), _pfxPassword);
      //  var certificate = new X509Certificate2(pfx, _pfxPassword);


      //  await _provider.Save(_context, pfx, certificate, pemChain, pemKey);
      //}
      //catch (Exception e)
      //{
      //  Console.WriteLine(e);
      //  throw;
      //}
    }


    private async Task<AcmeAccount> GetAccount()
    {
      var account = await _provider.GetAccount(_context);

      if (string.IsNullOrWhiteSpace(account.Key))
      {
        var acmeClient = await GetInitializedClient(account);
        var key = acmeClient.GenerateAccountKey();
        var accountResponse = await acmeClient.CreateAccountAsync(account.Mail, termsOfServiceAgreed: true);//TODO: account.TermsOfServiceAgreed
        // TODO: eab: account.EabKeyIdentifier, account.EabKey, account.EabKeyAlgorithm

        // Save the account key for later use
        account.Key = key.ToPem();
        account.AccountUrl = accountResponse.Url;

        await _provider.SetAccount(account, _context);
      }

      return account;
    }

    private Task<LakerfieldAcmeClient> GetInitializedClient(AcmeAccount account)
    {
      if (account.Type == AcmeAccountTypes.Custom)
        return GetInitializedClient(new Uri(account.AcmeDiscoveryUrl));
      return GetInitializedClient(GetServer(account.Type));
    }

    private async Task<LakerfieldAcmeClient> GetInitializedClient(Uri acmeDirectoryUrl)
    {
      var client = new LakerfieldAcmeClient(acmeDirectoryUrl, new HttpClient());
      await client.LoadDirectoryAsync();
      return client;
    }

    private Uri GetServer(AcmeAccountTypes type)
    {
      switch (type)
      {
        case AcmeAccountTypes.LetsEncrypt:
          return WellKnownServers.LetsEncrypt;

        case AcmeAccountTypes.LetsEncryptStaging:
          return WellKnownServers.LetsEncryptStaging;

        case AcmeAccountTypes.ZeroSsl:
          return WellKnownServers.ZeroSsl;

        default:
        case AcmeAccountTypes.Unknown:
          throw new ArgumentOutOfRangeException();
      }
    }

  }
}
