using System.Net.Http.Headers;
using System.Text.Json;
using dockertest_agent.Models;
using Microsoft.Extensions.Options;

namespace dockertest_agent.Services;

public class GitHubReleaseService
{
    private readonly HttpClient _http;
    private readonly DockerService _docker;
    private readonly AgentOptions _options;
    private readonly ILogger<GitHubReleaseService> _logger;

    public GitHubReleaseService(
        HttpClient http,
        DockerService docker,
        IOptions<AgentOptions> options,
        ILogger<GitHubReleaseService> logger)
    {
        _http = http;
        _docker = docker;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ReleasesResponse> GetCatalogAsync(CancellationToken ct)
    {
        var token = _options.ResolveToken();
        var response = new ReleasesResponse { TokenConfigured = !string.IsNullOrWhiteSpace(token) };
        string? lastError = null;

        var fromReleases = await TryGitHubReleasesAsync(token, ct);
        if (fromReleases.Error != null) lastError = fromReleases.Error;
        if (fromReleases.Items.Count > 0)
            return Finish(response, fromReleases.Items, "github-releases");

        var fromTags = await TryGitHubTagsAsync(token, ct);
        if (fromTags.Error != null) lastError = fromTags.Error;
        if (fromTags.Items.Count > 0)
            return Finish(response, fromTags.Items, "github-tags");

        if (!string.IsNullOrWhiteSpace(token))
        {
            var fromPackages = await TryGitHubPackagesAsync(token, ct);
            if (fromPackages.Error != null) lastError = fromPackages.Error;
            if (fromPackages.Items.Count > 0)
                return Finish(response, fromPackages.Items, "github-packages");
        }

        var fromDocker = await _docker.ListLocalImageTagsAsync(ct);
        if (fromDocker.Count > 0)
        {
            var items = fromDocker.Select(v => new ReleaseVersion
            {
                Version = v,
                Tag = $"v{v}",
                Name = $"Yerel image — {v}",
                PublishedAt = DateTimeOffset.MinValue
            }).ToList();
            response.Hint = "GitHub'dan alınamadı; bu makinedeki image'lar listeleniyor.";
            return Finish(response, items, "docker-local");
        }

        response.Hint = lastError != null
            ? $"GitHub'a ulaşılamadı: {lastError}"
            : "Release bulunamadı. Önce release branch'ine push yap.";
        response.Error = lastError;
        return response;
    }

    private static ReleasesResponse Finish(ReleasesResponse response, List<ReleaseVersion> items, string source)
    {
        response.Items = items;
        response.Source = source;
        return response;
    }

    private async Task<(List<ReleaseVersion> Items, string? Error)> TryGitHubReleasesAsync(string? token, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{_options.GitHubOwner}/{_options.GitHubRepo}/releases?per_page=50";
        var (json, error) = await GetJsonAsync(url, token, ct);
        if (error != null) return ([], error);
        if (json == null) return ([], "Boş yanıt");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<ReleaseVersion>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var tag = item.GetProperty("tag_name").GetString() ?? "";
                var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                if (!TryParseAppVersion(tag, name, out var version))
                    continue;

                list.Add(new ReleaseVersion
                {
                    Tag = tag,
                    Version = version,
                    Name = name ?? tag,
                    PublishedAt = item.TryGetProperty("published_at", out var pub)
                        ? pub.GetDateTimeOffset()
                        : DateTimeOffset.MinValue,
                    IsPrerelease = item.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()
                });
            }

            return (list.OrderByDescending(r => r.PublishedAt).ToList(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Release JSON parse hatası");
            return ([], ex.Message);
        }
    }

    private async Task<(List<ReleaseVersion> Items, string? Error)> TryGitHubTagsAsync(string? token, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{_options.GitHubOwner}/{_options.GitHubRepo}/tags?per_page=50";
        var (json, error) = await GetJsonAsync(url, token, ct);
        if (error != null) return ([], error);
        if (json == null) return ([], "Boş yanıt");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<ReleaseVersion>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var tag = item.GetProperty("name").GetString() ?? "";
                if (!TryParseAppVersion(tag, null, out var version))
                    continue;

                list.Add(new ReleaseVersion
                {
                    Tag = tag,
                    Version = version,
                    Name = $"Tag — {tag}",
                    PublishedAt = DateTimeOffset.MinValue
                });
            }

            return (list, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tag JSON parse hatası");
            return ([], ex.Message);
        }
    }

    private async Task<(List<ReleaseVersion> Items, string? Error)> TryGitHubPackagesAsync(string token, CancellationToken ct)
    {
        var url = $"https://api.github.com/users/{_options.GitHubOwner}/packages/container/{_options.GitHubRepo}/versions?per_page=50";
        var (json, error) = await GetJsonAsync(url, token, ct);
        if (error != null) return ([], error);
        if (json == null) return ([], "Boş yanıt");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var versions = new Dictionary<string, ReleaseVersion>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("metadata", out var meta) ||
                    !meta.TryGetProperty("container", out var container) ||
                    !container.TryGetProperty("tags", out var tags))
                    continue;

                var createdAt = item.TryGetProperty("created_at", out var created)
                    ? created.GetDateTimeOffset()
                    : DateTimeOffset.MinValue;

                foreach (var tagEl in tags.EnumerateArray())
                {
                    var tag = tagEl.GetString() ?? "";
                    if (!AppVersionFilter.IsAppVersion(tag))
                        continue;

                    if (!versions.ContainsKey(tag))
                    {
                        versions[tag] = new ReleaseVersion
                        {
                            Version = tag,
                            Tag = $"v{tag}",
                            Name = $"GHCR — {tag}",
                            PublishedAt = createdAt
                        };
                    }
                }
            }

            return (versions.Values.OrderByDescending(v => v.PublishedAt).ToList(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Package JSON parse hatası");
            return ([], ex.Message);
        }
    }

    private async Task<(string? Json, string? Error)> GetJsonAsync(string url, string? token, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var httpResponse = await _http.SendAsync(request, ct);
            var body = await httpResponse.Content.ReadAsStringAsync(ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub API {Url} → {Status}: {Body}", url, httpResponse.StatusCode, body);
                if (body.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                    return (null, "GitHub rate limit aşıldı. Agent__GitHubToken ile PAT ekle.");
                return (null, $"{(int)httpResponse.StatusCode} — {TryReadGitHubMessage(body)}");
            }

            return (body, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub API isteği başarısız: {Url}", url);
            return (null, ex.Message);
        }
    }

    private static string TryReadGitHubMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString() ?? "API hatası";
        }
        catch { }
        return "API hatası";
    }

    private static bool TryParseAppVersion(string tag, string? releaseName, out string version)
    {
        version = "";
        if (releaseName?.StartsWith("Agent", StringComparison.OrdinalIgnoreCase) == true)
            return false;
        if (tag.StartsWith("agent-", StringComparison.OrdinalIgnoreCase))
            return false;

        version = tag.StartsWith('v') ? tag[1..] : tag;
        return AppVersionFilter.IsAppVersion(version);
    }
}
