using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using Docker.DotNet;
using Docker.DotNet.Models;
using dockertest_agent.Models;
using Microsoft.Extensions.Options;

namespace dockertest_agent.Services;

public class DockerService
{
    private readonly DockerClient _client;
    private readonly AgentOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DockerService> _logger;

    public DockerService(
        IOptions<AgentOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<DockerService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        var uri = File.Exists("/var/run/docker.sock")
            ? new Uri("unix:///var/run/docker.sock")
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new Uri("npipe://./pipe/docker_engine")
                : new Uri("unix:///var/run/docker.sock");
        _client = new DockerClientConfiguration(uri).CreateClient();
    }

    public async Task<IReadOnlyList<ManagedContainer>> ListManagedContainersAsync(string? activeName, CancellationToken ct)
    {
        var all = await _client.Containers.ListContainersAsync(new ContainersListParameters { All = true }, ct);
        var prefix = _options.ContainerNamePrefix;

        return all
            .Where(c => c.Names.Any(n => n.TrimStart('/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Select(c =>
            {
                var name = c.Names.First().TrimStart('/');
                var hostPort = c.Ports.FirstOrDefault(p => p.PrivatePort == (uint)_options.AppContainerPort)?.PublicPort;
                return new ManagedContainer
                {
                    Name = name,
                    Version = ExtractVersion(name),
                    State = c.State,
                    Image = c.Image,
                    HostPort = hostPort.HasValue ? (int)hostPort.Value : null,
                    IsActive = string.Equals(name, activeName, StringComparison.OrdinalIgnoreCase)
                };
            })
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.Name)
            .ToList();
    }

    public async Task PullImageAsync(string version, IProgress<string>? progress, CancellationToken ct)
    {
        var image = $"{_options.ImageName}:{version}";
        progress?.Report($"Image indiriliyor: {image}");

        await _client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = _options.ImageName, Tag = version },
            null,
            new Progress<JSONMessage>(msg =>
            {
                if (!string.IsNullOrWhiteSpace(msg.Status))
                    progress?.Report(msg.Status);
            }),
            ct);
    }

    public async Task LoadImageFromArchiveUrlAsync(string downloadUrl, IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report($"Arşiv indiriliyor: {downloadUrl}");

        var tempDir = Path.Combine(Path.GetTempPath(), "dockertest-agent");
        Directory.CreateDirectory(tempDir);

        var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"image-{Guid.NewGuid():N}.tar.gz";

        var tempPath = Path.Combine(tempDir, fileName);

        try
        {
            var http = _httpClientFactory.CreateClient("image-download");
            using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            await using (var netStream = await response.Content.ReadAsStreamAsync(ct))
            await using (var file = File.Create(tempPath))
            {
                var buffer = new byte[81920];
                long read = 0;
                int bytes;
                while ((bytes = await netStream.ReadAsync(buffer, ct)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, bytes), ct);
                    read += bytes;
                    if (total > 0)
                        progress?.Report($"İndiriliyor... %{read * 100 / total}");
                    else if (read % (10 * 1024 * 1024) < buffer.Length)
                        progress?.Report($"İndiriliyor... {read / (1024 * 1024)} MB");
                }
            }

            progress?.Report("Docker'a yükleniyor...");
            await using var loadStream = OpenImageLoadStream(tempPath);
            await _client.Images.LoadImageAsync(
                new ImageLoadParameters(),
                loadStream,
                new Progress<JSONMessage>(msg =>
                {
                    if (!string.IsNullOrWhiteSpace(msg.Status))
                        progress?.Report(msg.Status);
                }),
                ct);
            progress?.Report("Image yüklendi.");
            _logger.LogInformation("Loaded image from archive {Url}", downloadUrl);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    public async Task<string> RunContainerAsync(string name, int hostPort, string version, CancellationToken ct)
    {
        var image = $"{_options.ImageName}:{version}";
        var parameters = new CreateContainerParameters
        {
            Image = image,
            Name = name,
            Env = new List<string> { $"APP_VERSION={version}" },
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    [$"{_options.AppContainerPort}/tcp"] = new List<PortBinding>
                    {
                        new() { HostPort = hostPort.ToString() }
                    }
                },
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped }
            },
            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                [$"{_options.AppContainerPort}/tcp"] = default
            }
        };

        var response = await _client.Containers.CreateContainerAsync(parameters, ct);
        await _client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), ct);
        _logger.LogInformation("Container {Name} started on port {Port}", name, hostPort);
        return response.ID;
    }

    public async Task StopContainerAsync(string name, CancellationToken ct)
    {
        try
        {
            await _client.Containers.StopContainerAsync(name, new ContainerStopParameters { WaitBeforeKillSeconds = 10 }, ct);
            _logger.LogInformation("Container {Name} stopped", name);
        }
        catch (DockerContainerNotFoundException)
        {
            _logger.LogWarning("Container {Name} not found for stop", name);
        }
    }

    public async Task StartExistingContainerAsync(string name, CancellationToken ct)
    {
        await _client.Containers.StartContainerAsync(name, new ContainerStartParameters(), ct);
        _logger.LogInformation("Container {Name} restarted", name);
    }

    public async Task RemoveContainerAsync(string name, CancellationToken ct)
    {
        try
        {
            await _client.Containers.RemoveContainerAsync(name, new ContainerRemoveParameters { Force = true }, ct);
            _logger.LogInformation("Container {Name} removed", name);
        }
        catch (DockerContainerNotFoundException)
        {
            _logger.LogWarning("Container {Name} not found for remove", name);
        }
    }

    public async Task<bool> ImageExistsLocallyAsync(string version, CancellationToken ct)
    {
        var images = await _client.Images.ListImagesAsync(new ImagesListParameters { All = true }, ct);
        var tag = $"{_options.ImageName}:{version}".ToLowerInvariant();
        return images.Any(i => i.RepoTags?.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)) == true);
    }

    public async Task<IReadOnlyList<string>> ListLocalImageTagsAsync(CancellationToken ct)
    {
        var images = await _client.Images.ListImagesAsync(new ImagesListParameters { All = false }, ct);
        var imageName = _options.ImageName;
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var image in images)
        {
            foreach (var repoTag in image.RepoTags ?? [])
            {
                if (!AppVersionFilter.IsExactImageRepo(repoTag, imageName))
                    continue;

                var tag = repoTag[(repoTag.LastIndexOf(':') + 1)..];
                if (tag.Equals("latest", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!AppVersionFilter.IsAppVersion(tag))
                    continue;

                tags.Add(tag);
            }
        }

        return tags.OrderDescending(StringComparer.Ordinal).ToList();
    }

    public async Task StopContainersOnPortAsync(int hostPort, CancellationToken ct)
    {
        var containers = await _client.Containers.ListContainersAsync(new ContainersListParameters { All = true }, ct);
        foreach (var container in containers.Where(c => c.Ports.Any(p => p.PublicPort == (uint)hostPort)))
        {
            var name = container.Names.FirstOrDefault()?.TrimStart('/') ?? container.ID;
            await StopContainerAsync(name, ct);
        }
    }

    public async Task CleanupCandidatesAsync(CancellationToken ct)
    {
        var all = await _client.Containers.ListContainersAsync(new ContainersListParameters { All = true }, ct);
        foreach (var container in all)
        {
            var name = container.Names.FirstOrDefault()?.TrimStart('/') ?? "";
            if (name.Contains("_candidate", StringComparison.OrdinalIgnoreCase))
                await RemoveContainerAsync(name, ct);
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

    private static Stream OpenImageLoadStream(string path)
    {
        var file = File.OpenRead(path);
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            return new GZipStream(file, CompressionMode.Decompress, leaveOpen: false);

        return file;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // temp dosya kalırsa sorun değil
        }
    }
}
