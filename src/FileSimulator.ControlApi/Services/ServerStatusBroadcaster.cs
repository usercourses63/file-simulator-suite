namespace FileSimulator.ControlApi.Services;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using FileSimulator.ControlApi.Data;
using FileSimulator.ControlApi.Data.Entities;
using FileSimulator.ControlApi.Hubs;
using FileSimulator.ControlApi.Models;

/// <summary>
/// Background service that periodically discovers servers,
/// checks health, broadcasts status via SignalR, and records metrics to database.
/// </summary>
public class ServerStatusBroadcaster : BackgroundService
{
    private readonly IHubContext<ServerStatusHub> _hubContext;
    private readonly IHubContext<MetricsHub> _metricsHubContext;
    private readonly IKubernetesDiscoveryService _discovery;
    private readonly IHealthCheckService _healthCheck;
    private readonly IDbContextFactory<MetricsDbContext> _contextFactory;
    private readonly ILogger<ServerStatusBroadcaster> _logger;

    // Broadcast interval - matches dashboard refresh expectations
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);

    // Cache latest status for API requests
    private ServerStatusUpdate? _latestStatus;
    private readonly object _statusLock = new();

    public ServerStatusBroadcaster(
        IHubContext<ServerStatusHub> hubContext,
        IHubContext<MetricsHub> metricsHubContext,
        IKubernetesDiscoveryService discovery,
        IHealthCheckService healthCheck,
        IDbContextFactory<MetricsDbContext> contextFactory,
        ILogger<ServerStatusBroadcaster> logger)
    {
        _hubContext = hubContext;
        _metricsHubContext = metricsHubContext;
        _discovery = discovery;
        _healthCheck = healthCheck;
        _contextFactory = contextFactory;
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

        // Record metrics to database for historical storage
        await RecordMetricsAsync(statuses, ct);

        // Stream real-time samples to metrics hub for dashboards
        await _metricsHubContext.Clients.All.SendAsync(
            "MetricsSample",
            new
            {
                timestamp = DateTime.UtcNow,
                samples = statuses.Select(s => new
                {
                    serverId = s.Name,
                    serverType = s.Protocol,
                    isHealthy = s.IsHealthy,
                    latencyMs = s.IsHealthy ? (int?)s.LatencyMs : null
                })
            },
            ct);
    }

    /// <summary>
    /// Record health check results to the metrics database.
    /// Uses IDbContextFactory pattern for background service compatibility.
    /// </summary>
    private async Task RecordMetricsAsync(IReadOnlyList<ServerStatus> statuses, CancellationToken ct)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var timestamp = DateTime.UtcNow;

            foreach (var status in statuses)
            {
                var sample = new HealthSample
                {
                    Timestamp = timestamp,
                    ServerId = status.Name,
                    ServerType = status.Protocol,
                    IsHealthy = status.IsHealthy,
                    LatencyMs = status.IsHealthy ? status.LatencyMs : null
                };

                context.HealthSamples.Add(sample);
            }

            await context.SaveChangesAsync(ct);

            _logger.LogDebug("Recorded {Count} health samples to database", statuses.Count);
        }
        catch (Exception ex)
        {
            // Don't fail the broadcast cycle if metrics recording fails
            _logger.LogWarning(ex, "Failed to record health metrics to database");
        }
    }
}
