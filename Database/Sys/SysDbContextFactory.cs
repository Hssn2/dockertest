using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace dockertest.Database.Sys;

public class SysDbContextFactory : IDesignTimeDbContextFactory<SysDbContext>
{
    public SysDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SysDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=dockertest;Username=postgres;Password=postgres")
            .Options;

        return new SysDbContext(options);
    }
}
