namespace FileSimulator.ControlApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FileSimulator.ControlApi.Data;
using FileSimulator.ControlApi.Models;

/// <summary>
/// REST API controller for querying alert history and statistics.
/// </summary>
[ApiController]
[Route("api/alerts")]
public class AlertsController : ControllerBase
{
    private readonly IDbContextFactory<MetricsDbContext> _contextFactory;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        IDbContextFactory<MetricsDbContext> contextFactory,
        ILogger<AlertsController> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get all active (unresolved) alerts ordered by most recent first.
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<Alert>>> GetActiveAlerts(CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var alerts = await context.Alerts
            .Where(a => !a.IsResolved)
            .OrderByDescending(a => a.TriggeredAt)
            .ToListAsync(ct);

        var models = alerts.Select(a => a.ToModel()).ToList();

        _logger.LogDebug("Returned {Count} active alerts", models.Count);

        return Ok(models);
    }

    /// <summary>
    /// Get alert history with optional severity filter.
    /// Returns last 100 alerts by default.
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<Alert>>> GetAlertHistory(
        [FromQuery] string? severity,
        CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.Alerts.AsQueryable();

        // Apply severity filter if provided
        if (!string.IsNullOrEmpty(severity))
        {
            if (Enum.TryParse<AlertSeverity>(severity, ignoreCase: true, out var severityEnum))
            {
                query = query.Where(a => a.Severity == severityEnum);
                _logger.LogDebug("Filtering alerts by severity: {Severity}", severityEnum);
            }
            else
            {
                return BadRequest(new { error = $"Invalid severity value: {severity}. Valid values: Info, Warning, Error, Critical" });
            }
        }

        var alerts = await query
            .OrderByDescending(a => a.TriggeredAt)
            .Take(100)
            .ToListAsync(ct);

        var models = alerts.Select(a => a.ToModel()).ToList();

        _logger.LogDebug("Returned {Count} alerts from history", models.Count);

        return Ok(models);
    }

    /// <summary>
    /// Get a specific alert by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Alert>> GetAlertById(Guid id, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var alert = await context.Alerts
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (alert == null)
        {
            _logger.LogDebug("Alert not found: {AlertId}", id);
            return NotFound(new { error = $"Alert with ID {id} not found" });
        }

        return Ok(alert.ToModel());
    }

    /// <summary>
    /// Get alert statistics for the last 24 hours.
    /// Returns counts by severity and type.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetAlertStats(CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var last24Hours = DateTime.UtcNow.AddHours(-24);

        var alerts = await context.Alerts
            .Where(a => a.TriggeredAt >= last24Hours)
            .ToListAsync(ct);

        var stats = new
        {
            TotalAlerts = alerts.Count,
            ActiveAlerts = alerts.Count(a => !a.IsResolved),
            ResolvedAlerts = alerts.Count(a => a.IsResolved),
            BySeverity = alerts
                .GroupBy(a => a.Severity)
                .Select(g => new
                {
                    Severity = g.Key.ToString(),
                    Count = g.Count(),
                    Active = g.Count(a => !a.IsResolved)
                })
                .OrderBy(x => x.Severity)
                .ToList(),
            ByType = alerts
                .GroupBy(a => a.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count(),
                    Active = g.Count(a => !a.IsResolved)
                })
                .OrderByDescending(x => x.Count)
                .ToList(),
            TimeRange = new
            {
                Start = last24Hours,
                End = DateTime.UtcNow
            }
        };

        _logger.LogDebug("Returned alert stats for last 24 hours: {TotalAlerts} total, {ActiveAlerts} active",
            stats.TotalAlerts, stats.ActiveAlerts);

        return Ok(stats);
    }
}
