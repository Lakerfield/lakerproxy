extern alias IPNetwork2;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Runtime;
using LakerProxy.Models;
using LakerProxy.ProxyApp.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;

namespace LakerProxy.ProxyApp.Yarp
{
  public static class WebsiteContextExtensions
  {
    public static readonly object HttpContextKey = new object();
    public const string PfxPassword = "P@ssw0rd!";


    public static IWebHostBuilder AddWebsiteSelector(this IWebHostBuilder builder)
    {
      return builder.ConfigureServices(s => s.AddSingleton<WebsiteSelector>());
    }

    public static IApplicationBuilder UseWebsiteSelector(this IApplicationBuilder builder)
    {
      return builder
        .UseMiddleware<WebsiteContextSelectMiddleware>()
        .UseAcmeServerCertificate()
        .MapWhen(context =>
        {
          if (context.Request.IsHttps)
            return false;

          var website = context.GetWebsite();
          if (website == null)
            return false;

          if (website.Website.Scope == CertificateScope.None)
            return false;

          return website.ActiveCertificate != null;
        }, subApp =>
        {
          subApp.UseHttpsRedirection();
        });
    }



    public static WebsiteWithCertificate GetWebsiteContext(this WebsiteSelector provider, HttpContext context)
    {
      return provider.GetByHostname(context.Request.Host.Host.ToLower().Trim());
    }



    public static WebsiteWithCertificate? GetWebsite(this HttpContext context)
    {
      if (context.Items.TryGetValue(HttpContextKey, out var website))
        return website as WebsiteWithCertificate;
      return default;
    }

    public static void SetWebsite(this HttpContext context, WebsiteWithCertificate website)
    {
      context.Items[HttpContextKey] = website;
    }


  }

  public class WebsiteContextSelectMiddleware
  {
    private readonly RequestDelegate _next;
    private readonly WebsiteSelector _provider;

    public WebsiteContextSelectMiddleware(RequestDelegate next, WebsiteSelector provider)
    {
      _next = next;
      _provider = provider;
    }

    public Task Invoke(HttpContext context)
    {
      context.SetWebsite(_provider.GetWebsiteContext(context));

      return _next.Invoke(context);
    }

  }

  public class WebsiteWithCertificate
  {
    public string Hostname { get; }
    public Website Website { get; }
    public AcmeValidation? AcmeValidation { get; set; }
    public X509Certificate2? ActiveCertificate { get; }
    public SslStreamCertificateContext? CertificateContext { get; set; }

    public FilterKind Filter { get; set; }
    public IPNetwork2::System.Net.IPNetwork2[] FilterAcl { get; set; } = Array.Empty<IPNetwork2::System.Net.IPNetwork2>();

    public WebsiteWithCertificate(Website website)
    {
      Hostname = website.Hostname;
      Website = website;
      Filter = website.Filter;

      var active = website.GetActiveCertificate();

      ActiveCertificate = default;
      if (active == null || !active.IsValid())
        return;

      try
      {
        //https://www.scottbrady91.com/C-Sharp/PEM-Loading-in-dotnet-core-and-dotnet
        if (active.Certificate.IsNotNullOrWhiteSpace())
        {
          ActiveCertificate = X509Certificate2.CreateFromPem(active.Certificate, active.CertificateKey);

          // https://github.com/dotnet/runtime/issues/23749
          // TODO: detect if running in WSL
          if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            ActiveCertificate = new X509Certificate2(ActiveCertificate.Export(X509ContentType.Pkcs12));

          var fullChain = new X509Certificate2Collection();
          fullChain.ImportFromPem(active.Certificate);

          CertificateContext = SslStreamCertificateContext.Create(ActiveCertificate, fullChain, offline: true);
        }
        else if (active.CertificatePfx != null && active.CertificatePfx.Length > 0)
          ActiveCertificate = new X509Certificate2(active.CertificatePfx, WebsiteContextExtensions.PfxPassword);
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }

      FilterAcl = website.FilterAcl.Select(IPNetwork2::System.Net.IPNetwork2.Parse).ToArray();
    }

  }


  public class WebsiteSelector : IDisposable
  {
    private readonly AppServices _services;

