using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Reactive;
using Lakerfield.Runtime;
using LakerProxy.Models;
using LakerProxy.ProxyApp.Yarp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

namespace LakerProxy.ProxyApp.BackgroundServices
{
  using CurrentService = YarpBackgroundService;

  public class YarpBackgroundService : BackgroundService
  {
    private readonly AppServices _services;
    private readonly WebsiteSelector _websiteSelector;
    private readonly LetsEncryptTrigger _letsEncryptTrigger;
    private readonly InMemoryConfigProvider _proxyConfigProvider;
    private readonly ILogger<CurrentService> _logger;

    public YarpBackgroundService(
      AppServices services,
      WebsiteSelector websiteSelector,
      IProxyConfigProvider proxyConfigProvider,
      LetsEncryptTrigger letsEncryptTrigger,
      ILogger<CurrentService> logger)
    {
      _services = services;
      _websiteSelector = websiteSelector;
      _letsEncryptTrigger = letsEncryptTrigger;
      _proxyConfigProvider = (proxyConfigProvider as InMemoryConfigProvider) ?? throw new ArgumentOutOfRangeException(nameof(proxyConfigProvider));
      _logger = logger;

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _logger.LogDebug($"{nameof(CurrentService)} is starting.");

      _logger.LogDebug($"{nameof(CurrentService)} is started.");
      try
      {
        bool websitesLive = false;
        ImmutableDictionary<ShortGuid, Website> websites = ImmutableDictionary<ShortGuid, Website>.Empty;

        var observable = _services.Website().ObserveAll();
        using (var subscription = observable.SubscribeAsync(async x =>
          {
            switch (x)
            {
              case DataReset<Website> dataReset:
                websites = ImmutableDictionary<ShortGuid, Website>.Empty;
                websitesLive = false;
                break;
              
              case DeleteData<Website> deleteData:
                if (websites.ContainsKey(deleteData.Id))
                  websites = websites.Remove(deleteData.Id);
                break;
              
              case DataChange<Website> data:
                if (websites.ContainsKey(data.Id))
                  websites = websites.Remove(data.Id);

                if (data.Entity.Kind != WebsiteKind.Offline)
                  websites = websites.Add(data.Id, data.Entity);
                break;
              
              case DataLive<Website> dataLive:
                websitesLive = true;
                break;
              
              default:
                return;
            }
            
            if (websitesLive)
            {
              UpdateProxy(websites);
              _websiteSelector.Reload(websites);
              _letsEncryptTrigger.Trigger();
            }

          },
          exception =>
          {
            Console.WriteLine($"Error: {exception.Message}");
          },
          () => { },
          TaskPoolScheduler.Default))
        {
          await stoppingToken;
          _logger.LogDebug($"{nameof(CurrentService)} is stopping.");
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
        _logger.LogError($"{nameof(CurrentService)} failed: {e.Message}.");
      }
      _logger.LogDebug($"{nameof(CurrentService)} is stopped.");
    }

    private void UpdateProxy(ImmutableDictionary<ShortGuid, Website> websites)
    {
      var routes = new List<RouteConfig>();
      var clusters = new List<ClusterConfig>();

      foreach (var website in websites.Values)
      {
        switch (website.Kind)
        {
          case WebsiteKind.Forward:
          case WebsiteKind.ForwardWithOriginalHostHeader:
            break;

          case WebsiteKind.Redirect:
            break;
          
          case WebsiteKind.NotFound:
            break;
          
          case WebsiteKind.StaticSite:
            break;

          case WebsiteKind.Info:
            break;

          default:
          case WebsiteKind.Offline:
            continue;
        }

        if (string.IsNullOrWhiteSpace(website.Destination))
          continue;

        var hosts = new List<string>();
        switch (website.Scope)
        {
          case CertificateScope.None:
          case CertificateScope.SingleDomain:
          case CertificateScope.WildcardWithApex:
            hosts.Add(website.Hostname);
            break;
        }
        switch (website.Scope)
        {
          case CertificateScope.Wildcard:
          case CertificateScope.WildcardWithApex:
            hosts.Add($"*.{website.Hostname}");
            break;
        }

        routes.Add(TransformConfig(website, new RouteConfig()
        {
          RouteId = $"route-{website.Id}",
          ClusterId = $"cluster-{website.Id}",
          Match = new RouteMatch
          {
            Hosts = hosts,
            Path = "{**catch-all}"
          }
        }));

        clusters.Add(new ClusterConfig()
        {
          ClusterId = $"cluster-{website.Id}",
          HttpRequest = GetHttpRequestConfig(website.DestinationProtocol),
          HttpClient = GetHttpClientConfig(website.DestinationProtocol),
          Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
          {
            { $"destination-{website.Id}", new DestinationConfig() { Address = website.Destination } }
          }
        });

      }

      _proxyConfigProvider.Update(routes, clusters);

      RouteConfig TransformConfig(Website website, RouteConfig config)
      {
        switch (website.Kind)
        {
          case WebsiteKind.ForwardWithOriginalHostHeader:
            return config.WithTransformUseOriginalHostHeader(useOriginal: true);

          default:
            return config;
        }
      }

      HttpClientConfig? GetHttpClientConfig(DestinationProtocol protocol)
      {
        switch (protocol)
        {
          case DestinationProtocol.Http2:
          case DestinationProtocol.Http3:
            return new()
            {
              DangerousAcceptAnyServerCertificate = true,
              EnableMultipleHttp2Connections = true,
            };

          default:
          case DestinationProtocol.Default:
            return null;
        }
      }

      ForwarderRequestConfig? GetHttpRequestConfig(DestinationProtocol protocol)
      {
        switch (protocol)
        {
          case DestinationProtocol.Http2:
            return  new()
            {
              ActivityTimeout = TimeSpan.FromHours(2),
              Version = new Version(2, 0),
              VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            };

          case DestinationProtocol.Http3:
            return new()
            {
              ActivityTimeout = TimeSpan.FromHours(2),
              Version = new Version(3, 0),
              VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            };

          case DestinationProtocol.Http10:
            return new()
            {
              Version = new Version(1, 0),
              VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            };

          case DestinationProtocol.Http11:
            return  new()
            {
              Version = new Version(1, 1),
              VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            };

          default:
          case DestinationProtocol.Default:
            return null;
        }

      }
    }

  }
}
