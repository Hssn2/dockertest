using dockertest.Database.Sys;
using dockertest.Entities.Sys;
using Microsoft.EntityFrameworkCore;

namespace dockertest.Services;

/// <summary>
/// SYS seed verisi — startup'ta çalışır, migration oluşturmaya gerek yok.
/// </summary>
public class SysDataSeeder
{
    private static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task SeedAsync(SysDbContext db, CancellationToken ct = default)
    {
        await SeedMenusAsync(db, ct);
        await SeedUsersAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedMenusAsync(SysDbContext db, CancellationToken ct)
    {
        var seeds = new[]
        {
            new SysMenu { Id = 1, Code = "home", Title = "Ana Sayfa", SortOrder = 1, IsActive = true, CreatedAt = SeedDate },
            new SysMenu { Id = 2, Code = "update", Title = "Güncelleme", SortOrder = 2, IsActive = true, CreatedAt = SeedDate },
            new SysMenu { Id = 3, Code = "games", Title = "Oyunlar", SortOrder = 3, IsActive = true, CreatedAt = SeedDate }
        };

        foreach (var seed in seeds)
        {
            var exists = await db.Menus.AnyAsync(m => m.Code == seed.Code, ct);
            if (!exists)
                db.Menus.Add(seed);
        }
    }

    private static async Task SeedUsersAsync(SysDbContext db, CancellationToken ct)
    {
        var seeds = new[]
        {
            new SysUser { Id = 1, Username = "admin", Email = "admin@dockertest.local", DisplayName = "Yönetici", IsActive = true, CreatedAt = SeedDate },
            new SysUser { Id = 2, Username = "demo", Email = "demo@dockertest.local", DisplayName = "Demo Kullanıcı", IsActive = true, CreatedAt = SeedDate }
        };

        foreach (var seed in seeds)
        {
            var exists = await db.Users.AnyAsync(u => u.Username == seed.Username, ct);
            if (!exists)
                db.Users.Add(seed);
        }
    }
}
