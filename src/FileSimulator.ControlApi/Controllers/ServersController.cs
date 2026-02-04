namespace FileSimulator.ControlApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using FileSimulator.ControlApi.Models;
using FileSimulator.ControlApi.Services;

/// <summary>
/// REST API controller for server management operations.
/// Provides CRUD endpoints for dynamic servers and lifecycle operations (start/stop/restart).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ServersController : ControllerBase
{
    private readonly IKubernetesManagementService _managementService;
    private readonly IKubernetesDiscoveryService _discoveryService;
    private readonly ILogger<ServersController> _logger;

    public ServersController(
        IKubernetesManagementService managementService,
        IKubernetesDiscoveryService discoveryService,
        ILogger<ServersController> logger)
    {
        _managementService = managementService;
        _discoveryService = discoveryService;
        _logger = logger;
    }

    /// <summary>
    /// List all servers (static and dynamic).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DiscoveredServer>>> GetServers(CancellationToken ct)
    {
        var servers = await _discoveryService.DiscoverServersAsync(ct);
        return Ok(servers);
    }

    /// <summary>
    /// Get a specific server by name.
    /// </summary>
    [HttpGet("{name}")]
    public async Task<ActionResult<DiscoveredServer>> GetServer(string name, CancellationToken ct)
    {
        var server = await _discoveryService.GetServerAsync(name, ct);
        if (server == null)
            return NotFound(new { error = $"Server '{name}' not found" });
        return Ok(server);
    }

    /// <summary>
    /// Create a new FTP server.
    /// </summary>
    [HttpPost("ftp")]
    public async Task<ActionResult<DiscoveredServer>> CreateFtpServer(
        [FromBody] CreateFtpServerRequest request,
        [FromServices] IValidator<CreateFtpServerRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.ToDictionary() });

        if (!await _managementService.IsServerNameAvailableAsync(request.Name, ct))
            return Conflict(new { error = $"Server name '{request.Name}' is already in use" });

        try
        {
            var server = await _managementService.CreateFtpServerAsync(request, ct);
            return CreatedAtAction(nameof(GetServer), new { name = server.Name }, server);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create FTP server {Name}", request.Name);
            return StatusCode(500, new { error = "Failed to create server", details = ex.Message });
        }
    }

    /// <summary>
    /// Create a new SFTP server.
    /// </summary>
    [HttpPost("sftp")]
    public async Task<ActionResult<DiscoveredServer>> CreateSftpServer(
        [FromBody] CreateSftpServerRequest request,
        [FromServices] IValidator<CreateSftpServerRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.ToDictionary() });

        if (!await _managementService.IsServerNameAvailableAsync(request.Name, ct))
            return Conflict(new { error = $"Server name '{request.Name}' is already in use" });

        try
        {
            var server = await _managementService.CreateSftpServerAsync(request, ct);
            return CreatedAtAction(nameof(GetServer), new { name = server.Name }, server);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create SFTP server {Name}", request.Name);
            return StatusCode(500, new { error = "Failed to create server", details = ex.Message });
        }
    }

    /// <summary>
    /// Create a new NAS server.
    /// </summary>
    [HttpPost("nas")]
    public async Task<ActionResult<DiscoveredServer>> CreateNasServer(
        [FromBody] CreateNasServerRequest request,
        [FromServices] IValidator<CreateNasServerRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.ToDictionary() });

        if (!await _managementService.IsServerNameAvailableAsync(request.Name, ct))
            return Conflict(new { error = $"Server name '{request.Name}' is already in use" });

        try
        {
            var server = await _managementService.CreateNasServerAsync(request, ct);
            return CreatedAtAction(nameof(GetServer), new { name = server.Name }, server);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create NAS server {Name}", request.Name);
            return StatusCode(500, new { error = "Failed to create server", details = ex.Message });
        }
    }

    /// <summary>
    /// Delete a dynamic server.
    /// </summary>
    [HttpDelete("{name}")]
    public async Task<ActionResult> DeleteServer(
        string name,
        [FromQuery] bool deleteData = false,
        CancellationToken ct = default)
    {
        try
        {
            await _managementService.DeleteServerAsync(name, deleteData, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete server {Name}", name);
            return StatusCode(500, new { error = "Failed to delete server", details = ex.Message });
        }
    }

    /// <summary>
    /// Stop a server (scale to 0).
    /// </summary>
    [HttpPost("{name}/stop")]
    public async Task<ActionResult> StopServer(string name, CancellationToken ct)
    {
        try
        {
            await _managementService.StopServerAsync(name, ct);
            return Ok(new { message = $"Server '{name}' stopped" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop server {Name}", name);
            return StatusCode(500, new { error = "Failed to stop server", details = ex.Message });
        }
    }

    /// <summary>
    /// Start a stopped server (scale to 1).
    /// </summary>
    [HttpPost("{name}/start")]
    public async Task<ActionResult> StartServer(string name, CancellationToken ct)
    {
        try
        {
            await _managementService.StartServerAsync(name, ct);
            return Ok(new { message = $"Server '{name}' started" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start server {Name}", name);
            return StatusCode(500, new { error = "Failed to start server", details = ex.Message });
        }
    }

    /// <summary>
    /// Restart a server (delete pod to recreate).
    /// </summary>
    [HttpPost("{name}/restart")]
    public async Task<ActionResult> RestartServer(string name, CancellationToken ct)
    {
        try
        {
            await _managementService.RestartServerAsync(name, ct);
            return Ok(new { message = $"Server '{name}' restarting" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart server {Name}", name);
            return StatusCode(500, new { error = "Failed to restart server", details = ex.Message });
        }
    }

    /// <summary>
    /// Check if a server name is available.
    /// </summary>
    [HttpGet("check-name/{name}")]
    public async Task<ActionResult> CheckNameAvailability(string name, CancellationToken ct)
    {
        var available = await _managementService.IsServerNameAvailableAsync(name, ct);
        return Ok(new { name, available });
    }
}
