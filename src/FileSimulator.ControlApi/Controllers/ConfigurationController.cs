namespace FileSimulator.ControlApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using FileSimulator.ControlApi.Models;
using FileSimulator.ControlApi.Services;

/// <summary>
/// REST API controller for configuration export and import operations.
/// Provides endpoints for exporting simulator configuration for backup
/// and replicating test environments across machines.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly IConfigurationExportService _exportService;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(
        IConfigurationExportService exportService,
        ILogger<ConfigurationController> logger)
    {
        _exportService = exportService;
        _logger = logger;
    }

    /// <summary>
    /// Export current configuration as JSON file download.
    /// </summary>
    [HttpGet("export")]
    [Produces("application/json")]
    public async Task<IActionResult> ExportConfiguration(CancellationToken ct)
    {
        try
        {
            var config = await _exportService.ExportConfigurationAsync(ct);

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var fileName = $"file-simulator-config-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            var bytes = Encoding.UTF8.GetBytes(json);

            return File(bytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export configuration");
            return StatusCode(500, new { error = "Failed to export configuration", details = ex.Message });
        }
    }

    /// <summary>
    /// Get configuration as JSON response (for preview, not file download).
    /// </summary>
    [HttpGet("preview")]
    public async Task<ActionResult<ServerConfigurationExport>> PreviewConfiguration(CancellationToken ct)
    {
        try
        {
            var config = await _exportService.ExportConfigurationAsync(ct);
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration preview");
            return StatusCode(500, new { error = "Failed to get configuration", details = ex.Message });
        }
    }

    /// <summary>
    /// Validate import configuration without applying changes.
    /// </summary>
    [HttpPost("validate")]
    public async Task<ActionResult<ImportResult>> ValidateImport(
        [FromBody] ServerConfigurationExport config,
        CancellationToken ct)
    {
        try
        {
            var result = await _exportService.ValidateImportAsync(config, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate import");
            return StatusCode(500, new { error = "Failed to validate import", details = ex.Message });
        }
    }

    /// <summary>
    /// Import configuration from JSON.
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<ImportResult>> ImportConfiguration(
        [FromBody] ImportConfigurationRequest request,
        CancellationToken ct)
    {
        try
        {
            // Basic validation
            if (request.Configuration?.Servers == null || !request.Configuration.Servers.Any())
            {
                return BadRequest(new { error = "Configuration must contain at least one server" });
            }

            if (string.IsNullOrEmpty(request.Configuration.Version))
            {
                return BadRequest(new { error = "Configuration must include version" });
            }

            var result = await _exportService.ImportConfigurationAsync(
                request.Configuration,
                request.Strategy,
                ct);

            if (result.Failed.Any())
            {
                // Partial success - return 207 Multi-Status
                return StatusCode(207, result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import configuration");
            return StatusCode(500, new { error = "Failed to import configuration", details = ex.Message });
        }
    }

    /// <summary>
    /// Import configuration from uploaded JSON file.
    /// </summary>
    [HttpPost("import/file")]
    public async Task<ActionResult<ImportResult>> ImportConfigurationFile(
        IFormFile file,
        [FromQuery] ConflictResolutionStrategy strategy = ConflictResolutionStrategy.Skip,
        CancellationToken ct = default)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file provided" });
            }

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "File must be JSON format" });
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var json = await reader.ReadToEndAsync(ct);

            var config = JsonSerializer.Deserialize<ServerConfigurationExport>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
            {
                return BadRequest(new { error = "Invalid JSON format" });
            }

            var result = await _exportService.ImportConfigurationAsync(config, strategy, ct);

            if (result.Failed.Any())
            {
                return StatusCode(207, result);
            }

            return Ok(result);
        }
        catch (JsonException ex)
        {
            return BadRequest(new { error = "Invalid JSON format", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import configuration file");
            return StatusCode(500, new { error = "Failed to import configuration", details = ex.Message });
        }
    }

    /// <summary>
    /// Get configuration templates for common scenarios.
    /// </summary>
    [HttpGet("templates")]
    public ActionResult<Dictionary<string, object>> GetTemplates()
    {
        var templates = new Dictionary<string, object>
        {
            ["basic"] = new ServerConfigurationExport
            {
                Version = "2.0",
                Namespace = "file-simulator",
                ReleasePrefix = "file-sim-file-simulator",
                Servers = new List<ServerConfiguration>
                {
                    new() { Name = "ftp-1", Protocol = "FTP", IsDynamic = true,
                        Ftp = new FtpConfiguration { Username = "user1", Password = "password123" } }
                },
                Metadata = new ExportMetadata { Description = "Basic single FTP server" }
            },
            ["multi-nas"] = new ServerConfigurationExport
            {
                Version = "2.0",
                Namespace = "file-simulator",
                ReleasePrefix = "file-sim-file-simulator",
                Servers = new List<ServerConfiguration>
                {
                    new() { Name = "nas-input-1", Protocol = "NFS", IsDynamic = true,
                        Nas = new NasConfiguration { Directory = "input" } },
                    new() { Name = "nas-input-2", Protocol = "NFS", IsDynamic = true,
                        Nas = new NasConfiguration { Directory = "input" } },
                    new() { Name = "nas-output-1", Protocol = "NFS", IsDynamic = true,
                        Nas = new NasConfiguration { Directory = "output" } }
                },
                Metadata = new ExportMetadata { Description = "Multi-NAS setup with 2 input, 1 output" }
            },
            ["full-stack"] = new ServerConfigurationExport
            {
                Version = "2.0",
                Namespace = "file-simulator",
                ReleasePrefix = "file-sim-file-simulator",
                Servers = new List<ServerConfiguration>
                {
                    new() { Name = "ftp-primary", Protocol = "FTP", IsDynamic = true,
                        Ftp = new FtpConfiguration { Username = "ftpuser", Password = "ftppass" } },
                    new() { Name = "sftp-secure", Protocol = "SFTP", IsDynamic = true,
                        Sftp = new SftpConfiguration { Username = "sftpuser", Password = "sftppass" } },
                    new() { Name = "nas-data", Protocol = "NFS", IsDynamic = true,
                        Nas = new NasConfiguration { Directory = "data" } }
                },
                Metadata = new ExportMetadata { Description = "Full stack with FTP, SFTP, and NAS" }
            }
        };

        return Ok(templates);
    }
}
