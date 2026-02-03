using Microsoft.EntityFrameworkCore;
using FileSimulator.ControlApi.Data;
using FileSimulator.ControlApi.Data.Entities;

namespace FileSimulator.ControlApi.Services;

/// <summary>
/// Service for recording and querying health metrics.
/// Uses IDbContextFactory for compatibility with background services.
/// </summary>
public class MetricsService : IMetricsService
{
    private readonly IDbContextFactory<MetricsDbContext> _contextFactory;
    private readonly ILogger<MetricsService> _logger;

    public MetricsService(
        IDbContextFactory<MetricsDbContext> contextFactory,
        ILogger<MetricsService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecordSampleAsync(
        string serverId,
        string serverType,
        bool isHealthy,
        double? latencyMs,
        CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var sample = new HealthSample
        {
            Timestamp = DateTime.UtcNow,
            ServerId = serverId,
            ServerType = serverType,
            IsHealthy = isHealthy,
            LatencyMs = latencyMs
        };

        context.HealthSamples.Add(sample);
        await context.SaveChangesAsync(ct);

        _logger.LogDebug(
            "Recorded health sample for {ServerId}: healthy={IsHealthy}, latency={LatencyMs}ms",
            serverId, isHealthy, latencyMs);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HealthSample>> QuerySamplesAsync(
        string? serverId,
        DateTime startTime,
        DateTime endTime,
        CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.HealthSamples
            .Where(s => s.Timestamp >= startTime && s.Timestamp <= endTime);

        if (!string.IsNullOrEmpty(serverId))
        {
            query = query.Where(s => s.ServerId == serverId);
        }

        var samples = await query
            .OrderByDescending(s => s.Timestamp)
            .ToListAsync(ct);

        _logger.LogDebug(
            "Queried {Count} samples for server={ServerId}, range={Start} to {End}",
            samples.Count, serverId ?? "all", startTime, endTime);

        return samples;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HealthHourly>> QueryHourlyAsync(
        string? serverId,
        DateTime startTime,
        DateTime endTime,
        CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.HealthHourly
            .Where(h => h.HourStart >= startTime && h.HourStart <= endTime);

        if (!string.IsNullOrEmpty(serverId))
        {
            query = query.Where(h => h.ServerId == serverId);
        }

        var rollups = await query
            .OrderByDescending(h => h.HourStart)
            .ToListAsync(ct);

        _logger.LogDebug(
            "Queried {Count} hourly rollups for server={ServerId}, range={Start} to {End}",
            rollups.Count, serverId ?? "all", startTime, endTime);

        return rollups;
    }
}
