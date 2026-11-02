using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yarp.ReverseProxy.Configuration;
using Lakerfield.Acme;
using Lakerfield.Runtime;
using Lakerfield.Settings;
using LakerProxy.Settings;
using LakerProxy.ProxyApp.Providers;
using LakerProxy.ProxyApp.Routing.Constraints;
using LakerProxy.ProxyApp.Security;
using LakerProxy.ProxyApp.Yarp;

namespace LakerProxy.ProxyApp
{
  public class Startup
  {
    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
      var settings = Program.Runtime.Services.Settings().LakerProxy();

      //var routes = new[]
      //{
      //  new RouteConfig()
      //  {
      //    RouteId = "route1",
      //    ClusterId = "cluster1",
      //    Match = new RouteMatch
      //    {
      //      Hosts = new []{ "api.lakerfield.net"},
      //      Path = "{**catch-all}"
      //    }
      //  }
      //};
      //var clusters = new[]
      //{
      //  new ClusterConfig()
      //  {
      //    ClusterId = "cluster1",
      //    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
      //    {
      //      { "destination1", new DestinationConfig() { Address = "https://lakerfield.net" } }
      //    }
      //  }
      //};

      services.AddReverseProxy()
        //.LoadFromMemory(routes, clusters);
        .LoadFromMemory(Array.Empty<RouteConfig>(), Array.Empty<ClusterConfig>());

      services.AddRouting(options =>
      {
        options.ConstraintMap["shortguid"] = typeof(ShortGuidRouteContraint);
      });

      services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
          //options.SuppressConsumesConstraintForFormFileParameters = true;
          //options.SuppressInferBindingSourcesForParameters = true;
          //options.SuppressModelStateInvalidFilter = true;
          //options.SuppressMapClientErrors = true;
          //options.ClientErrorMapping[404].Link = "https://httpstatuses.com/404";
        })
        .AddJsonOptions(options => {
          options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
          //options.JsonSerializerOptions.IgnoreNullValues = true;
        });

#if DEBUG
      services.AddSwaggerGen(c =>
      {
        // Your custom configuration
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
          Title = "LakerProxy API",
          Version = "v1",
          Contact = new Microsoft.OpenApi.Models.OpenApiContact()
          {
            Name = "Michael Lakerveld",
            Url = new Uri("https://lakerfield.net/"),
          }
          //TermsOfService = ""
        });

        //c.DescribeAllEnumsAsStrings();

        // if you're using the SecurityRequirementsOperationFilter, you also need to tell Swashbuckle you're using OAuth2
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
          Description = "Standard Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
          In = Microsoft.OpenApi.Models.ParameterLocation.Header,
          Name = "Authorization",
          Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
        });

        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
        {
          {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
              Reference = new Microsoft.OpenApi.Models.OpenApiReference
              {
                Id = "Bearer", //The name of the previously defined security scheme.
                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme
              }
            },
            new List<string>()
          }
        });

        c.OperationFilter<SwaggerLimitContentTypeFilter>();
        c.OperationFilter<SwaggerSetObsoleteFilter>();
      });
