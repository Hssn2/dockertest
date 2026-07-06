using dockertest.Entities.Sys;
using Microsoft.EntityFrameworkCore;

namespace dockertest.Database.Sys;

/// <summary>
/// Sadece sys_* tabloları — ana uygulama entity'leri burada değil.
/// </summary>
public class SysDbContext : DbContext
{
    public SysDbContext(DbContextOptions<SysDbContext> options) : base(options) { }

    public DbSet<SysMenu> Menus => Set<SysMenu>();
    public DbSet<SysUser> Users => Set<SysUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SysDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
