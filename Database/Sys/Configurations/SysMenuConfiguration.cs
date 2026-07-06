using dockertest.Database.Constants;
using dockertest.Entities.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dockertest.Database.Sys.Configurations;

/// <summary>
/// Sadece tablo şeması — seed SysDataSeeder'da (migration gerekmez).
/// </summary>
public sealed class SysMenuConfiguration : IEntityTypeConfiguration<SysMenu>
{
    public void Configure(EntityTypeBuilder<SysMenu> builder)
    {
        builder.ToTable(SysTableNames.Menu);

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SortOrder).HasDefaultValue(0);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
    }
}
