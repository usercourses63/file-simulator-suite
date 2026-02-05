using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using FileSimulator.ControlApi.Data;
using FileSimulator.ControlApi.Models;

namespace FileSimulator.ControlApi.Hubs;

/// <summary>
/// SignalR hub for real-time alert notifications.
/// Clients can subscribe to alert events and query alert history.
/// </summary>
public class AlertHub : Hub
{
    private readonly IDbContextFactory<MetricsDbContext> _contextFactory;
    private readonly ILogger<AlertHub> _logger;

    public AlertHub(
        IDbContextFactory<MetricsDbContext> contextFactory,
        ILogger<AlertHub> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get all currently active (unresolved) alerts.
    /// </summary>
    /// <returns>List of active alerts ordered by severity and triggered time.</returns>
    public async Task<List<Alert>> GetActiveAlerts()
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var activeAlerts = await context.Alerts
            .Where(a => !a.IsResolved)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.TriggeredAt)
            .ToListAsync();

        _logger.LogDebug("Retrieved {Count} active alerts", activeAlerts.Count);

        return activeAlerts.Select(a => a.ToModel()).ToList();
    }

    /// <summary>
    /// Get alert history (last 100 alerts, including resolved).
    /// </summary>
    /// <returns>List of recent alerts ordered by triggered time descending.</returns>
    public async Task<List<Alert>> GetAlertHistory()
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var recentAlerts = await context.Alerts
            .OrderByDescending(a => a.TriggeredAt)
            .Take(100)
            .ToListAsync();

        _logger.LogDebug("Retrieved {Count} alerts from history", recentAlerts.Count);

        return recentAlerts.Select(a => a.ToModel()).ToList();
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to AlertHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected from AlertHub with error: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected from AlertHub: {ConnectionId}",
                Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
