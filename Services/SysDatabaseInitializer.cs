using dockertest.Database.Sys;
using dockertest.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace dockertest.Services;

/// <summary>
/// Uygulama açılışında SYS DbContext migration'larını uygular.
/// </summary>
public class SysDatabaseInitializer
{
    private readonly DatabaseOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SysDatabaseInitializer> _logger;

    public SysDatabaseInitializer(
        IOptions<DatabaseOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<SysDatabaseInitializer> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("SYS database devre dışı (Database:Enabled=false).");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException("Database:ConnectionString tanımlı değil.");

        if (_options.AutoCreateDatabase)
        {
            _logger.LogInformation("Veritabanı kontrol ediliyor...");
            await PostgreSqlBootstrap.EnsureDatabaseExistsAsync(_options.ConnectionString, ct);
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SysDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<SysDataSeeder>();

        _logger.LogInformation("SYS şema migration uygulanıyor...");
        await db.Database.MigrateAsync(ct);
        _logger.LogInformation("SYS şema migration tamamlandı.");

        _logger.LogInformation("SYS seed uygulanıyor...");
        await seeder.SeedAsync(db, ct);
        _logger.LogInformation("SYS seed tamamlandı.");
    }
}
