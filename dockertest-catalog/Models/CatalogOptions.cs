namespace dockertest_catalog.Models;

public class CatalogOptions
{
    public const string SectionName = "Catalog";

    /// <summary>
    /// İndirme URL'leri için taban adres. Boşsa istekten (scheme + host) alınır.
    /// Docker'da: http://host.docker.internal:8090
    /// </summary>
    public string PublicBaseUrl { get; set; } = "";
}
