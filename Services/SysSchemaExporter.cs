using System.Diagnostics;
using System.Text;
using dockertest.Models;
using Microsoft.Extensions.Options;
using Npgsql;

namespace dockertest.Services;

/// <summary>
/// Lokal PostgreSQL'deki sys_* objelerini pg_dump ile SQL dosyasına çeker.
/// Geliştirme: DB'de değiştir → export → git → deploy'da SYS runner uygular.
/// </summary>
public class SysSchemaExporter
{
    private readonly DatabaseOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SysSchemaExporter> _logger;

    public SysSchemaExporter(
        IOptions<DatabaseOptions> options,
        IWebHostEnvironment env,
        ILogger<SysSchemaExporter> logger)
    {
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public async Task<SysExportResult> ExportAsync(CancellationToken ct = default)
    {
        var sourceCs = string.IsNullOrWhiteSpace(_options.ExportConnectionString)
            ? _options.ConnectionString
            : _options.ExportConnectionString;

        if (string.IsNullOrWhiteSpace(sourceCs))
            throw new InvalidOperationException("Database:ConnectionString veya ExportConnectionString gerekli.");

        await PostgreSqlBootstrap.EnsureDatabaseExistsAsync(sourceCs, ct);

        var objects = await ListSysObjectsAsync(sourceCs, ct);
        if (objects.Count == 0)
        {
            return new SysExportResult
            {
                Success = true,
                Message = $"sys_* objesi bulunamadı (prefix: {_options.SysObjectPrefix}).",
                ObjectCount = 0
            };
        }

        var dump = await RunPgDumpAsync(sourceCs, objects, ct);
        var scriptsDir = ResolveScriptsDirectory();
        Directory.CreateDirectory(scriptsDir);

        var fileName = _options.ExportFileName;
        var path = Path.Combine(scriptsDir, fileName);
        var header = new StringBuilder();
        header.AppendLine("-- Otomatik üretildi: lokal DB'den sys_* export");
        header.AppendLine($"-- Tarih: {DateTimeOffset.Now:O}");
        header.AppendLine($"-- Obje sayısı: {objects.Count}");
        header.AppendLine();
        header.AppendLine(dump);

        await File.WriteAllTextAsync(path, header.ToString(), ct);
        _logger.LogInformation("SYS export yazıldı: {Path} ({Count} obje)", path, objects.Count);

        return new SysExportResult
        {
            Success = true,
            Message = $"Export tamam: {fileName}",
            FilePath = path,
            ObjectCount = objects.Count,
            Objects = objects
        };
    }

    private async Task<List<string>> ListSysObjectsAsync(string connectionString, CancellationToken ct)
    {
        var prefix = _options.SysObjectPrefix;
        var likePattern = prefix + "%";
        var list = new List<string>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT c.relname
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public'
              AND c.relkind IN ('r', 'v', 'm')
              AND c.relname LIKE @prefix
            ORDER BY c.relname;
            """;

        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("prefix", likePattern);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                list.Add(reader.GetString(0));
        }

        const string funcSql = """
            SELECT p.proname
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public'
              AND p.proname LIKE @prefix
            ORDER BY p.proname;
            """;

        await using (var cmd = new NpgsqlCommand(funcSql, conn))
        {
            cmd.Parameters.AddWithValue("prefix", likePattern);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var name = reader.GetString(0);
                if (!list.Contains(name, StringComparer.OrdinalIgnoreCase))
                    list.Add(name);
            }
        }

        return list.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private async Task<string> RunPgDumpAsync(
        string connectionString,
        IReadOnlyList<string> objects,
        CancellationToken ct)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString);

        if (!string.IsNullOrWhiteSpace(_options.PgDumpDockerContainer))
            return await RunPgDumpViaDockerAsync(csb, objects, ct);

        return await RunPgDumpLocalAsync(csb, objects, ct);
    }

    private async Task<string> RunPgDumpLocalAsync(
        NpgsqlConnectionStringBuilder csb,
        IReadOnlyList<string> objects,
        CancellationToken ct)
    {
        var args = BuildPgDumpArgs(csb, objects);
        return await ExecutePgDumpAsync("pg_dump", args, csb.Password, ct);
    }

    private async Task<string> RunPgDumpViaDockerAsync(
        NpgsqlConnectionStringBuilder csb,
        IReadOnlyList<string> objects,
        CancellationToken ct)
    {
        var innerArgs = BuildPgDumpArgs(csb, objects, hostOverride: "localhost");
        var dockerArgs = new List<string>
        {
            "exec", "-e", $"PGPASSWORD={csb.Password}", _options.PgDumpDockerContainer!,
            "pg_dump"
        };
        dockerArgs.AddRange(innerArgs);

        return await ExecutePgDumpAsync("docker", dockerArgs, null, ct);
    }

    private static List<string> BuildPgDumpArgs(
        NpgsqlConnectionStringBuilder csb,
        IReadOnlyList<string> objects,
        string? hostOverride = null)
    {
        var args = new List<string>
        {
            "--schema-only",
            "--no-owner",
            "--no-privileges",
            "-h", hostOverride ?? csb.Host ?? "localhost",
            "-p", csb.Port.ToString(),
            "-U", csb.Username ?? "postgres",
            "-d", csb.Database ?? "postgres"
        };

        foreach (var obj in objects)
        {
            args.Add("-t");
            args.Add(obj);
        }

        return args;
    }

    private async Task<string> ExecutePgDumpAsync(
        string fileName,
        IReadOnlyList<string> args,
        string? password,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        if (!string.IsNullOrEmpty(password))
            psi.Environment["PGPASSWORD"] = password;

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"{fileName} başlatılamadı. PATH'te var mı?");

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{fileName} hata ({process.ExitCode}): {error}");

        return output;
    }

    private string ResolveScriptsDirectory()
    {
        var relative = _options.SysScriptsPath.Trim('/', '\\');
        var fromContentRoot = Path.Combine(_env.ContentRootPath, relative);
        return Directory.Exists(fromContentRoot)
            ? fromContentRoot
            : Path.Combine(AppContext.BaseDirectory, relative);
    }
}

public class SysExportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? FilePath { get; set; }
    public int ObjectCount { get; set; }
    public IReadOnlyList<string> Objects { get; set; } = [];
}
