using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LakerProxy.WebApi.Models;

namespace LakerProxy.WebApi
{
  public class LakerProxyApiClient
  {
    public const string EmptyId = "AAAAAAAAAAAAAAAAAAAAAA";

    private readonly HttpClient _httpClient;
    private readonly LakerProxyApiOptions _options;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private HttpClient? _streamingHttpClient;


    public LakerProxyApiClient(LakerProxyApiOptions options, HttpClient? httpClient = null)
    {
      _options = options ?? throw new ArgumentNullException(nameof(options));

      _httpClient = httpClient ?? new HttpClient(new HttpClientHandler()
      {
        AllowAutoRedirect = false,
      });

      if (!string.IsNullOrWhiteSpace(options.ApiBaseUrl))
        _httpClient.BaseAddress = new Uri(options.ApiBaseUrl);
      if (!string.IsNullOrWhiteSpace(options.ApiKey))
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", options.ApiKey);

      _jsonSerializerOptions = new JsonSerializerOptions();
      _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
      _jsonSerializerOptions.PropertyNameCaseInsensitive = true;
    }

    /// <summary>
    /// Separate client for long-lived streaming responses (SSE): the default
    /// HttpClient timeout of 100 seconds would abort an open stream.
    /// </summary>
    private HttpClient StreamingHttpClient
    {
      get
      {
        if (_streamingHttpClient != null)
          return _streamingHttpClient;

        var client = new HttpClient(new HttpClientHandler()
        {
          AllowAutoRedirect = false,
        })
        {
          Timeout = Timeout.InfiniteTimeSpan,
        };
        if (!string.IsNullOrWhiteSpace(_options.ApiBaseUrl))
          client.BaseAddress = new Uri(_options.ApiBaseUrl);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
          client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", _options.ApiKey);

        return _streamingHttpClient = client;
      }
    }


    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
      var request = new HttpRequestMessage(method, url);
      return request;
    }

    private HttpRequestMessage CreateRequest<T>(HttpMethod method, string url, T data)
    {
      var request = CreateRequest(method, url);
      request.Content = data.AsJsonStringContent(_jsonSerializerOptions);
      return request;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, byte[] data)
    {
      var request = CreateRequest(method, url);

      var multiPart = new MultipartFormDataContent();
      var fileContent = new ByteArrayContent(data);
      fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
      multiPart.Add(fileContent, "file", "photo.jpg");

      request.Content = multiPart;
      return request;
    }

    #region ApiUser

    public async Task<IReadOnlyCollection<ApiUser>> ApiUserGet()
    {
      var request = CreateRequest(HttpMethod.Get, "/api/v1/apiuser");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
      var results = await response.Content.ReadFromJsonAsync<List<ApiUser>>(_jsonSerializerOptions);
      return results;
    }

    public async Task<ApiUser> ApiUserGetById(string id)
    {
      var request = CreateRequest(HttpMethod.Get, $"/api/v1/apiuser/{id}");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
      var result = await response.Content.ReadFromJsonAsync<ApiUser>(_jsonSerializerOptions);
      return result;
    }

    public async Task<ApiUser> ApiUserPost(ApiUser apiUser)
    {
      var request = CreateRequest(HttpMethod.Post, $"/api/v1/apiuser", apiUser);

      var response = await _httpClient.SendAsync(request);
      if (!response.IsSuccessStatusCode)
        throw new WebException(await response.Content.ReadAsStringAsync());
      response.EnsureSuccessStatusCode();
      var result = await response.Content.ReadFromJsonAsync<ApiUser>(_jsonSerializerOptions);
      return result;
    }

    public async Task<ApiUser> ApiUserPut(ApiUser apiUser)
    {
      var request = CreateRequest(HttpMethod.Put, $"/api/v1/apiuser/{apiUser.Id}", apiUser);

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
      var result = await response.Content.ReadFromJsonAsync<ApiUser>(_jsonSerializerOptions);
      return result;
    }

    public async Task ApiUserDelete(string id)
    {
      var request = CreateRequest(HttpMethod.Delete, $"/api/v1/apiuser/{id}");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
    }

    #endregion

    #region AcmeAccount

