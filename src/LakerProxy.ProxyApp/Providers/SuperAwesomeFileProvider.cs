using System.Collections.Generic;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Runtime;
using LakerProxy.Models;
using LakerProxy.ProxyApp.Yarp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace LakerProxy.ProxyApp.Providers
{
  public class SuperAwesomeFileProvider : IFileProvider
  {
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppServices _services;

    private readonly Dictionary<ShortGuid, ValueTask<SuperAwesomeWebsite>> _websites;

    public SuperAwesomeFileProvider(IHttpContextAccessor httpContextAccessor, AppServices services)
    {
      _httpContextAccessor = httpContextAccessor;
      _services = services;

      _websites = new Dictionary<ShortGuid, ValueTask<SuperAwesomeWebsite>>();
    }

    private ValueTask<SuperAwesomeWebsite> GetWebsite()
    {
      var websiteDataId = _httpContextAccessor.HttpContext?.GetWebsite()?.Website.WebsiteData ?? ShortGuid.Empty;
      return GetWebsite(websiteDataId);
    }

    private ValueTask<SuperAwesomeWebsite> GetWebsite(ShortGuid websiteDataId)
    {
      lock (_websites)
      {
        if (_websites.TryGetValue(websiteDataId, out var website))
          return website;

        website = GenerateWebsite();
        _websites[websiteDataId] = website;
        return website;
      }

      async ValueTask<SuperAwesomeWebsite> GenerateWebsite()
      {
        var data = await _services.WebsiteData().GetById(websiteDataId);
        return SuperAwesomeWebsite.Create(data, _httpContextAccessor);
      }
    }

    public async Task Update(ShortGuid websiteDataId, WebsiteData data)
    {
      var website = await GetWebsite(websiteDataId);
      website.Update(data);
    }

    /// <summary>
    /// Existence check without touching the response headers (see SuperAwesomeWebsite.GetFileInfo)
    /// </summary>
    public async ValueTask<bool> FileExists(string subpath)
    {
      var website = await GetWebsite();
      return website.FileExists(subpath);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
      // HACK: Take .Result of Task
      var website = GetWebsite().Result;
      return website.GetDirectoryContents(subpath);
    }

    public IFileInfo GetFileInfo(string subpath)
    {
      // HACK: Take .Result of Task
      var website = GetWebsite().Result;
      return website.GetFileInfo(subpath);
    }

    public IChangeToken Watch(string filter)
    {
      // HACK: Take .Result of Task
      var website = GetWebsite().Result;
      return website.Watch(filter);
    }

  }
}
