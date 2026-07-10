using dockertest_catalog.Services;
using Microsoft.AspNetCore.Mvc;

namespace dockertest_catalog.Controllers;

[ApiController]
[Route("api")]
public class ReleasesController : ControllerBase
{
    private readonly ReleaseStorageService _storage;

    public ReleasesController(ReleaseStorageService storage) => _storage = storage;

    [HttpGet("releases")]
    public IActionResult GetReleases() => Ok(_storage.GetCatalog());

    [HttpPost("releases/upload")]
    [RequestSizeLimit(long.MaxValue)]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string? version,
        [FromForm] string? name,
        CancellationToken ct)
    {
        var (ok, error, item) = await _storage.UploadAsync(file, version, name, ct);
        if (!ok)
            return BadRequest(new { error });

        return Ok(new { message = $"Yüklendi: v{item!.Version}", item });
    }

    [HttpDelete("releases/{version}")]
    public IActionResult Delete(string version)
    {
        var (ok, error) = _storage.Delete(version);
        if (!ok)
            return NotFound(new { error });

        return Ok(new { message = $"Silindi: v{version}" });
    }

    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(new { status = "healthy", service = "dockertest-catalog" });
}
