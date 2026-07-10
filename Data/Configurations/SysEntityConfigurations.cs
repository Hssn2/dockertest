using dockertest.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dockertest.Data.Configurations;

public static class SysEntityConfigurations
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        ConfigureSysUser(modelBuilder.Entity<SysUser>());
        ConfigureSysSetting(modelBuilder.Entity<SysSetting>());
        ConfigureSysAuditLog(modelBuilder.Entity<SysAuditLog>());
    }

    private static void ConfigureSysUser(EntityTypeBuilder<SysUser> entity)
    {
        entity.ToTable("sys_user");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Username)
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(e => e.Email)
            .HasMaxLength(256)
            .IsRequired();

        entity.Property(e => e.IsActive)
            .HasDefaultValue(true);

        entity.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        entity.HasIndex(e => e.Username)
            .IsUnique();

        entity.HasIndex(e => e.Email)
            .IsUnique();
    }

    private static void ConfigureSysSetting(EntityTypeBuilder<SysSetting> entity)
    {
        entity.ToTable("sys_setting");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Key)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(e => e.Value)
            .HasMaxLength(4000);

        entity.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        entity.HasIndex(e => e.Key)
            .IsUnique();
    }

    private static void ConfigureSysAuditLog(EntityTypeBuilder<SysAuditLog> entity)
    {
        entity.ToTable("sys_audit_log");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Action)
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(e => e.EntityName)
            .HasMaxLength(200);

        entity.Property(e => e.EntityId)
            .HasMaxLength(100);

        entity.Property(e => e.Details)
            .HasColumnType("text");

        entity.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        entity.HasIndex(e => e.CreatedAt);

        entity.HasIndex(e => e.UserId);
    }
}
