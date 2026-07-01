namespace dockertest_agent.Models;

public class AgentOptions
{
    public const string SectionName = "Agent";

    public string ImageName { get; set; } = "ghcr.io/hssn2/dockertest";
    public int AppHostPort { get; set; } = 80;
    public int AppContainerPort { get; set; } = 8080;
    public string ContainerNamePrefix { get; set; } = "dockertest_";
    public string GitHubOwner { get; set; } = "Hssn2";
    public string GitHubRepo { get; set; } = "dockertest";
    public string GitHubToken { get; set; } = "";
    public string HealthPath { get; set; } = "/health";
    public string HealthCheckHost { get; set; } = "";
    public int HealthCheckRetries { get; set; } = 15;
    public int HealthCheckIntervalSeconds { get; set; } = 2;
    public string StateDirectory { get; set; } = "";

    public string ResolveToken() =>
        !string.IsNullOrWhiteSpace(GitHubToken)
            ? GitHubToken
            : Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "";

    public string ResolveHealthCheckHost()
    {
        if (!string.IsNullOrWhiteSpace(HealthCheckHost))
            return HealthCheckHost;
        return File.Exists("/.dockerenv") ? "host.docker.internal" : "localhost";
    }
}
