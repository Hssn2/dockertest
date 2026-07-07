namespace dockertest.Data;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// When true, pending EF migrations are applied at startup.
    /// Only Sys_* tables defined in AppDbContext are affected.
    /// </summary>
    public bool AutoMigrate { get; set; }
}
