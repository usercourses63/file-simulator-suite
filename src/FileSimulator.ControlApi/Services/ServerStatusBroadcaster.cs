namespace FileSimulator.ControlApi.Services;

using Microsoft.AspNetCore.SignalR;
using FileSimulator.ControlApi.Hubs;
using FileSimulator.ControlApi.Models;

/// <summary>
/// Background service that periodically discovers servers,
/// checks health, and broadcasts status via SignalR.
/// </summary>
public class ServerStatusBroadcaster : BackgroundService
{
    private readonly IHubContext<ServerStatusHub> _hubContext;
    private readonly IKubernetesDiscoveryService _discovery;
    private readonly IHealthCheckService _healthCheck;
    private readonly ILogger<ServerStatusBroadcaster> _logger;

    // Broadcast interval - matches dashboard refresh expectations
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);

    // Cache latest status for API requests
    private ServerStatusUpdate? _latestStatus;
    private readonly object _statusLock = new();

    public ServerStatusBroadcaster(
        IHubContext<ServerStatusHub> hubContext,
        IKubernetesDiscoveryService discovery,
        IHealthCheckService healthCheck,
        ILogger<ServerStatusBroadcaster> logger)
    {
        _hubContext = hubContext;
        _discovery = discovery;
        _healthCheck = healthCheck;
        _logger = logger;
    }

    /// <summary>
    /// Get the latest cached status (for REST API endpoint).
    /// </summary>
    public ServerStatusUpdate? GetLatestStatus()
    {
        lock (_statusLock)
        {
            return _latestStatus;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ServerStatusBroadcaster started, interval: {Interval}s",
            _interval.TotalSeconds);

        // Initial delay to let K8s client initialize
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BroadcastStatusAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during status broadcast");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("ServerStatusBroadcaster stopped");
    }

    private async Task BroadcastStatusAsync(CancellationToken ct)
    {
        // Discover servers
        var servers = await _discovery.DiscoverServersAsync(ct);

        if (servers.Count == 0)
        {
            _logger.LogWarning("No servers discovered - is RBAC configured?");
            return;
        }

        // Check health of all servers
        var statuses = await _healthCheck.CheckAllHealthAsync(servers, ct);

        // Create update message
        var update = new ServerStatusUpdate
        {
            Servers = statuses
        };

        // Cache for REST API
        lock (_statusLock)
        {
            _latestStatus = update;
        }

        // Broadcast to all connected clients
        await _hubContext.Clients.All.SendAsync(
            "ServerStatusUpdate",
            update,
            ct);

        _logger.LogDebug(
            "Broadcast status: {Healthy}/{Total} healthy",
            update.HealthyServers, update.TotalServers);
    }
}
