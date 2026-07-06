namespace dockertest.Models;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = "";

    /// <summary>Boşsa ConnectionString kullanılır (lokal gopoint-postgres vb.)</summary>
    public string ExportConnectionString { get; set; } = "";

    public string SysScriptsPath { get; set; } = "Database/Sys";

    /// <summary>Eski SYS_*.sql dosyalarını da uygula (varsayılan: kapalı)</summary>
    public bool ApplySqlScripts { get; set; }

    /// <summary>DB yoksa postgres'e bağlanıp CREATE DATABASE yapar.</summary>
    public bool AutoCreateDatabase { get; set; } = true;

    /// <summary>Export/import edilecek obje prefix'i (varsayılan: sys_)</summary>
    public string SysObjectPrefix { get; set; } = "sys_";

    /// <summary>Lokal DB export dosya adı (SYS runner sırayla uygular)</summary>
    public string ExportFileName { get; set; } = "SYS_900_AutoExport.sql";

    /// <summary>pg_dump host'ta yoksa: gopoint-postgres gibi container adı</summary>
    public string PgDumpDockerContainer { get; set; } = "";
}