#endif

      services.AddMaliciousIpFilter(options =>
      {
        options.BlockedStatusCode = StatusCodes.Status403Forbidden;
        options.RuntimeCleanupInterval = TimeSpan.FromMinutes(1);

        options.BlocklistFeedUrls.AddRange(settings.BlocklistFeeds);
        options.BlocklistFeedRefreshInterval = TimeSpan.FromMinutes(settings.BlocklistRefreshMinutes);
        options.AllowList.AddRange(settings.BlocklistAllowList);
      });

      services.AddAuthFailureTracking(options =>
      {
        options.MaxFailures = 3;
        options.FailureWindow = TimeSpan.FromMinutes(5);
        options.BlockDuration = TimeSpan.FromMinutes(65);
      });

      services.AddHttpContextAccessor();
      services.AddSingleton<SuperAwesomeFileProvider>();

      services.AddSingleton<BackgroundServices.LetsEncryptTrigger>();
      if (Program.Server.Services.HasFlag(ServerServices.Proxy))
        services.AddHostedService<BackgroundServices.YarpBackgroundService>();
      if (Program.Server.Services.HasFlag(ServerServices.LetsEncrypt) && !string.IsNullOrWhiteSpace(Program.Server.AcmeChallengeZone))
      {
        services.AddHostedService<BackgroundServices.LetsEncryptBackgroundService>();
        services.AddAcmeDnsServer(options =>
        {
          options.ZoneName = Program.Server.AcmeChallengeZone;
        });
        // log incoming dns-01 validation queries to the acme log
        services.AddOptions<AcmeDnsServerOptions>()
          .PostConfigure<AppServices>((options, appServices) =>
          {
            options.OnTxtQuery = (qname, remoteEndPoint, found) =>
            {
              var log = appServices.AcmeLog().New(qname, "V", $"dns-01 TXT query from {remoteEndPoint.Address} => {(found ? "ok" : "not found")}");
              _ = appServices.AcmeLog().Insert(log);
            };
          });
      }
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
      }

      app.UseMaliciousIpFilter();
      app.UseMiddleware<SuspiciousRequestMiddleware>();

      app.UseWebsiteSelector();

      app.UseMaxRequestBodySize();

      app.UseWebsiteUpload();

      // Static-site branch: UseStaticFiles is middleware and cannot be called per
      // request, so build a sub-pipeline once and hand its RequestDelegate to
      // MapWebsiteKind.
      var staticSiteFiles = app.ApplicationServices.GetRequiredService<SuperAwesomeFileProvider>();
      var staticSiteApp = app.New();
      staticSiteApp.UseStaticSiteDefaultDocument(staticSiteFiles);
      staticSiteApp.UseStaticFiles(new StaticFileOptions()
      {
        FileProvider = staticSiteFiles,
        ServeUnknownFileTypes = true,
        HttpsCompression = HttpsCompressionMode.DoNotCompress,
      });
      staticSiteApp.RunStaticSiteNotFound(staticSiteFiles);
      var staticSiteBranch = staticSiteApp.Build();

      app.MapWebsiteKind(

        (WebsiteKind.StaticSite, staticSiteBranch),

        (WebsiteKind.Redirect, async (context) => {
          var website = context.GetWebsite();
          if (string.IsNullOrWhiteSpace(website.Website.Destination))
            return;
          context.Response.Redirect(website.Website.Destination, false);
        }),

        (WebsiteKind.RedirectPermanent, async (context) => {
            var website = context.GetWebsite();
            if (string.IsNullOrWhiteSpace(website.Website.Destination))
              return;
            context.Response.Redirect(website.Website.Destination, true);
          }
        ),

        (WebsiteKind.FilterRedirect, async (context) => {
            var website = context.GetWebsite();
            if (!string.IsNullOrWhiteSpace(website?.Website.FilterDestination))
              context.Response.Redirect(website.Website.FilterDestination, false);
            else
              context.Response.StatusCode = 404;
          }
        ),

        (WebsiteKind.NotFound, async (context) => {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("404 - The requested page or endpoint could not be found.");
          }),

        (WebsiteKind.Offline, async (context) => {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("404 - The requested page or endpoint could not be found.");
          }),

        (WebsiteKind.Info, async (context) => {
            //await context.Response.WriteAsync("<html><body><h1>Uw ip adres is: ");
            await context.Response.WriteAsync($"{context.Connection?.RemoteIpAddress?.ToString().Replace("::ffff:", "")}");
            //await context.Response.WriteAsync("</h1></body></html>");
          }),

        (WebsiteKind.InfoExtended, async (context) => {
            await context.Response.WriteAsync($"Hello {context.Connection?.RemoteIpAddress?.ToString().Replace("::ffff:", "")} the proxy server is alive. Timestamp={DateTime.Now:O}; Host={Environment.MachineName}; GitHash={EnvironmentHelper.GitHash}");
            //await context.Response.WriteAsync($"Hello {context.Connection?.RemoteIpAddress?.ToString().Replace("::ffff:", "") ?? "World"} through a Proxy! It is {DateTime.Now.ToString("O")} @ {Environment.MachineName} running on version {Environment.Version} and sdk {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} ({System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}) from git {EnvironmentHelper.GitHash}");
          })
      );

      //app.UseRejectWebsocketOverHttp2WhileUnsupported();

      app.UseRouting();

      app.UseEndpoints(endpoints =>
      {
        endpoints.MapReverseProxy();
        endpoints.MapControllers();
      });

#if DEBUG
      app.UseSwagger();
      app.UseSwaggerUI(c =>
      {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "LakerProxy-API-v1");
      });
#endif

      app.Run(async context =>
      {
#if DEBUG
        await context.Response.WriteAsync($"Proxy server alive. No upstream configured. Timestamp={DateTime.Now:O}; Host={Environment.MachineName}; GitHash={EnvironmentHelper.GitHash}");
#else
        await context.Response.WriteAsync($"Proxy server alive. No upstream configured. Timestamp={DateTime.Now:O}; Host={Environment.MachineName}");
#endif
      });

    }
  }
}
