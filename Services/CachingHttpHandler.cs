using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GitAgent.Services;

public class CachingHttpHandler : DelegatingHandler
{
    private readonly string _cacheDirectory;
    private readonly TimeSpan _cacheExpiry;

    public CachingHttpHandler(TimeSpan? cacheExpiry = null) : base(new HttpClientHandler())
    {
        _cacheExpiry = cacheExpiry ?? TimeSpan.FromHours(24);
        _cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".git-agent", "http-cache");

        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Post || request.Content == null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var cacheKey = await GenerateCacheKeyAsync(request);
        var cacheFile = Path.Combine(_cacheDirectory, $"{cacheKey}.json");

        var cachedResponse = await TryGetFromCacheAsync(cacheFile);
        if (cachedResponse != null)
        {
            return cachedResponse;
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            await CacheResponseAsync(cacheFile, response);
        }

        return response;
    }

    private async Task<string> GenerateCacheKeyAsync(HttpRequestMessage request)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append(request.RequestUri?.ToString() ?? "");
        keyBuilder.Append('|');

        if (request.Content != null)
        {
            var body = await request.Content.ReadAsStringAsync();
            keyBuilder.Append(body);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    private async Task<HttpResponseMessage?> TryGetFromCacheAsync(string cacheFile)
    {
        if (!File.Exists(cacheFile))
        {
            return null;
        }

        try
        {
            var fileInfo = new FileInfo(cacheFile);
            if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc > _cacheExpiry)
            {
                File.Delete(cacheFile);
                return null;
            }

            var json = await File.ReadAllTextAsync(cacheFile);
            var cached = JsonSerializer.Deserialize<CachedHttpResponse>(json);

            if (cached == null)
            {
                return null;
            }

            var response = new HttpResponseMessage((HttpStatusCode)cached.StatusCode)
            {
                Content = new StringContent(cached.Content, Encoding.UTF8, cached.ContentType ?? "application/json")
            };

            response.Headers.Add("X-Cache", "HIT");

            return response;
        }
        catch
        {
            return null;
        }
    }

    private async Task CacheResponseAsync(string cacheFile, HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType;

            var cached = new CachedHttpResponse
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = contentType,
                CachedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(cached, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(cacheFile, json);

            response.Content = new StringContent(content, Encoding.UTF8, contentType ?? "application/json");
        }
        catch
        {
        }
    }

    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json"))
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
        }
    }

    public string CacheDirectory => _cacheDirectory;
}

internal class CachedHttpResponse
{
    public int StatusCode { get; set; }
    public string Content { get; set; } = "";
    public string? ContentType { get; set; }
    public DateTime CachedAt { get; set; }
}
