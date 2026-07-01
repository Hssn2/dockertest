using dockertest_agent.Hubs;
using dockertest_agent.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace dockertest_agent.Services;

public class UpdateOrchestrator
{
    private readonly DockerService _docker;
    private readonly UpdateStateStore _store;
    private readonly AgentOptions _options;
    private readonly IHubContext<UpdateHub> _hub;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpdateOrchestrator> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public UpdateOrchestrator(
        DockerService docker,
        UpdateStateStore store,
        IOptions<AgentOptions> options,
        IHubContext<UpdateHub> hub,
        IHttpClientFactory httpClientFactory,
        ILogger<UpdateOrchestrator> logger)
    {
        _docker = docker;
        _store = store;
        _options = options.Value;
        _hub = hub;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsBusy => _gate.CurrentCount == 0;

    public async Task DeployVersionAsync(string version, CancellationToken ct)
    {
        if (!await _gate.WaitAsync(0, ct))
            throw new InvalidOperationException("Başka bir güncelleme zaten çalışıyor.");

        var previousContainer = _store.GetState().ActiveContainerName;
        var previousVersion = _store.GetState().ActiveVersion;
        var prodName = $"{_options.ContainerNamePrefix}{version}";

        try
        {
            Begin(version, previousVersion);
            await _docker.CleanupCandidatesAsync(ct);

            await SetPhaseAsync(UpdatePhase.StoppingCurrent, "Çalışan sürüm durduruluyor...", 20, ct);
            await _docker.StopContainersOnPortAsync(_options.AppHostPort, ct);
            if (!string.IsNullOrWhiteSpace(previousContainer))
                await _docker.StopContainerAsync(previousContainer, ct);

            await SetPhaseAsync(UpdatePhase.PullingImage, "Yeni image indiriliyor...", 45, ct);
            var pullProgress = new Progress<string>(msg => _ = BroadcastLog(msg));
            await _docker.PullImageAsync(version, pullProgress, ct);

            await SetPhaseAsync(UpdatePhase.StartingProduction, $"Port {_options.AppHostPort} üzerinde yeni sürüm başlatılıyor...", 70, ct);
            await _docker.RemoveContainerAsync(prodName, ct);
            await _docker.RunContainerAsync(prodName, _options.AppHostPort, version, ct);

            await SetPhaseAsync(UpdatePhase.HealthCheckingProduction, "Uygulama kontrol ediliyor...", 90, ct);
            var healthy = await WaitForHealthAsync(_options.AppHostPort, ct);
            if (!healthy)
            {
                await SetPhaseAsync(UpdatePhase.RollingBack, "Yeni sürüm açılmadı, eskisine dönülüyor...", 95, ct);
                await _docker.StopContainerAsync(prodName, ct);

                if (!string.IsNullOrWhiteSpace(previousContainer))
                {
                    await _docker.StartExistingContainerAsync(previousContainer, ct);
                    var rollbackOk = await WaitForHealthAsync(_options.AppHostPort, ct);
                    if (!rollbackOk)
                        throw new InvalidOperationException("Rollback başarısız! Manuel müdahale gerekli.");

                    _store.SetActive(previousContainer, previousVersion ?? "");
                    await SetPhaseAsync(UpdatePhase.RolledBack, $"Eski sürüme dönüldü: {previousVersion}", 100, ct);
                }
                else
                {
                    throw new InvalidOperationException("Yeni sürüm başarısız ve geri dönülecek container yok.");
                }

                return;
            }

            _store.SetActive(prodName, version);
            await SetPhaseAsync(UpdatePhase.Completed, $"Güncelleme tamamlandı! Aktif sürüm: {version}", 100, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deploy failed for version {Version}", version);
            _store.UpdateProgress(p =>
            {
                p.Phase = UpdatePhase.Failed;
                p.Message = ex.Message;
                p.Error = ex.Message;
                p.IsRunning = false;
                p.FinishedAt = DateTimeOffset.UtcNow;
                p.Percent = 100;
            });
            await BroadcastStateAsync(ct);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RollbackToAsync(string containerName, CancellationToken ct)
    {
        if (!await _gate.WaitAsync(0, ct))
            throw new InvalidOperationException("Başka bir işlem zaten çalışıyor.");

        var state = _store.GetState();
        var current = state.ActiveContainerName;

        try
        {
            Begin(ExtractVersion(containerName), state.ActiveVersion);
            await SetPhaseAsync(UpdatePhase.StoppingCurrent, "Mevcut sürüm durduruluyor...", 30, ct);
            await _docker.StopContainersOnPortAsync(_options.AppHostPort, ct);
            if (!string.IsNullOrWhiteSpace(current))
                await _docker.StopContainerAsync(current, ct);

            await SetPhaseAsync(UpdatePhase.RollingBack, $"{containerName} başlatılıyor...", 60, ct);
            await _docker.StartExistingContainerAsync(containerName, ct);

            await SetPhaseAsync(UpdatePhase.HealthCheckingProduction, "Kontrol ediliyor...", 85, ct);
            var healthy = await WaitForHealthAsync(_options.AppHostPort, ct);
            if (!healthy)
                throw new InvalidOperationException("Rollback sonrası uygulama yanıt vermiyor.");

            _store.SetActive(containerName, ExtractVersion(containerName));
            await SetPhaseAsync(UpdatePhase.RolledBack, $"Geri dönüldü: {ExtractVersion(containerName)}", 100, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ExtractVersion(string containerName)
    {
        var prefix = _options.ContainerNamePrefix;
        var name = containerName;
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            name = name[prefix.Length..];
        if (name.EndsWith("_candidate", StringComparison.OrdinalIgnoreCase))
            name = name[..^"_candidate".Length];
        return name;
    }

    private void Begin(string targetVersion, string? previousVersion)
    {
        _store.UpdateProgress(p =>
        {
            p.Phase = UpdatePhase.Idle;
            p.Message = "Başlatılıyor...";
            p.TargetVersion = targetVersion;
            p.PreviousVersion = previousVersion;
            p.Percent = 0;
            p.IsRunning = true;
            p.StartedAt = DateTimeOffset.UtcNow;
            p.FinishedAt = null;
            p.Error = null;
        });
    }

    private async Task SetPhaseAsync(UpdatePhase phase, string message, int percent, CancellationToken ct)
    {
        _store.UpdateProgress(p =>
        {
            p.Phase = phase;
            p.Message = message;
            p.Percent = percent;
            p.IsRunning = phase is not (UpdatePhase.Completed or UpdatePhase.Failed or UpdatePhase.RolledBack);
            if (!p.IsRunning) p.FinishedAt = DateTimeOffset.UtcNow;
        });
        await BroadcastStateAsync(ct);
        await BroadcastLog(message);
    }

    private async Task BroadcastStateAsync(CancellationToken ct)
    {
        var state = _store.GetState();
        await _hub.Clients.All.SendAsync("progress", state.Progress, ct);
    }

    private async Task BroadcastLog(string message)
    {
        await _hub.Clients.All.SendAsync("log", message);
    }

    private async Task<bool> WaitForHealthAsync(int hostPort, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        var host = _options.ResolveHealthCheckHost();
        var url = $"http://{host}:{hostPort}{_options.HealthPath}";

        for (var i = 0; i < _options.HealthCheckRetries; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Health check attempt {Attempt} failed for {Url}", i + 1, url);
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds), ct);
        }

        return false;
    }
}
