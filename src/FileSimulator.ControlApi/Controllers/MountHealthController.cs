namespace FileSimulator.ControlApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using FileSimulator.ControlApi.Models;
using FileSimulator.ControlApi.Services;

/// <summary>
/// REST API controller for mount health monitoring.
/// </summary>
[ApiController]
[Route("api/mount-health")]
public class MountHealthController : ControllerBase
{
    private readonly MountHealthMonitor _monitor;
    private readonly IKubernetesManagementService _managementService;
    private readonly IKubernetesDiscoveryService _discoveryService;
    private readonly ILogger<MountHealthController> _logger;

    // Same list as MountHealthMonitor
    private static readonly string[] AffectedComponents =
        ["ftp", "sftp", "http", "webdav", "smb", "management"];

    public MountHealthController(
        MountHealthMonitor monitor,
        IKubernetesManagementService managementService,
        IKubernetesDiscoveryService discoveryService,
        ILogger<MountHealthController> logger)
    {
        _monitor = monitor;
        _managementService = managementService;
        _discoveryService = discoveryService;
        _logger = logger;
    }

    /// <summary>
    /// Get current mount health status.
    /// </summary>
    [HttpGet]
    public ActionResult<MountHealthStatus> GetMountHealth()
    {
        return Ok(_monitor.GetStatus());
    }

    /// <summary>
    /// Manually trigger restart of all PVC-dependent pods.
    /// </summary>
    [HttpPost("restart-affected")]
    public async Task<IActionResult> RestartAffectedPods(CancellationToken ct)
    {
        _logger.LogWarning("Manual restart of PVC-dependent pods requested");

        var servers = await _discoveryService.DiscoverServersAsync(ct);
        var restarted = new List<string>();

        foreach (var server in servers)
        {
            var component = server.Protocol.ToLowerInvariant();
            if (!AffectedComponents.Contains(component))
                continue;

            try
            {
                await _managementService.RestartServerAsync(server.Name, ct);
                restarted.Add(server.Name);
                _logger.LogInformation("Restarted server: {Name}", server.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restart server {Name}", server.Name);
            }
        }

        return Ok(new
        {
            message = $"Restarted {restarted.Count} PVC-dependent pods",
            restartedServers = restarted
        });
    }
}
