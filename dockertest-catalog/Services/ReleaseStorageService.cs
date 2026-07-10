using System.Text.Json;
using System.Text.RegularExpressions;
using dockertest_catalog.Models;

namespace dockertest_catalog.Services;

public class ReleaseStorageService
{
    private static readonly Regex VersionFromFile = new(
        @"^dockertest-(?<ver>\d+\.\d+\.\d+)\.tar\.gz$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SemVer = new(
        @"^\d+\.\d+\.\d+$",
        RegexOptions.Compiled);

    private readonly string _releasesDir;
    private readonly ILogger<ReleaseStorageService> _logger;
    private readonly object _lock = new();

    public ReleaseStorageService(IWebHostEnvironment env, ILogger<ReleaseStorageService> logger)
    {
        _logger = logger;
        _releasesDir = Path.Combine(
            env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"),
            "releases");
        Directory.CreateDirectory(_releasesDir);
    }

    public CatalogResponse GetCatalog()
    {
        lock (_lock)
        {
            var entries = LoadManifestEntries();
            if (entries.Count == 0)
                entries = ScanArchiveFiles();

            var items = entries
                .Where(e => System.IO.File.Exists(GetFilePath(e.File)))
                .Select(ToDto)
                .OrderByDescending(r => r.PublishedAt)
                .ToList();

            var response = new CatalogResponse { Items = items, Source = "catalog" };
            if (items.Count == 0)
            {
                response.Hint = "Henüz sürüm yok. .tar.gz dosyası yükle veya releases klasörüne kopyala.";
            }

            return response;
        }
    }

    public async Task<(bool Ok, string? Error, CatalogReleaseDto? Item)> UploadAsync(
        IFormFile file,
        string? version,
        string? name,
        CancellationToken ct)
    {
        if (file.Length == 0)
            return (false, "Dosya boş.", null);

        var originalName = Path.GetFileName(file.FileName);
        if (!originalName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            return (false, "Sadece .tar.gz dosyası yüklenebilir.", null);

        version = NormalizeVersion(version, originalName);
        if (version == null)
            return (false, "Sürüm bulunamadı. Dosya adı dockertest-x.y.z.tar.gz olmalı veya sürüm alanını doldur.", null);

        var targetFile = $"dockertest-{version}.tar.gz";
        var targetPath = GetFilePath(targetFile);

        try
        {
            await using (var stream = file.OpenReadStream())
            await using (var output = System.IO.File.Create(targetPath))
                await stream.CopyToAsync(output, ct);

            ManifestReleaseEntry entry;
            lock (_lock)
            {
                entry = new ManifestReleaseEntry
                {
                    Version = version,
                    Name = string.IsNullOrWhiteSpace(name) ? $"Sürüm {version}" : name.Trim(),
                    File = targetFile,
                    PublishedAt = DateTimeOffset.UtcNow
                };
                UpsertManifestEntry(entry);
            }

            _logger.LogInformation("Release uploaded: {Version} ({File})", version, targetFile);
            return (true, null, ToDto(entry));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed");
            return (false, ex.Message, null);
        }
    }

    public (bool Ok, string? Error) Delete(string version)
    {
        if (!SemVer.IsMatch(version))
            return (false, "Geçersiz sürüm.");

        lock (_lock)
        {
            var file = $"dockertest-{version}.tar.gz";
            var path = GetFilePath(file);

            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);

            var manifest = LoadManifestFile();
            var before = manifest.Releases.Count;
            manifest.Releases.RemoveAll(r =>
                string.Equals(r.Version, version, StringComparison.OrdinalIgnoreCase));
            SaveManifest(manifest);

            if (!System.IO.File.Exists(path) && before == manifest.Releases.Count)
                return (false, "Sürüm bulunamadı.");

            _logger.LogInformation("Release deleted: {Version}", version);
            return (true, null);
        }
    }

    private string GetFilePath(string file) => Path.Combine(_releasesDir, file);

    private CatalogReleaseDto ToDto(ManifestReleaseEntry e)
    {
        var path = GetFilePath(e.File);
        return new CatalogReleaseDto
        {
            Version = e.Version,
            Tag = e.Version.StartsWith('v') ? e.Version : $"v{e.Version}",
            Name = string.IsNullOrWhiteSpace(e.Name) ? $"Sürüm {e.Version}" : e.Name,
            DownloadUrl = $"/releases/{e.File}",
            PublishedAt = e.PublishedAt ?? (System.IO.File.Exists(path)
                ? System.IO.File.GetLastWriteTimeUtc(path)
                : DateTimeOffset.UtcNow),
            FileSizeBytes = System.IO.File.Exists(path) ? new FileInfo(path).Length : 0
        };
    }

    private static string? NormalizeVersion(string? version, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(version))
        {
            version = version.Trim().TrimStart('v', 'V');
            return SemVer.IsMatch(version) ? version : null;
        }

        var match = VersionFromFile.Match(fileName);
        return match.Success ? match.Groups["ver"].Value : null;
    }

    private List<ManifestReleaseEntry> LoadManifestEntries()
    {
        return LoadManifestFile().Releases
            .Where(r => !string.IsNullOrWhiteSpace(r.Version) && !string.IsNullOrWhiteSpace(r.File))
            .ToList();
    }

    private ManifestFile LoadManifestFile()
    {
        var manifestPath = Path.Combine(_releasesDir, "manifest.json");
        if (!System.IO.File.Exists(manifestPath))
            return new ManifestFile();

        try
        {
            var json = System.IO.File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<ManifestFile>(json, JsonOptions) ?? new ManifestFile();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "manifest.json okunamadı");
            return new ManifestFile();
        }
    }

    private void SaveManifest(ManifestFile manifest)
    {
        var manifestPath = Path.Combine(_releasesDir, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, JsonWriteOptions);
        System.IO.File.WriteAllText(manifestPath, json);
    }

    private void UpsertManifestEntry(ManifestReleaseEntry entry)
    {
        var manifest = LoadManifestFile();
        manifest.Releases.RemoveAll(r =>
            string.Equals(r.Version, entry.Version, StringComparison.OrdinalIgnoreCase));
        manifest.Releases.Add(entry);
        manifest.Releases = manifest.Releases
            .OrderByDescending(r => r.PublishedAt ?? DateTimeOffset.MinValue)
            .ToList();
        SaveManifest(manifest);
    }

    private List<ManifestReleaseEntry> ScanArchiveFiles()
    {
        return Directory
            .EnumerateFiles(_releasesDir, "dockertest-*.tar.gz", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                var file = Path.GetFileName(path);
                var match = VersionFromFile.Match(file);
                if (!match.Success)
                    return null;

                return new ManifestReleaseEntry
                {
                    Version = match.Groups["ver"].Value,
                    Name = $"Arşiv — {match.Groups["ver"].Value}",
                    File = file,
                    PublishedAt = System.IO.File.GetLastWriteTimeUtc(path)
                };
            })
            .Where(e => e != null)
            .Cast<ManifestReleaseEntry>()
            .ToList();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };
}