    private List<WebsiteWithCertificate> _allWebsites;
    private List<WebsiteWithCertificate> _wildcardWebsites;
    private ReaderWriterLockSlim _websitesLock;
    private TaskCompletionSource<bool> _websitesLoaded;

    public WebsiteSelector(AppServices services)
    {
      _services = services;
      _allWebsites = new List<WebsiteWithCertificate>();
      _wildcardWebsites = new List<WebsiteWithCertificate>();
      _websitesLock = new ReaderWriterLockSlim();
      _websitesLoaded = new TaskCompletionSource<bool>();

    }

    public void Reload(ImmutableDictionary<ShortGuid, Website> websites)
    {
      try
      {
        _websitesLock.EnterWriteLock();

        _allWebsites.Clear();
        foreach (var website in websites.Values)
          _allWebsites.Add(new WebsiteWithCertificate(website));

        _wildcardWebsites.Clear();
        _wildcardWebsites.AddRange(_allWebsites.Where(w => w.Website.Scope is CertificateScope.Wildcard or CertificateScope.WildcardWithApex));
      }
      finally
      {
        _websitesLock.ExitWriteLock();
      }

      _websitesLoaded.TrySetResult(true);
    }

    public WebsiteWithCertificate GetByWebsiteId(ShortGuid id)
    {
      try
      {
        _websitesLock.EnterReadLock();

        return _allWebsites.FirstOrDefault(w => w.Website.Id == id);
      }
      finally
      {
        _websitesLock.ExitReadLock();
      }
    }

    public WebsiteWithCertificate? GetByHostname(string hostname)
    {
      try
      {
        _websitesLock.EnterReadLock();

        var direct = _allWebsites.FirstOrDefault(w => w.Hostname == hostname);
        if (direct != null)
          return direct;

        //TODO: check of ook only 1 label and no more
        var wildcard = _wildcardWebsites.FirstOrDefault(w => hostname.EndsWith($".{w.Hostname}"));
        if (wildcard != null)
          return wildcard;

        return null;
      }
      finally
      {
        _websitesLock.ExitReadLock();
      }
    }



    public async Task Save(WebsiteWithCertificate context, byte[] pfx, X509Certificate2 certificate, string pemCertificate, string pemKey)
    {
      var website = await _services.Website().GetById(context.Website.Id);

      // Add new certificate
      var newCertificate = new Website.CertificateData()
      {
        State = CertificateStates.Active,
        //CertificatePrivateKey = privateKey.ToPem(),
        //Certificate = certificateChain.Certificate.ToPem(),
        CertificatePfx = pfx,
        Certificate = pemCertificate,
        CertificateKey = pemKey,
        ValidFrom = certificate.NotBefore,
        ValidTill = certificate.NotAfter,
      };
      website.Certificates.Add(newCertificate);

      // Remove old certificates
      if (website.Certificates.Count > 3)
      {
        var activeCertificate = website.GetActiveCertificate();
        var certificatesToRemove = website.Certificates
          .Where(c => c != newCertificate)
          .Where(c => c != activeCertificate)
          .OrderByDescending(c => c.ValidTill)
          .Skip(2);

        foreach (var certificateToRemove in certificatesToRemove)
          website.Certificates.Remove(certificateToRemove);
      }

      website.ForceRenew = false;

      await _services.Website().Save(website, Context.Empty);
    }

    public X509Certificate2? SelectCertificate(ConnectionContext context, string sniHostname)
    {
      if (string.IsNullOrWhiteSpace(sniHostname))
        return null;

      var websiteContext = GetByHostname(sniHostname);
      return websiteContext?.ActiveCertificate;
    }

    public Func<SslServerAuthenticationOptions, SslServerAuthenticationOptions> ConfigureOptions { get; set; }

