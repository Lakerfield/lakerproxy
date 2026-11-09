using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    

  }
}
