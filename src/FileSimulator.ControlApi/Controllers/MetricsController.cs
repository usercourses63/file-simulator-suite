namespace FileSimulator.ControlApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FileSimulator.ControlApi.Data;
using FileSimulator.ControlApi.Models;
using FileSimulator.ControlApi.Services;

/// <summary>
/// REST API controller for querying historical health metrics.
/// </summary>
[ApiController]
[Route("api/metrics")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsService _metricsService;
    private readonly IDbContextFactory<MetricsDbContext> _contextFactory;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IMetricsService metricsService,
        IDbContextFactory<MetricsDbContext> contextFactory,
        ILogger<MetricsController> logger)
    {
        _metricsService = metricsService;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get raw samples for a time range.
    /// Best for ranges under 24 hours.
    /// </summary>
    [HttpGet("samples")]
    public async Task<ActionResult<MetricsSamplesResponse>> GetSamples(
        [FromQuery] MetricsQueryParams query,
        CancellationToken ct)
    {
        // Validate time range
        if (query.EndTime <= query.StartTime)
        {
            return BadRequest(new { error = "EndTime must be after StartTime" });
        }

        // Limit raw sample queries to reasonable ranges
        var range = query.EndTime - query.StartTime;
        if (range > TimeSpan.FromDays(7))
        {
            return BadRequest(new { error = "Raw sample queries limited to 7 days. Use /api/metrics/hourly for longer ranges." });
        }

        var samples = await _metricsService.QuerySamplesAsync(
            query.ServerId,
            query.StartTime.ToUniversalTime(),
            query.EndTime.ToUniversalTime(),
            ct);

        // Apply ServerType filter if provided
        if (!string.IsNullOrEmpty(query.ServerType))
        {
            samples = samples.Where(s => s.ServerType.Equals(query.ServerType, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var dtos = samples.Select(s => new HealthSampleDto
        {
            Id = s.Id,
            Timestamp = s.Timestamp,
            ServerId = s.ServerId,
            ServerType = s.ServerType,
            IsHealthy = s.IsHealthy,
            LatencyMs = s.LatencyMs
        }).ToList();

        _logger.LogDebug("Returned {Count} samples for {ServerId} in range {Start} to {End}",
            dtos.Count, query.ServerId ?? "all servers", query.StartTime, query.EndTime);

        return Ok(new MetricsSamplesResponse
        {
            Samples = dtos,
            TotalCount = dtos.Count,
            QueryStart = query.StartTime,
            QueryEnd = query.EndTime
        });
    }

    /// <summary>
    /// Get hourly aggregations for a time range.
    /// Best for ranges over 24 hours.
    /// </summary>
    [HttpGet("hourly")]
    public async Task<ActionResult<MetricsHourlyResponse>> GetHourly(
        [FromQuery] MetricsQueryParams query,
        CancellationToken ct)
    {
        // Validate time range
        if (query.EndTime <= query.StartTime)
        {
            return BadRequest(new { error = "EndTime must be after StartTime" });
        }

        var hourly = await _metricsService.QueryHourlyAsync(
            query.ServerId,
            query.StartTime.ToUniversalTime(),
            query.EndTime.ToUniversalTime(),
            ct);

        // Apply ServerType filter if provided
        if (!string.IsNullOrEmpty(query.ServerType))
        {
            hourly = hourly.Where(h => h.ServerType.Equals(query.ServerType, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var dtos = hourly.Select(h => new HealthHourlyDto
        {
            Id = h.Id,
            HourStart = h.HourStart,
            ServerId = h.ServerId,
            ServerType = h.ServerType,
            SampleCount = h.SampleCount,
            HealthyCount = h.HealthyCount,
            AvgLatencyMs = h.AvgLatencyMs,
            MinLatencyMs = h.MinLatencyMs,
            MaxLatencyMs = h.MaxLatencyMs,
            P95LatencyMs = h.P95LatencyMs
        }).ToList();

        _logger.LogDebug("Returned {Count} hourly rollups for {ServerId} in range {Start} to {End}",
            dtos.Count, query.ServerId ?? "all servers", query.StartTime, query.EndTime);

        return Ok(new MetricsHourlyResponse
        {
            Hourly = dtos,
            TotalCount = dtos.Count,
            QueryStart = query.StartTime,
            QueryEnd = query.EndTime
        });
    }

    /// <summary>
    /// Get server list with available metrics date range.
    /// </summary>
    [HttpGet("servers")]
    public async Task<IActionResult> GetServersWithMetrics(CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var servers = await context.HealthSamples
            .GroupBy(s => new { s.ServerId, s.ServerType })
            .Select(g => new
            {
                ServerId = g.Key.ServerId,
                ServerType = g.Key.ServerType,
                FirstSample = g.Min(s => s.Timestamp),
                LastSample = g.Max(s => s.Timestamp),
                TotalSamples = g.Count()
            })
            .ToListAsync(ct);

        _logger.LogDebug("Returned {Count} servers with metrics", servers.Count);

        return Ok(servers);
    }
}