    public async ValueTask<SslServerAuthenticationOptions> SelectTlsAuthContext(TlsHandshakeCallbackContext context)
    {
      var sniHostname = context?.ClientHelloInfo.ServerName;
      if (string.IsNullOrWhiteSpace(sniHostname))
      {
        Console.WriteLine($"SKIP:        {context?.ClientHelloInfo.ServerName} ${context?.Connection?.RemoteEndPoint}");
        throw new AuthenticationException("SniHostname is missing");
      }

      var websiteContext = GetByHostname(sniHostname);

      var filter = websiteContext?.Website.Filter ?? FilterKind.None;
      if (filter != FilterKind.None &&
          context.Connection.RemoteEndPoint is IPEndPoint ipEndPoint)
      {
        var ip = ipEndPoint.Address;
        if (ip.IsIPv4MappedToIPv6)
          ip = ip.MapToIPv4();

        var matched = websiteContext.FilterAcl.Any(acl => acl.Contains(ip));
        if (matched)
        { // acl matched => check for block filter otherwise allow through
          if (filter == FilterKind.BlockAcl)
          {
            Console.WriteLine($"BLOCKED:     {context?.ClientHelloInfo.ServerName} ${context?.Connection?.RemoteEndPoint}");
            throw new AuthenticationException($"{sniHostname} blocked");
          }
          if (filter == FilterKind.BlockAclRedirect)
          {
            Console.WriteLine($"BLOCKED-R:   {context?.ClientHelloInfo.ServerName} ${context?.Connection?.RemoteEndPoint}");
            context.Connection.SetTlsConnectionFilter(new TlsConnectionFilterContext()
            {
              OverrideWebsiteKind = WebsiteKind.FilterRedirect,
            });
          }
        }
        else
        { // acl not matched => block when allow filter otherwise allow through
          if (filter == FilterKind.AllowAcl)
          {
            Console.WriteLine($"DISALLOWED:  {context?.ClientHelloInfo.ServerName} ${context?.Connection?.RemoteEndPoint}");
            throw new AuthenticationException($"{sniHostname} blocked");
          }

          if (filter == FilterKind.AllowAclRedirect)
          {
            Console.WriteLine($"DISALLOWED-R:{context?.ClientHelloInfo.ServerName} ${context?.Connection?.RemoteEndPoint}");
            context.Connection.SetTlsConnectionFilter(new TlsConnectionFilterContext()
            {
              OverrideWebsiteKind = WebsiteKind.FilterRedirect,
            });
          }
        }
      }

      var certificateContext = websiteContext?.CertificateContext;

      if (certificateContext == null)
      {
        // no (valid) certificate for this hostname (e.g. a connection to the
        // bare ip): end the handshake with an AuthenticationException, which
        // Kestrel logs at debug level, instead of continuing without a
        // certificate and triggering an unhandled NotSupportedException error
        Console.WriteLine($"NO-CERT:    {context?.ClientHelloInfo.ServerName} ${context?.Connection?.RemoteEndPoint}");
        throw new AuthenticationException($"No certificate available for {sniHostname}");
      }

      Console.WriteLine($"ACCEPT:     {context?.ClientHelloInfo.ServerName} ${context?.Connection?.RemoteEndPoint}");

      // Custom implementation to support full cert chain (unedited by .net) ivm letsencrypt dual root certs for android
      // https://github.com/dotnet/aspnetcore/issues/21513#issuecomment-914370034
      var options = new SslServerAuthenticationOptions
      {
        // Don't go online to retrieve cert chain
        ServerCertificateContext = certificateContext,
      };

      if (ConfigureOptions != null)
        options = ConfigureOptions(options);

      return options;
    }






    public async Task<AcmeAccount> GetAccount(WebsiteWithCertificate context)
    {
      var account = default(AcmeAccount);
      if (!ShortGuid.IsEmpty(context.Website.AcmeAccount))
        account = await _services.AcmeAccount().GetById(context.Website.AcmeAccount);

      if (account == null)
      {
        var defaultAccount = await _services.AcmeAccount().FindDefault();
        if (defaultAccount == null)
          throw new Exception("Default certificate account not found");

        var website = await _services.Website().GetById(context.Website.Id);
        website.AcmeAccount = defaultAccount.Id;
        await _services.Website().Save(website, Context.Empty);

        account = defaultAccount;
      }

      return account;
    }

    public async Task SetAccount(AcmeAccount account, WebsiteWithCertificate website)
    {
      var acmeAccount = await _services.AcmeAccount().GetById(account.Id);
      acmeAccount.Key = account.Key;
      acmeAccount.AccountUrl = account.AccountUrl;
      await _services.AcmeAccount().Save(acmeAccount, Context.Empty);
    }


    public void Dispose()
    {
    }

  }



}
