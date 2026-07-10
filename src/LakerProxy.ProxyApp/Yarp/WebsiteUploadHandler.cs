using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Runtime;
using Lakerfield.Storage;
using LakerProxy.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LakerProxy.ProxyApp.Yarp
{
  public static class WebsiteUploadHandlerExtensions
  {
    public static IApplicationBuilder UseWebsiteUpload(this IApplicationBuilder builder)
    {
      return builder.UseMiddleware<WebsiteUploadMiddleware>();
    }
  }

  /// <summary>
  /// Handles POST /.well-known/lakerproxy/upload-website on the website's own
  /// hostname: stores the uploaded zip as WebsiteData and points the website to
  /// it. Every invalid request (wrong method, no/invalid token, wrong website)
  /// falls through to the normal pipeline, so the endpoint is invisible to
  /// scanners.
  /// </summary>
  public class WebsiteUploadMiddleware
  {
    public const string UploadPath = "/.well-known/lakerproxy/upload-website";

    private readonly RequestDelegate _next;
    private readonly AppServices _services;

    public WebsiteUploadMiddleware(RequestDelegate next, AppServices services)
    {
      _next = next;
      _services = services;
    }

    public async Task Invoke(HttpContext context)
    {
      if (!HttpMethods.IsPost(context.Request.Method)
          || !context.Request.Path.Equals(UploadPath, StringComparison.OrdinalIgnoreCase)
          || !context.Request.IsHttps)
      {
        await _next(context);
        return;
      }

      var websiteContext = context.GetWebsite();
      if (websiteContext == null)
      {
        await _next(context);
        return;
      }

      var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
      if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("bearer ", StringComparison.OrdinalIgnoreCase))
      {
        await _next(context);
        return;
      }

      var user = await _services.ApiUser().Login(authHeader.Substring(7));
      if (user == null
          || !user.Rights.HasFlag(ApiUserRights.UploadWebsiteData)
          || user.Website != websiteContext.Website.Id)
      {
        await _next(context);
        return;
      }

      // token is valid and bound to this website, from here on errors are reported
      try
      {
        if (!context.Request.HasFormContentType)
        {
          await WriteResult(context, StatusCodes.Status400BadRequest, "Expected multipart/form-data");
          return;
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var file = form.Files["file"];
        if (file == null || file.Length == 0)
        {
          await WriteResult(context, StatusCodes.Status400BadRequest, "Missing form file 'file'");
          return;
        }

        var website = await _services.Website().GetById(websiteContext.Website.Id);
        if (website == null)
        {
          await WriteResult(context, StatusCodes.Status404NotFound, "Website not found");
          return;
        }

        var data = await WebsiteDataUpload.Save(_services, user, website, file);

        await WriteResult(context, StatusCodes.Status200OK,
          $"Uploaded {file.FileName} ({file.Length} bytes) to {website.Hostname}", data.Id);
      }
      catch (InvalidDataException)
      {
        await WriteResult(context, StatusCodes.Status400BadRequest, "File is not a valid zip archive");
      }
      catch (Exception)
      {
        await WriteResult(context, StatusCodes.Status500InternalServerError, "Upload failed");
      }
    }

    private static Task WriteResult(HttpContext context, int statusCode, string message, ShortGuid? websiteDataId = null)
    {
      context.Response.StatusCode = statusCode;
      context.Response.ContentType = "application/json";
      return context.Response.WriteAsJsonAsync(new
      {
        message,
        websiteData = websiteDataId?.ToString(),
      });
    }
  }

  public static class WebsiteDataUpload
  {
    /// <summary>
    /// Stores the uploaded zip as a new WebsiteData document, points the website
    /// to it and removes the previous document when nothing else references it.
    /// Throws InvalidDataException when the file is not a valid zip.
    /// </summary>
    public static async Task<WebsiteData> Save(AppServices services, ApiUser user, Website website, IFormFile file)
    {
      byte[] bytes;
      using (var ms = new MemoryStream())
      {
        await file.CopyToAsync(ms);
        bytes = ms.ToArray();
      }

      // validate the zip before storing it
      using (var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read, leaveOpen: false))
      {
        if (archive.Entries.Count == 0)
          throw new InvalidDataException("Zip archive is empty");
      }

      var dbContext = Context.Empty.Add(user);

      var data = new WebsiteData()
      {
        Name = website.Hostname,
        Date = DateTime.UtcNow,
        Version = file.FileName,
        Data = bytes,
      };
      await services.WebsiteData().Save(data, dbContext);

      var previousId = website.WebsiteData;
      website.WebsiteData = data.Id;
      await services.Website().Save(website, dbContext);

      if (!ShortGuid.IsEmpty(previousId) && previousId != data.Id)
      {
        var websites = await services.Website().GetAll();
        if (!websites.Any(w => w.WebsiteData == previousId))
        {
          var previous = await services.WebsiteData().GetById(previousId);
          if (previous != null)
            await services.WebsiteData().Delete(previous, dbContext);
        }
      }

      return data;
    }
  }
}
