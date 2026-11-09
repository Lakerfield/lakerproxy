using System;
using System.Security.Authentication;
using System.Threading.Tasks;
using Lakerfield.Runtime;
using LakerProxy.Models;
using LakerProxy.ProxyApp.Yarp;
using LakerProxy.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LakerProxy.ProxyApp
{
  public class Program
  {
    public const long MaxRequestBodySize = 128L * 1024 * 1024;
    /// <summary>
    /// Kestrel defaults to 30,000,000 bytes, which is approximately 28.6MB.
    /// </summary>
    public const long DefaultRequestBodySize = 30_000_000;

    public static AppRuntime Runtime { get; private set; }
    public static Server Server { get; set; }

    public static async Task Main(string[] args)
    {
      var builder = new AppRuntimeBuilder(await LakerProxyRuntime.CreateOptions("ProxyApp"));
      builder.ConfigureModules(c => c
        .AddBson()
        .AddMongoDb()
        .AddLakerProxy());

      var runtime = await builder.Build();
      Runtime = runtime;
      Server = await Runtime.Services.Server().FindOrCreate(Environment.MachineName);
      
      var host = Host
        .CreateDefaultBuilder(args)
        .UseSystemd()
        
        .ConfigureLogging((context, builder) =>
        {
          builder.AddConsole();
        })
        .ConfigureWebHostDefaults(webBuilder =>
        {
          webBuilder
            .ConfigureServices(s => s
              .AddRuntime(Runtime))

            .AddWebsiteSelector()
            
            .UseKestrel(options =>
            {
              var websiteContextProvider = options.ApplicationServices.GetService<WebsiteSelector>();
              if (websiteContextProvider == null)
                throw new ArgumentNullException("missing required WebsiteContextProvider");

              websiteContextProvider.ConfigureOptions = options =>
              {
                options.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                return options;
              };

              options.AddServerHeader = false;
              options.Limits.MaxRequestBodySize = MaxRequestBodySize;
              options.ListenAnyIP(80, configure =>
              {
                configure.Protocols = HttpProtocols.Http1;
              });
              options.ListenAnyIP(443, configure =>
              {
                configure.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                //configure.UseHttps(httpsOptions =>
                //{
                //  httpsOptions.ServerCertificateSelector = websiteContextProvider.SelectCertificate;
                //  httpsOptions.SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
                //});
                configure.UseHttps(new TlsHandshakeCallbackOptions
                {
                  // Custom implementation to support full cert chain (unedited by .net) ivm letsencrypt dual root certs for android
                  // https://github.com/dotnet/aspnetcore/issues/21513#issuecomment-914370034
                  OnConnection = websiteContextProvider.SelectTlsAuthContext,
                });
              });
            });
          webBuilder.UseStartup<Startup>();
        })
        .Build();

      // https://github.com/davidfowl/MultiProtocolAspNetCore/blob/master/KestrelTcpDemo/MyEchoConnectionHandler.cs

      await using (Runtime)
        await host.RunAsync();
    }

  }
}
