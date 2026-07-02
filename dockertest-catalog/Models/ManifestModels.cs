namespace dockertest_catalog.Models;

public class ManifestFile
{
    public List<ManifestReleaseEntry> Releases { get; set; } = [];
}

public class ManifestReleaseEntry
{
    public string Version { get; set; } = "";
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
}

public class CatalogReleaseDto
{
    public string Version { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Name { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public DateTimeOffset PublishedAt { get; set; }
    public long FileSizeBytes { get; set; }
}

public class CatalogResponse
{
    public IReadOnlyList<CatalogReleaseDto> Items { get; set; } = [];
    public string Source { get; set; } = "catalog";
    public string? Hint { get; set; }
}