    public async Task<IReadOnlyCollection<AcmeAccount>> AcmeAccountGet()
    {
      var request = CreateRequest(HttpMethod.Get, "/api/v1/acmeaccount");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
      var results = await response.Content.ReadFromJsonAsync<List<AcmeAccount>>(_jsonSerializerOptions);
      return results;
    }

    public async Task<AcmeAccount> AcmeAccountGetById(string id)
    {
      var request = CreateRequest(HttpMethod.Get, $"/api/v1/acmeaccount/{id}");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
      var result = await response.Content.ReadFromJsonAsync<AcmeAccount>(_jsonSerializerOptions);
      return result;
    }

    public async Task<AcmeAccount> AcmeAccountPost(AcmeAccount acmeAccount)
    {
      var request = CreateRequest(HttpMethod.Post, $"/api/v1/acmeaccount", acmeAccount);

      var response = await _httpClient.SendAsync(request);
      if (!response.IsSuccessStatusCode)
        throw new WebException(await response.Content.ReadAsStringAsync());
      response.EnsureSuccessStatusCode();
      var result = await response.Content.ReadFromJsonAsync<AcmeAccount>(_jsonSerializerOptions);
      return result;
    }

    public async Task<AcmeAccount> AcmeAccountPut(AcmeAccount acmeAccount)
    {
      var request = CreateRequest(HttpMethod.Put, $"/api/v1/acmeaccount/{acmeAccount.Id}", acmeAccount);

      var response = await _httpClient.SendAsync(request);
      if (!response.IsSuccessStatusCode)
        throw new WebException(await response.Content.ReadAsStringAsync());
      response.EnsureSuccessStatusCode();
      var result = await response.Content.ReadFromJsonAsync<AcmeAccount>(_jsonSerializerOptions);
      return result;
    }

    #endregion

    #region Certificate

    public async Task<Certificate> CertificateGetByName(string domain, string name)
    {
      var request = CreateRequest(HttpMethod.Get, $"/api/v1/certificate/{domain}/{name}");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
      var result = await response.Content.ReadFromJsonAsync<Certificate>(_jsonSerializerOptions);
      return result;
    }

    public async Task<Certificate> WebsiteCertificateGetById(string id)
    {
      var request = CreateRequest(HttpMethod.Get, $"/api/v1/websitecertificate/{id}");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
      var result = await response.Content.ReadFromJsonAsync<Certificate>(_jsonSerializerOptions);
      return result;
    }

    #endregion

    #region Website

    public async Task<IReadOnlyCollection<Website>> WebsiteGet()
    {
      var request = CreateRequest(HttpMethod.Get, "/api/v1/website");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
      var results = await response.Content.ReadFromJsonAsync<List<Website>>(_jsonSerializerOptions);
      return results;
    }

    public async Task<Website> WebsiteGetById(string id)
    {
      var request = CreateRequest(HttpMethod.Get, $"/api/v1/website/{id}");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
      var result = await response.Content.ReadFromJsonAsync<Website>(_jsonSerializerOptions);
      return result;
    }

    public async Task<Website> WebsitePost(Website website)
    {
      var request = CreateRequest(HttpMethod.Post, $"/api/v1/website", website);

      var response = await _httpClient.SendAsync(request);
      if (!response.IsSuccessStatusCode)
        throw new WebException(await response.Content.ReadAsStringAsync());
      response.EnsureSuccessStatusCode();
      var result = await response.Content.ReadFromJsonAsync<Website>(_jsonSerializerOptions);
      return result;
    }

    public async Task<Website> WebsitePut(Website website)
    {
      var request = CreateRequest(HttpMethod.Put, $"/api/v1/website/{website.Id}", website);

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
      var result = await response.Content.ReadFromJsonAsync<Website>(_jsonSerializerOptions);
      return result;
    }

    public async Task WebsiteDelete(string id)
    {
      var request = CreateRequest(HttpMethod.Delete, $"/api/v1/website/{id}");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
    }

    #endregion

    #region Log

    public async Task<IReadOnlyCollection<LogEntry>> LogGet(DateTime? dateFrom = null, DateTime? dateTill = null, int take = 200)
    {
      var query = $"?take={take}";
      if (dateFrom.HasValue)
        query += $"&dateFrom={Uri.EscapeDataString(dateFrom.Value.ToString("O"))}";
      if (dateTill.HasValue)
        query += $"&dateTill={Uri.EscapeDataString(dateTill.Value.ToString("O"))}";

      var request = CreateRequest(HttpMethod.Get, $"/api/v1/log{query}");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
      var results = await response.Content.ReadFromJsonAsync<List<LogEntry>>(_jsonSerializerOptions);
      return results;
    }

