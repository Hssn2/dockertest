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

        var fromReleases = await TryGitHubReleasesAsync(token, ct);
        if (fromReleases.Count > 0)
        {
            response.Items = fromReleases;
            response.Source = "github-releases";
            return response;
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            var fromPackages = await TryGitHubPackagesAsync(token, ct);
            if (fromPackages.Count > 0)
            {
                response.Items = fromPackages;
                response.Source = "github-packages";
                return response;
            }
        }

        var fromDocker = await _docker.ListLocalImageTagsAsync(ct);
        if (fromDocker.Count > 0)
        {
            response.Items = fromDocker
                .Select(v => new ReleaseVersion
                {
                    Version = v,
                    Tag = $"v{v}",
                    Name = $"Yerel image — {v}",
                    PublishedAt = DateTimeOffset.MinValue
                })
                .ToList();
            response.Source = "docker-local";
            if (!response.TokenConfigured)
                response.Hint = "GitHub'dan alınamadı; bu makinedeki dockertest image'ları gösteriliyor.";
            return response;
        }

        response.Hint = "dockertest release bulunamadı. release branch'ine push yapıldı mı kontrol et.";
        return response;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, string? token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.UserAgent.ParseAdd("dockertest-agent");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static bool IsAppRelease(string tag, string? name)
    {
        if (name?.StartsWith("Agent", StringComparison.OrdinalIgnoreCase) == true)
            return false;

        var version = tag.StartsWith('v') ? tag[1..] : tag;
        return AppVersionFilter.IsAppVersion(version);
    }

    private async Task<IReadOnlyList<ReleaseVersion>> TryGitHubReleasesAsync(string? token, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{_options.GitHubOwner}/{_options.GitHubRepo}/releases?per_page=50";
        try
        {
            using var request = CreateRequest(HttpMethod.Get, url, token);
            using var httpResponse = await _http.SendAsync(request, ct);
            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub releases API returned {Status}", httpResponse.StatusCode);
                return [];
            }

            var json = await httpResponse.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var list = new List<ReleaseVersion>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var tag = item.GetProperty("tag_name").GetString() ?? "";
                var name = item.GetProperty("name").GetString();
                if (!IsAppRelease(tag, name))
                    continue;

                var version = tag.StartsWith('v') ? tag[1..] : tag;
                if (!AppVersionFilter.IsAppVersion(version))
                    continue;

                list.Add(new ReleaseVersion
                {
                    Tag = tag,
                    Version = version,
                    Name = name ?? tag,
                    PublishedAt = item.GetProperty("published_at").GetDateTimeOffset(),
                    IsPrerelease = item.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()
                });
            }

            return list.OrderByDescending(r => r.PublishedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub releases could not be loaded");
            return [];
        }
    }

    private async Task<IReadOnlyList<ReleaseVersion>> TryGitHubPackagesAsync(string token, CancellationToken ct)
    {
        var url = $"https://api.github.com/users/{_options.GitHubOwner}/packages/container/{_options.GitHubRepo}/versions?per_page=50";
        try
        {
            using var request = CreateRequest(HttpMethod.Get, url, token);
            using var httpResponse = await _http.SendAsync(request, ct);
            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub packages API returned {Status}", httpResponse.StatusCode);
                return [];
            }

            var json = await httpResponse.Content.ReadAsStringAsync(ct);
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
                    if (string.IsNullOrWhiteSpace(tag) || tag.Equals("latest", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!IsAppRelease(tag, null))
                        continue;

                    if (!AppVersionFilter.IsAppVersion(tag))
                        continue;

                    if (!versions.ContainsKey(tag))
                    {
                        versions[tag] = new ReleaseVersion
                        {
                            Version = tag,
                            Tag = tag,
                            Name = $"GHCR — {tag}",
                            PublishedAt = createdAt
                        };
                    }
                }
            }

            return versions.Values.OrderByDescending(v => v.PublishedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub packages could not be loaded");
            return [];
        }
    }
}
