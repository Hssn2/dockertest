using dockertest_agent.Models;
using Microsoft.Extensions.Options;

namespace dockertest_agent.Services;

public class ReleaseService
{
    private readonly CatalogReleaseService _catalog;
    private readonly GitHubReleaseService _github;
    private readonly AgentOptions _options;
    private readonly Dictionary<string, string> _downloadUrls = new(StringComparer.OrdinalIgnoreCase);

    public ReleaseService(
        CatalogReleaseService catalog,
        GitHubReleaseService github,
        IOptions<AgentOptions> options)
    {
        _catalog = catalog;
        _github = github;
        _options = options.Value;
    }

    public async Task<ReleasesResponse> GetCatalogAsync(CancellationToken ct)
    {
        _downloadUrls.Clear();

        if (!string.IsNullOrWhiteSpace(_options.CatalogUrl))
        {
            var catalog = await _catalog.GetCatalogAsync(ct);
            foreach (var item in catalog.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.DownloadUrl))
                    _downloadUrls[item.Version] = item.DownloadUrl;
            }

            if (catalog.Items.Count > 0)
                return catalog;
        }

        return await _github.GetCatalogAsync(ct);
    }

    public string? GetDownloadUrl(string version) =>
        _downloadUrls.TryGetValue(version, out var url) ? url : null;

    public async Task<string?> ResolveDownloadUrlAsync(string version, CancellationToken ct)
    {
        if (_downloadUrls.TryGetValue(version, out var cached))
            return cached;

        await GetCatalogAsync(ct);
        return GetDownloadUrl(version);
    }
}
