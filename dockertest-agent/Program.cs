using dockertest_agent.Hubs;
using dockertest_agent.Models;
using dockertest_agent.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.AddSingleton<UpdateStateStore>();
builder.Services.AddSingleton<DockerService>();
builder.Services.AddSingleton<UpdateOrchestrator>();
builder.Services.AddHttpClient<GitHubReleaseService>();
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
