using System;
using System.Linq;
using Lakerfield.Runtime;
using LakerProxy.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LakerProxy.ProxyApp.Yarp
{
  public static class AcmeHandlerExtensionsExtensions
  {

    public static IApplicationBuilder UseAcmeServerCertificate(this IApplicationBuilder app)
    {
      var serverCertificateProvider = app.ApplicationServices.GetService<WebsiteSelector>();
      if (serverCertificateProvider == null)
        throw new InvalidOperationException("Unable to find the required services. Please add 'IServiceCollection.AddAcmeServerCertificate' inside 'ConfigureServices(...)' in the application startup code.");

      app.MapWhen(context =>
        !context.Request.IsHttps &&
        context.Request.Path.StartsWithSegments(@"/.well-known/acme-challenge"), subApp =>
      {
        subApp.Run(async context =>
        {
          var requestedToken = context.Request.Path.ToString().Replace("/.well-known/acme-challenge/", "");
          var website = context.GetWebsite();
          var challenge = website?.AcmeValidation?.Validate(requestedToken);

          // log incoming http-01 validation requests to the acme log, but only
          // while a validation is pending to keep scanner noise out
          if (website?.AcmeValidation != null)
          {
            var services = context.RequestServices.GetService<AppServices>();
            if (services != null)
            {
              var ip = context.Connection.RemoteIpAddress?.ToString().Replace("::ffff:", "");
              var log = services.AcmeLog().New(context.Request.Host.Host, "V", $"http-01 validation request for token '{requestedToken}' from {ip} => {(string.IsNullOrWhiteSpace(challenge) ? "unknown token" : "ok")}");
              await services.AcmeLog().Insert(log);
            }
          }

          if (!string.IsNullOrWhiteSpace(challenge))
          {
            //context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(challenge);
          }
          else
            context.Response.StatusCode = 404;
        });
      });

      return app;
    }

    public static Website.CertificateData? GetActiveCertificate(this Website website)
    {
      var now = DateTime.Now;

      var certificate = website.Certificates
        .Where(c => c.State == CertificateStates.Active)
        .Where(c => c.ValidFrom < now)
        .OrderByDescending(c => c.ValidTill)
        .FirstOrDefault();

      if (certificate == null)
        return website.Certificates.FirstOrDefault();

      return certificate;
    }

    public static bool IsValid(this Website.CertificateData certificateData)
    {
      if (certificateData.State != CertificateStates.Active)
        return false;

      if (string.IsNullOrWhiteSpace(certificateData.Certificate)
          && certificateData.CertificatePfx != null 
          && certificateData.CertificatePfx.Length == 0)
        return false;

      return true;
    }

  }
}
