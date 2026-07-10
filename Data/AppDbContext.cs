using dockertest.Data.Configurations;
using dockertest.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace dockertest.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<SysUser> SysUsers => Set<SysUser>();

    public DbSet<SysSetting> SysSettings => Set<SysSetting>();

    public DbSet<SysAuditLog> SysAuditLogs => Set<SysAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        SysEntityConfigurations.Configure(modelBuilder);
    }
}
