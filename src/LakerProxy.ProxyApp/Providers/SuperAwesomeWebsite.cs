using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using LakerProxy.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace LakerProxy.ProxyApp.Providers
{
  public class SuperAwesomeWebsite : IFileProvider
  {
    private readonly IHttpContextAccessor _httpContextAccessor;

    private CancellationTokenSource _changeTokenSource;
    private IChangeToken _changeToken;
    private List<VirtualFile> _files;


    public static SuperAwesomeWebsite Create(WebsiteData data, IHttpContextAccessor httpContextAccessor)
    {
      var result = new SuperAwesomeWebsite(httpContextAccessor);
      result.Update(data);
      return result;
    }

    private SuperAwesomeWebsite(IHttpContextAccessor httpContextAccessor)
    {
      _httpContextAccessor = httpContextAccessor;
    }

    public void Update(WebsiteData website)
    {
      var files = new List<VirtualFile>();
      if (website != null && website.Data != null && website.Data.Length > 0)
      {
        using (var archive = new ZipArchive(
                 stream: new MemoryStream(website.Data),
                 mode: ZipArchiveMode.Read,
                 leaveOpen: false))
        {
          foreach (var entry in archive.Entries)
          {
            files.Add(new VirtualFile(entry, VirtualFileEncoding.None));

            if (entry.IsDirectory())
              continue;

            if (entry.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                || entry.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                || entry.Name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                || entry.Name.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                || entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || entry.Name.EndsWith(".webmanifest", StringComparison.OrdinalIgnoreCase))
            {
              files.Add(new VirtualFile(entry, VirtualFileEncoding.Gzip));
              files.Add(new VirtualFile(entry, VirtualFileEncoding.Brotli));
            }
          }
        }
      }

      _files = files;
      _changeTokenSource?.Cancel();
      _changeTokenSource = new CancellationTokenSource();
      _changeToken = new CancellationChangeToken(_changeTokenSource.Token);
    }



    public IFileInfo GetFileInfo(string subpath)
    {
      var context = _httpContextAccessor.HttpContext;
      var path = subpath.TrimStart('/');

      if (path.EndsWith(".js"))
      {
        if (path.EndsWith("service-worker.js"))
          context.Response.Headers[HeaderNames.CacheControl] = "no-store";
        else
          //https://developers.google.com/web/tools/lighthouse/audits/cache-policy
          context.Response.Headers[HeaderNames.CacheControl] = "max-age=31536000";
      }

      if (path.EndsWith(".png") || path.EndsWith(".jpg") || path.EndsWith(".woff2") || path.EndsWith(".ttf") || path.EndsWith(".eot") ||
          path.EndsWith(".svg"))
      {
        context.Response.Headers[HeaderNames.CacheControl] = "max-age=604800";
      }

      if (path.EndsWith(".html"))
      {
        context.Response.Headers[HeaderNames.CacheControl] = "no-store";
      }

      if (context.Request.Headers.TryGetValue("Accept-Encoding", out var encodings))
      {
        if (encodings.Any(encoding => encoding.Contains("br")))
        {
          var file = GetFile(path + ".br");
          if (file != null && file.Exists)
          {
            context.Response.Headers["X-File"] = "Brotli version served";
            context.Response.Headers[HeaderNames.ContentEncoding] = "br";
            return file;
          }
        }

        if (encodings.Any(encoding => encoding.Contains("gzip")))
        {
          var file = GetFile(path + ".gz");
          if (file != null && file.Exists)
          {
            context.Response.Headers["X-File"] = "Gzip version served";
            context.Response.Headers[HeaderNames.ContentEncoding] = "gzip";
            return file;
          }
        }
      }

      context.Response.Headers["X-File"] = "Default version served";
      return GetFile(path) ?? new NotFoundVirtualFile(path);
    }

    private IFileInfo GetFile(string path)
    {
      return _files.FirstOrDefault(f => f.PhysicalPathInt.Equals(path));
    }

    /// <summary>
    /// True when the zip contains a file (not a directory entry) at this path. Unlike GetFileInfo this has no side effects on the response.
    /// </summary>
    public bool FileExists(string subpath)
    {
      var path = subpath.TrimStart('/');
      return _files.Any(f => !f.IsDirectory && f.PhysicalPathInt.Equals(path));
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
      // todo implement filter files on directory, for directory browsing
      return VirtualDirectory.Empty;
    }

    public IChangeToken Watch(string filter)
    {
      return _changeToken;
    }



    private enum VirtualFileEncoding
    {
      None,
      Gzip,
      Brotli,
    }

    private class VirtualFile : IFileInfo
    {
      private readonly byte[] _data;

      public bool Exists => true;
      public long Length { get; }
      public string PhysicalPathInt { get; }
      string IFileInfo.PhysicalPath => null;
      public string Name { get; }
      public DateTimeOffset LastModified { get; }
      public bool IsDirectory { get; }

      public VirtualFile(ZipArchiveEntry entry, VirtualFileEncoding encoding)
      {
        IsDirectory = entry.IsDirectory();
        if (IsDirectory)
        {
          PhysicalPathInt = entry.FullName.Replace('\\', '/');
          Name = Path.GetFileName(PhysicalPathInt);
          Length = -1;
        }
        else
        {
          if (encoding == VirtualFileEncoding.Brotli)
          {
            Name = entry.Name + ".br";
            PhysicalPathInt = (entry.FullName + ".br").Replace('\\', '/');
            using (var stream = entry.Open())
            {
              using (var ms = new MemoryStream())
              {
                using (var brStream = new BrotliStream(ms, CompressionLevel.Fastest))
                  stream.CopyTo(brStream);
                _data = ms.ToArray();
              }
            }

            Length = _data.LongLength;
          }
          else if (encoding == VirtualFileEncoding.Gzip)
          {
            Name = entry.Name + ".gz";
            PhysicalPathInt = (entry.FullName + ".gz").Replace('\\', '/');
            using (var stream = entry.Open())
            {
              using (var ms = new MemoryStream())
              {
                using (var gzipStream = new GZipStream(ms, CompressionLevel.Fastest))
                  stream.CopyTo(gzipStream);
                _data = ms.ToArray();
              }
            }

            Length = _data.LongLength;
          }
          else
          {
            Name = entry.Name;
            PhysicalPathInt = entry.FullName.Replace('\\', '/');
            using (var stream = entry.Open())
            {
              using (var ms = new MemoryStream())
              {
                stream.CopyTo(ms);
                _data = ms.ToArray();
              }
            }
            Length = _data.LongLength;
          }
        }

        LastModified = entry.LastWriteTime;
      }

      public Stream CreateReadStream()
      {
        if (IsDirectory)
          throw new InvalidOperationException("Cannot create a stream for a directory.");
        return new MemoryStream(_data);
      }

      public override string ToString()
      {
        return PhysicalPathInt;
      }
    }



    private class NotFoundVirtualFile : IFileInfo
    {
      public bool Exists => false;
      public long Length => -1;
      public string PhysicalPath { get; }
      public string Name { get; }
      public DateTimeOffset LastModified => DateTimeOffset.MinValue;
      public bool IsDirectory => false;

      public NotFoundVirtualFile(string path)
      {
        PhysicalPath = path;
        Name = Path.GetFileName(path);
      }

      public Stream CreateReadStream()
      {
        throw new NotImplementedException();
      }
    }



    private class VirtualDirectory : IDirectoryContents
    {
      private readonly IFileInfo[] _files;

      public bool Exists => _files.Length > 0;

      private VirtualDirectory(params IFileInfo[] files)
      {
        _files = files;
      }

      public IEnumerator<IFileInfo> GetEnumerator()
      {
        return ((IEnumerable<IFileInfo>)_files).GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
        return GetEnumerator();
      }

      public static readonly VirtualDirectory Empty = new VirtualDirectory();

    }
  }

  public static class SuperAwesomeFileProviderExtensions
  {
    public static bool IsDirectory(this ZipArchiveEntry entry)
      => string.IsNullOrEmpty(entry.Name);
  }
}
