namespace FileSimulator.ControlApi.Services;

using Microsoft.EntityFrameworkCore;
using FileSimulator.ControlApi.Data;
using FileSimulator.ControlApi.Data.Entities;

/// <summary>
/// Background service that generates hourly rollups from raw health samples.
/// Runs every hour and aggregates the previous completed hour's data.
/// </summary>
public class RollupGenerationService : BackgroundService
{
    private readonly IDbContextFactory<MetricsDbContext> _contextFactory;
    private readonly ILogger<RollupGenerationService> _logger;

    // Run every hour
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    // Initial delay to let system stabilize
    private readonly TimeSpan _initialDelay = TimeSpan.FromMinutes(5);

    public RollupGenerationService(
        IDbContextFactory<MetricsDbContext> contextFactory,
        ILogger<RollupGenerationService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RollupGenerationService started, interval: {Interval}h, initial delay: {Delay}min",
            _interval.TotalHours, _initialDelay.TotalMinutes);

        // Initial delay to let system stabilize
        await Task.Delay(_initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateRollupsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rollup generation");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("RollupGenerationService stopped");
    }

    /// <summary>
    /// Generate hourly rollups for the previous completed hour.
    /// </summary>
    private async Task GenerateRollupsAsync(CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Calculate the previous completed hour
        var now = DateTime.UtcNow;
        var currentHourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var previousHourStart = currentHourStart.AddHours(-1);
        var previousHourEnd = currentHourStart;

        _logger.LogDebug("Generating rollups for hour: {HourStart}", previousHourStart);

        // Get all raw samples for the previous hour
        var samples = await context.HealthSamples
            .Where(s => s.Timestamp >= previousHourStart && s.Timestamp < previousHourEnd)
            .ToListAsync(ct);

        if (samples.Count == 0)
        {
            _logger.LogDebug("No samples found for hour {HourStart}", previousHourStart);
            return;
        }

        // Group by server
        var serverGroups = samples.GroupBy(s => new { s.ServerId, s.ServerType });

        var rollupsCreated = 0;

        foreach (var group in serverGroups)
        {
            // Check if rollup already exists (prevent duplicates)
            var existingRollup = await context.HealthHourly
                .FirstOrDefaultAsync(r =>
                    r.HourStart == previousHourStart &&
                    r.ServerId == group.Key.ServerId,
                    ct);

            if (existingRollup != null)
            {
                _logger.LogDebug("Rollup already exists for {Server} at {Hour}",
                    group.Key.ServerId, previousHourStart);
                continue;
            }

            // Calculate aggregates
            var groupSamples = group.ToList();
            var healthySamples = groupSamples.Where(s => s.IsHealthy).ToList();
            var latencyValues = healthySamples
                .Where(s => s.LatencyMs.HasValue)
                .Select(s => s.LatencyMs!.Value)
                .OrderBy(v => v)
                .ToList();

            var rollup = new HealthHourly
            {
                HourStart = previousHourStart,
                ServerId = group.Key.ServerId,
                ServerType = group.Key.ServerType,
                SampleCount = groupSamples.Count,
                HealthyCount = healthySamples.Count,
                AvgLatencyMs = latencyValues.Count > 0 ? latencyValues.Average() : null,
                MinLatencyMs = latencyValues.Count > 0 ? latencyValues.Min() : null,
                MaxLatencyMs = latencyValues.Count > 0 ? latencyValues.Max() : null,
                P95LatencyMs = CalculatePercentile(latencyValues, 95)
            };

            context.HealthHourly.Add(rollup);
            rollupsCreated++;
        }

        if (rollupsCreated > 0)
        {
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Created {Count} hourly rollups for {Hour}",
                rollupsCreated, previousHourStart);
        }
    }

    /// <summary>
    /// Calculate percentile using linear interpolation method.
    /// </summary>
    /// <param name="sortedValues">Pre-sorted list of values</param>
    /// <param name="percentile">Percentile to calculate (0-100)</param>
    /// <returns>Calculated percentile value, or null if list is empty</returns>
    private static double? CalculatePercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return null;
        if (sortedValues.Count == 1) return sortedValues[0];

        double index = (percentile / 100.0) * (sortedValues.Count - 1);
        int lower = (int)Math.Floor(index);
        int upper = (int)Math.Ceiling(index);

        if (lower == upper) return sortedValues[lower];

        double fraction = index - lower;
        return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * fraction;
    }
}
