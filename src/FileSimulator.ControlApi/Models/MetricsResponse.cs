namespace FileSimulator.ControlApi.Models;

/// <summary>
/// DTO for raw health sample data.
/// </summary>
public class HealthSampleDto
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ServerId { get; set; } = default!;
    public string ServerType { get; set; } = default!;
    public bool IsHealthy { get; set; }
    public double? LatencyMs { get; set; }
}

/// <summary>
/// DTO for hourly aggregated health metrics.
/// </summary>
public class HealthHourlyDto
{
    public int Id { get; set; }
    public DateTime HourStart { get; set; }
    public string ServerId { get; set; } = default!;
    public string ServerType { get; set; } = default!;
    public int SampleCount { get; set; }
    public int HealthyCount { get; set; }
    public double? AvgLatencyMs { get; set; }
    public double? MinLatencyMs { get; set; }
    public double? MaxLatencyMs { get; set; }
    public double? P95LatencyMs { get; set; }

    /// <summary>
    /// Uptime percentage (0-100) calculated from HealthyCount/SampleCount.
    /// </summary>
    public double UptimePercent => SampleCount > 0
        ? Math.Round((double)HealthyCount / SampleCount * 100, 1)
        : 0;
}

/// <summary>
/// Response wrapper for raw samples query.
/// </summary>
public class MetricsSamplesResponse
{
    public IReadOnlyList<HealthSampleDto> Samples { get; set; } = Array.Empty<HealthSampleDto>();
    public int TotalCount { get; set; }
    public DateTime QueryStart { get; set; }
    public DateTime QueryEnd { get; set; }
}

/// <summary>
/// Response wrapper for hourly aggregations query.
/// </summary>
public class MetricsHourlyResponse
{
    public IReadOnlyList<HealthHourlyDto> Hourly { get; set; } = Array.Empty<HealthHourlyDto>();
    public int TotalCount { get; set; }
    public DateTime QueryStart { get; set; }
    public DateTime QueryEnd { get; set; }
}
