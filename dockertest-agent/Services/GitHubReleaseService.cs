using System.Net.Http.Headers;
using System.Text.Json;
using dockertest_agent.Models;
using Microsoft.Extensions.Options;

namespace dockertest_agent.Services;

public class GitHubReleaseService
{
    private readonly HttpClient _http;
    private readonly AgentOptions _options;
    private readonly ILogger<GitHubReleaseService> _logger;

    public GitHubReleaseService(HttpClient http, IOptions<AgentOptions> options, ILogger<GitHubReleaseService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("dockertest-agent");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(_options.GitHubToken))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.GitHubToken);
    }

    public async Task<IReadOnlyList<ReleaseVersion>> GetReleasesAsync(CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{_options.GitHubOwner}/{_options.GitHubRepo}/releases?per_page=50";
        try
        {
            var json = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var list = new List<ReleaseVersion>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var tag = item.GetProperty("tag_name").GetString() ?? "";
                var version = tag.StartsWith('v') ? tag[1..] : tag;
                list.Add(new ReleaseVersion
                {
                    Tag = tag,
                    Version = version,
                    Name = item.GetProperty("name").GetString() ?? tag,
                    PublishedAt = item.GetProperty("published_at").GetDateTimeOffset(),
                    IsPrerelease = item.GetProperty("prerelease").GetBoolean()
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
}
