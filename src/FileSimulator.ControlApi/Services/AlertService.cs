using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using FileSimulator.ControlApi.Data;
using FileSimulator.ControlApi.Data.Entities;
using FileSimulator.ControlApi.Hubs;
using FileSimulator.ControlApi.Models;

namespace FileSimulator.ControlApi.Services;

/// <summary>
/// Background service that monitors system conditions and manages alerts.
/// Checks disk space, Kafka health, and server health every 30 seconds.
/// </summary>
public class AlertService : IHostedService, IDisposable
{
    private readonly IDbContextFactory<MetricsDbContext> _contextFactory;
    private readonly IHubContext<AlertHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlertService> _logger;
    private Timer? _timer;

    // Alert thresholds
    private const long DiskSpaceThresholdBytes = 1024L * 1024L * 1024L; // 1 GB
    private static readonly TimeSpan AlertRetentionPeriod = TimeSpan.FromDays(7);
    private const int ConsecutiveFailuresThreshold = 3; // 3 consecutive failures = 15 seconds

    public AlertService(
        IDbContextFactory<MetricsDbContext> contextFactory,
        IHubContext<AlertHub> hubContext,
        IServiceProvider serviceProvider,
        ILogger<AlertService> logger)
    {
        _contextFactory = contextFactory;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AlertService starting with 30-second check interval");

        // Start timer with 30-second interval
        _timer = new Timer(
            callback: async _ => await CheckConditionsAsync(),
            state: null,
            dueTime: TimeSpan.FromSeconds(5), // First check after 5 seconds
            period: TimeSpan.FromSeconds(30));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AlertService stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async Task CheckConditionsAsync()
    {
        try
        {
            await CheckDiskSpaceAsync();
            await CheckKafkaHealthAsync();
            await CheckServerHealthAsync();
            await CleanupOldAlertsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking alert conditions");
        }
    }

    /// <summary>
    /// Check disk space using DiskSpaceHealthCheck.
    /// </summary>
    private async Task CheckDiskSpaceAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var healthCheck = scope.ServiceProvider.GetRequiredService<DiskSpaceHealthCheck>();

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        if (result.Status == HealthStatus.Degraded || result.Status == HealthStatus.Unhealthy)
        {
            var availableBytes = result.Data.TryGetValue("available_bytes", out var available)
                ? (long)available : 0;

            var message = $"Disk space low: {FormatBytes(availableBytes)} available. " +
                         $"Threshold: {FormatBytes(DiskSpaceThresholdBytes)}";

            await RaiseAlertAsync(
                type: "DiskSpace",
                severity: AlertSeverity.Warning,
                title: "Low Disk Space",
                message: message,
                source: "ControlAPI");
        }
        else if (result.Status == HealthStatus.Healthy)
        {
            await ResolveAlertsAsync("DiskSpace", "ControlAPI");
        }
    }

