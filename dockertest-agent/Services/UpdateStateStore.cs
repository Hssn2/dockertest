using System.Text.Json;
using dockertest_agent.Models;
using Microsoft.Extensions.Options;

namespace dockertest_agent.Services;

public class UpdateStateStore
{
    private readonly string _statePath;
    private readonly object _lock = new();
    private AgentState _state;

    public UpdateStateStore(IWebHostEnvironment env, IOptions<AgentOptions> options)
    {
        var dir = options.Value.StateDirectory;
        if (string.IsNullOrWhiteSpace(dir))
            dir = env.ContentRootPath;

        Directory.CreateDirectory(dir);
        _statePath = Path.Combine(dir, "agent-state.json");
        _state = Load();
    }

    public AgentState GetState()
    {
        lock (_lock) return Clone(_state);
    }

    public void SetActive(string containerName, string version)
    {
        lock (_lock)
        {
            _state.ActiveContainerName = containerName;
            _state.ActiveVersion = version;
            Save();
        }
    }

    public void UpdateProgress(Action<UpdateProgress> update)
    {
        lock (_lock)
        {
            update(_state.Progress);
            Save();
        }
    }

    public void ResetProgress()
    {
        lock (_lock)
        {
            _state.Progress = new UpdateProgress();
            Save();
        }
    }

    private AgentState Load()
    {
        if (!File.Exists(_statePath))
            return new AgentState();

        try
        {
            var json = File.ReadAllText(_statePath);
            return JsonSerializer.Deserialize<AgentState>(json) ?? new AgentState();
        }
        catch
        {
            return new AgentState();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_statePath, json);
    }

    private static AgentState Clone(AgentState state) =>
        JsonSerializer.Deserialize<AgentState>(JsonSerializer.Serialize(state))!;
}
