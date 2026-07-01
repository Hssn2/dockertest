namespace dockertest_agent.Models;

public enum UpdatePhase
{
    Idle,
    StoppingCurrent,
    PullingImage,
    StartingProduction,
    HealthCheckingProduction,
    Completed,
    Failed,
    RollingBack,
    RolledBack
}

public class UpdateProgress
{
    public UpdatePhase Phase { get; set; } = UpdatePhase.Idle;
    public string Message { get; set; } = "Hazır";
    public string? TargetVersion { get; set; }
    public string? PreviousVersion { get; set; }
    public int Percent { get; set; }
    public bool IsRunning { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Error { get; set; }
}

public class ReleaseVersion
{
    public string Version { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTimeOffset PublishedAt { get; set; }
    public bool IsPrerelease { get; set; }
}

public class ManagedContainer
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string State { get; set; } = "";
    public string Image { get; set; } = "";
    public int? HostPort { get; set; }
    public bool IsActive { get; set; }
}

public class ReleasesResponse
{
    public IReadOnlyList<ReleaseVersion> Items { get; set; } = [];
    public string Source { get; set; } = "";
    public string? Hint { get; set; }
    public string? Error { get; set; }
    public bool TokenConfigured { get; set; }
}

public class AgentState
{
    public string? ActiveContainerName { get; set; }
    public string? ActiveVersion { get; set; }
    public UpdateProgress Progress { get; set; } = new();
}
