namespace FileSimulator.ControlApi.Services;

using Microsoft.EntityFrameworkCore;
using FileSimulator.ControlApi.Data;

/// <summary>
/// Background service that deletes data older than 7 days.
/// Runs every hour to clean up expired health samples and rollups.
/// </summary>
public class RetentionCleanupService : BackgroundService
{
    private readonly IDbContextFactory<MetricsDbContext> _contextFactory;
    private readonly ILogger<RetentionCleanupService> _logger;

    // Run every hour
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    // Initial delay to let system stabilize (10 minutes)
    private readonly TimeSpan _initialDelay = TimeSpan.FromMinutes(10);

    // Retention period (7 days)
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(7);

    public RetentionCleanupService(
        IDbContextFactory<MetricsDbContext> contextFactory,
        ILogger<RetentionCleanupService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RetentionCleanupService started, interval: {Interval}h, retention: {Retention} days, initial delay: {Delay}min",
            _interval.TotalHours, _retentionPeriod.TotalDays, _initialDelay.TotalMinutes);

        // Initial delay to let system stabilize
        await Task.Delay(_initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during retention cleanup");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("RetentionCleanupService stopped");
    }

    /// <summary>
    /// Delete data older than the retention period.
    /// Uses ExecuteDeleteAsync for efficient bulk delete (single SQL statement).
    /// </summary>
    private async Task ExecuteCleanupAsync(CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var cutoff = DateTime.UtcNow.AddDays(-_retentionPeriod.TotalDays);

        _logger.LogDebug("Retention cleanup: deleting data older than {Cutoff}", cutoff);

        // Bulk delete samples older than cutoff
        var deletedSamples = await context.HealthSamples
            .Where(s => s.Timestamp < cutoff)
            .ExecuteDeleteAsync(ct);

        // Bulk delete rollups older than cutoff
        var deletedRollups = await context.HealthHourly
            .Where(r => r.HourStart < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deletedSamples > 0 || deletedRollups > 0)
        {
            _logger.LogInformation(
                "Retention cleanup completed: {Samples} samples, {Rollups} rollups deleted (older than {Days} days)",
                deletedSamples, deletedRollups, _retentionPeriod.TotalDays);
        }
        else
        {
            _logger.LogDebug("Retention cleanup: no expired data found");
        }
    }
}
