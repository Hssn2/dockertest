using dockertest.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));

var databaseOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>();

if (!string.IsNullOrWhiteSpace(databaseOptions?.ConnectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(databaseOptions.ConnectionString));
}

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (databaseOptions?.AutoMigrate == true)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");

    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        logger.LogInformation("Applying database migrations...");
        dbContext.Database.Migrate();
        logger.LogInformation("Database migrations applied.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration failed. Check ConnectionString and PostgreSQL reachability.");
        throw;
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
 

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet("/health", () =>
{
    var version = Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev";
    return Results.Ok(new { status = "healthy", version });
});

app.Run();