    /// <summary>
    /// Opens the server-sent events stream and yields log entries until the
    /// connection drops or the token is cancelled. Reconnecting is up to the caller.
    /// </summary>
    public async IAsyncEnumerable<LogEntry> LogStream(
      int history = 100,
      [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var request = CreateRequest(HttpMethod.Get, $"/api/v1/log/stream?history={history}");

      using var response = await StreamingHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
      response.EnsureSuccessStatusCode();

      using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
      using var reader = new StreamReader(stream);

      string? dataBuffer = null;
      while (!cancellationToken.IsCancellationRequested)
      {
        var line = await reader.ReadLineAsync(cancellationToken);
        if (line == null)
          yield break; // server closed the stream

        if (line.Length == 0)
        {
          // blank line = dispatch buffered event
          if (dataBuffer == null)
            continue;
          var entry = JsonSerializer.Deserialize<LogEntry>(dataBuffer, _jsonSerializerOptions);
          dataBuffer = null;
          if (entry != null)
            yield return entry;
          continue;
        }

        if (line[0] == ':')
          continue; // comment / keep-alive

        if (line.StartsWith("data:", StringComparison.Ordinal))
        {
          var payload = line.Substring(5).TrimStart();
          dataBuffer = dataBuffer == null ? payload : dataBuffer + "\n" + payload;
        }
        // other fields (id:, event:, retry:) are ignored for now
      }
    }

    #endregion

    #region AcmeLog

    public async Task<IReadOnlyCollection<AcmeLogEntry>> AcmeLogGet(DateTime? dateFrom = null, DateTime? dateTill = null, string? domain = null, int take = 200)
    {
      var query = $"?take={take}";
      if (dateFrom.HasValue)
        query += $"&dateFrom={Uri.EscapeDataString(dateFrom.Value.ToString("O"))}";
      if (dateTill.HasValue)
        query += $"&dateTill={Uri.EscapeDataString(dateTill.Value.ToString("O"))}";
      if (!string.IsNullOrWhiteSpace(domain))
        query += $"&domain={Uri.EscapeDataString(domain)}";

      var request = CreateRequest(HttpMethod.Get, $"/api/v1/acmelog{query}");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
      var results = await response.Content.ReadFromJsonAsync<List<AcmeLogEntry>>(_jsonSerializerOptions);
      return results;
    }

    /// <summary>
    /// Opens the server-sent events stream and yields acme log entries until the
    /// connection drops or the token is cancelled. Reconnecting is up to the caller.
    /// </summary>
    public async IAsyncEnumerable<AcmeLogEntry> AcmeLogStream(
      int history = 100,
      string? domain = null,
      [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var query = $"?history={history}";
      if (!string.IsNullOrWhiteSpace(domain))
        query += $"&domain={Uri.EscapeDataString(domain)}";

      var request = CreateRequest(HttpMethod.Get, $"/api/v1/acmelog/stream{query}");

      using var response = await StreamingHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
      response.EnsureSuccessStatusCode();

      using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
      using var reader = new StreamReader(stream);

      string? dataBuffer = null;
      while (!cancellationToken.IsCancellationRequested)
      {
        var line = await reader.ReadLineAsync(cancellationToken);
        if (line == null)
          yield break; // server closed the stream

        if (line.Length == 0)
        {
          // blank line = dispatch buffered event
          if (dataBuffer == null)
            continue;
          var entry = JsonSerializer.Deserialize<AcmeLogEntry>(dataBuffer, _jsonSerializerOptions);
          dataBuffer = null;
          if (entry != null)
            yield return entry;
          continue;
        }

        if (line[0] == ':')
          continue; // comment / keep-alive

        if (line.StartsWith("data:", StringComparison.Ordinal))
        {
          var payload = line.Substring(5).TrimStart();
          dataBuffer = dataBuffer == null ? payload : dataBuffer + "\n" + payload;
        }
        // other fields (id:, event:, retry:) are ignored for now
      }
    }

    #endregion


  }
}
