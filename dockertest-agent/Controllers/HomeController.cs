using dockertest_agent.Models;
using dockertest_agent.Services;
using Microsoft.AspNetCore.Mvc;

namespace dockertest_agent.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
[ApiController]
[Route("api")]
public class UpdateApiController : ControllerBase
{
    private readonly ReleaseService _releases;
    private readonly DockerService _docker;
    private readonly UpdateOrchestrator _orchestrator;
    private readonly UpdateStateStore _store;
    private readonly AgentOptions _options;

    public UpdateApiController(
        ReleaseService releases,
        DockerService docker,
        UpdateOrchestrator orchestrator,
        UpdateStateStore store,
        Microsoft.Extensions.Options.IOptions<AgentOptions> options)
    {
        _releases = releases;
        _docker = docker;
        _orchestrator = orchestrator;
        _store = store;
        _options = options.Value;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var state = _store.GetState();
        return Ok(new
        {
            state.ActiveContainerName,
            state.ActiveVersion,
            progress = state.Progress,
            isBusy = _orchestrator.IsBusy
        });
    }

    [HttpGet("releases")]
    public async Task<IActionResult> Releases(CancellationToken ct)
    {
        var catalog = await _releases.GetCatalogAsync(ct);
        return Ok(catalog);
    }

    [HttpGet("containers")]
    public async Task<IActionResult> Containers(CancellationToken ct)
    {
        var state = _store.GetState();
        var list = await _docker.ListManagedContainersAsync(state.ActiveContainerName, ct);
        return Ok(list);
    }

    [HttpPost("deploy/{version}")]
    public IActionResult Deploy(string version)
    {
        if (_orchestrator.IsBusy)
            return Conflict(new { error = "Başka bir güncelleme çalışıyor." });

        _ = Task.Run(async () =>
        {
            try
            {
                await _orchestrator.DeployVersionAsync(version, CancellationToken.None);
            }
            catch
            {
                // errors stored in state or db
            }
        });

        return Accepted(new { message = $"Güncelleme başlatıldı: {version}" });
    }

    [HttpPost("rollback/{containerName}")]
    public IActionResult Rollback(string containerName)
    {
        if (_orchestrator.IsBusy)
            return Conflict(new { error = "Başka bir işlem çalışıyor." });

        if (!containerName.StartsWith(_options.ContainerNamePrefix, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Geçersiz container adı." });

        _ = Task.Run(async () =>
        {
            try
            {
                await _orchestrator.RollbackToAsync(containerName, CancellationToken.None);
            }
            catch
            {
                // errors stored in state
            }
        });

        return Accepted(new { message = $"Rollback başlatıldı: {containerName}" });
    }
}