    /// <summary>
    /// Check Kafka broker health using KafkaHealthCheck.
    /// </summary>
    private async Task CheckKafkaHealthAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var healthCheck = scope.ServiceProvider.GetRequiredService<KafkaHealthCheck>();

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        if (result.Status == HealthStatus.Unhealthy)
        {
            var brokerHost = result.Data.TryGetValue("broker_host", out var host)
                ? host.ToString() : "unknown";

            var message = $"Kafka broker unreachable at {brokerHost}. " +
                         "Check Kafka pod status and network connectivity.";

            await RaiseAlertAsync(
                type: "KafkaConnection",
                severity: AlertSeverity.Critical,
                title: "Kafka Broker Unreachable",
                message: message,
                source: "ControlAPI");
        }
        else if (result.Status == HealthStatus.Healthy)
        {
            await ResolveAlertsAsync("KafkaConnection", "ControlAPI");
        }
    }

    /// <summary>
    /// Check server health by querying HealthSamples for consecutive failures.
    /// </summary>
    private async Task CheckServerHealthAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        // Get latest samples per server (last 5 samples = 25 seconds)
        var cutoffTime = DateTime.UtcNow.AddSeconds(-25);

        var serverGroups = await context.HealthSamples
            .Where(s => s.Timestamp >= cutoffTime)
            .GroupBy(s => s.ServerId)
            .ToListAsync();

        foreach (var group in serverGroups)
        {
            var serverId = group.Key;
            var recentSamples = group.OrderByDescending(s => s.Timestamp).Take(5).ToList();

            // Check if last 3 samples are unhealthy
            if (recentSamples.Count >= ConsecutiveFailuresThreshold)
            {
                var lastThree = recentSamples.Take(ConsecutiveFailuresThreshold);
                var allUnhealthy = lastThree.All(s => !s.IsHealthy);

                if (allUnhealthy)
                {
                    var message = $"Server {serverId} has failed {ConsecutiveFailuresThreshold} " +
                                 $"consecutive health checks. Server may be down or unreachable.";

                    await RaiseAlertAsync(
                        type: "ServerHealth",
                        severity: AlertSeverity.Critical,
                        title: $"Server {serverId} Unhealthy",
                        message: message,
                        source: serverId);
                }
                else
                {
                    // Server is healthy again, resolve any alerts
                    await ResolveAlertsAsync("ServerHealth", serverId);
                }
            }
        }
    }

    /// <summary>
    /// Raise an alert or update existing unresolved alert.
    /// </summary>
    private async Task RaiseAlertAsync(
        string type,
        AlertSeverity severity,
        string title,
        string message,
        string source)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        // Check for existing unresolved alert with same type and source
        var existingAlert = await context.Alerts
            .FirstOrDefaultAsync(a => a.Type == type && a.Source == source && !a.IsResolved);

        if (existingAlert != null)
        {
            // Update existing alert
            existingAlert.Message = message;
            existingAlert.Severity = severity;
            existingAlert.Title = title;
            await context.SaveChangesAsync();

            _logger.LogDebug("Updated existing alert: {Type} - {Source}", type, source);
        }
        else
        {
            // Create new alert
            var alert = new AlertEntity
            {
                Id = Guid.NewGuid(),
                Type = type,
                Severity = severity,
                Title = title,
                Message = message,
                Source = source,
                TriggeredAt = DateTime.UtcNow,
                IsResolved = false
            };

            context.Alerts.Add(alert);
            await context.SaveChangesAsync();

            _logger.LogWarning("Alert raised: [{Severity}] {Title} - {Message}",
                severity, title, message);

            // Broadcast to connected clients
            await _hubContext.Clients.All.SendAsync("AlertTriggered", alert.ToModel());
        }
    }

    /// <summary>
    /// Resolve all unresolved alerts of a given type and source.
    /// </summary>
    private async Task ResolveAlertsAsync(string type, string source)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var unresolvedAlerts = await context.Alerts
            .Where(a => a.Type == type && a.Source == source && !a.IsResolved)
            .ToListAsync();

        if (unresolvedAlerts.Any())
        {
            foreach (var alert in unresolvedAlerts)
            {
                alert.IsResolved = true;
                alert.ResolvedAt = DateTime.UtcNow;

                _logger.LogInformation("Alert resolved: [{Severity}] {Title}",
                    alert.Severity, alert.Title);

                // Broadcast resolution to connected clients
                await _hubContext.Clients.All.SendAsync("AlertResolved", alert.ToModel());
            }

            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Delete alerts older than the retention period.
    /// </summary>
    private async Task CleanupOldAlertsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var cutoffDate = DateTime.UtcNow - AlertRetentionPeriod;

        var oldAlerts = await context.Alerts
            .Where(a => a.TriggeredAt < cutoffDate)
            .ToListAsync();

        if (oldAlerts.Any())
        {
            context.Alerts.RemoveRange(oldAlerts);
            await context.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} old alerts (older than {Days} days)",
                oldAlerts.Count, AlertRetentionPeriod.TotalDays);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
