using System.Text.Json;
using dockertest_agent.Models;
using Microsoft.Extensions.Options;

namespace dockertest_agent.Services;

public class CatalogReleaseService
{
    private readonly HttpClient _http;
    private readonly AgentOptions _options;
    private readonly ILogger<CatalogReleaseService> _logger;

    public CatalogReleaseService(
        HttpClient http,
        IOptions<AgentOptions> options,
        ILogger<CatalogReleaseService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ReleasesResponse> GetCatalogAsync(CancellationToken ct)
    {
        var baseUrl = _options.CatalogUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/releases";

        try
        {
            using var response = await _http.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Katalog {Url} → {Status}: {Body}", url, response.StatusCode, body);
                return new ReleasesResponse
                {
                    Source = "catalog",
                    Error = $"{(int)response.StatusCode} — katalog okunamadı",
                    Hint = $"CatalogUrl kontrol et: {_options.CatalogUrl}"
                };
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var items = new List<ReleaseVersion>();

            if (root.TryGetProperty("items", out var itemsEl))
            {
                foreach (var item in itemsEl.EnumerateArray())
                {
                    var version = item.TryGetProperty("version", out var ver) ? ver.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(version))
                        continue;

                    items.Add(new ReleaseVersion
                    {
                        Version = version,
                        Tag = item.TryGetProperty("tag", out var tag) ? tag.GetString() ?? $"v{version}" : $"v{version}",
                        Name = item.TryGetProperty("name", out var name) ? name.GetString() ?? version : version,
                        DownloadUrl = ResolveDownloadUrl(item.TryGetProperty("downloadUrl", out var dl) ? dl.GetString() : null),
                        PublishedAt = item.TryGetProperty("publishedAt", out var pub) && pub.ValueKind == JsonValueKind.String
                            ? pub.GetDateTimeOffset()
                            : DateTimeOffset.MinValue
                    });
                }
            }

            var hint = root.TryGetProperty("hint", out var hintEl) ? hintEl.GetString() : null;
            return new ReleasesResponse
            {
                Items = items,
                Source = root.TryGetProperty("source", out var src) ? src.GetString() ?? "catalog" : "catalog",
                Hint = hint
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Katalog isteği başarısız: {Url}", url);
            return new ReleasesResponse
            {
                Source = "catalog",
                Error = ex.Message,
                Hint = $"Catalog servisine ulaşılamadı: {_options.CatalogUrl}"
            };
        }
    }

    private string? ResolveDownloadUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        return $"{_options.CatalogUrl.TrimEnd('/')}/{url.TrimStart('/')}";
    }
}
