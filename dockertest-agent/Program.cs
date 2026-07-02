using System.Net.Http.Headers;
using dockertest_agent.Hubs;
using dockertest_agent.Models;
using dockertest_agent.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.AddSingleton<UpdateStateStore>();
builder.Services.AddSingleton<DockerService>();
builder.Services.AddSingleton<UpdateOrchestrator>();
builder.Services.AddSingleton<ReleaseService>();
builder.Services.AddHttpClient<CatalogReleaseService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("dockertest-agent");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<GitHubReleaseService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("dockertest-agent");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("image-download", client =>
{
    client.Timeout = TimeSpan.FromHours(2);
});
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.AddControllersWithViews();

var app = builder.Build();

await DiscoverActiveContainerAsync(app);

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Home/Error");

app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapHub<UpdateHub>("/hubs/update");

app.Run();

static async Task DiscoverActiveContainerAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var docker = scope.ServiceProvider.GetRequiredService<DockerService>();
        var store = scope.ServiceProvider.GetRequiredService<UpdateStateStore>();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>().Value;

        var containers = await docker.ListManagedContainersAsync(null, CancellationToken.None);
        var active = containers.FirstOrDefault(c =>
            c.State == "running" && c.HostPort == options.AppHostPort);

        if (active != null)
            store.SetActive(active.Name, active.Version);
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Docker'a bağlanılamadı. Socket mount kontrol et: /var/run/docker.sock");
    }
}
