using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<dockertest.Models.DatabaseOptions>(
    builder.Configuration.GetSection(dockertest.Models.DatabaseOptions.SectionName));

builder.Services.AddDbContext<dockertest.Database.Sys.SysDbContext>((sp, options) =>
{
    var dbOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<dockertest.Models.DatabaseOptions>>().Value;
    options.UseNpgsql(dbOpts.ConnectionString);
});

builder.Services.AddSingleton<dockertest.Services.SysDatabaseInitializer>();
builder.Services.AddSingleton<dockertest.Services.SysDataSeeder>();
builder.Services.AddSingleton<dockertest.Services.SysSchemaExporter>();
builder.Services.AddControllersWithViews();

if (args.Contains("export-sys", StringComparer.OrdinalIgnoreCase))
{
    var exportApp = builder.Build();
    var exporter = exportApp.Services.GetRequiredService<dockertest.Services.SysSchemaExporter>();
    var result = await exporter.ExportAsync();
    Console.WriteLine(result.Message);
    if (result.Objects.Count > 0)
        Console.WriteLine("Objeler: " + string.Join(", ", result.Objects));
    return;
}

var app = builder.Build();

await app.Services.GetRequiredService<dockertest.Services.SysDatabaseInitializer>().MigrateAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet("/health", async (
    Microsoft.Extensions.Options.IOptions<dockertest.Models.DatabaseOptions> dbOptions,
    dockertest.Database.Sys.SysDbContext db) =>
{
    var version = Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev";
    var dbOpts = dbOptions.Value;
    var payload = new Dictionary<string, object>
    {
        ["status"] = "healthy",
        ["version"] = version
    };

    if (dbOpts.Enabled && !string.IsNullOrWhiteSpace(dbOpts.ConnectionString))
    {
        try
        {
            payload["database"] = await db.Database.CanConnectAsync() ? "connected" : "error";
        }
        catch (Exception ex)
        {
            payload["database"] = "error";
            payload["databaseError"] = ex.Message;
        }
    }

    return Results.Ok(payload);
});

if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/sys/export", async (dockertest.Services.SysSchemaExporter exporter) =>
    {
        var result = await exporter.ExportAsync();
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    });
}

app.Run();
