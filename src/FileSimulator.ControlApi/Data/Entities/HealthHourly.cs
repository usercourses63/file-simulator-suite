namespace FileSimulator.ControlApi.Data.Entities;

/// <summary>
/// Hourly aggregated health metrics rollup.
/// Generated from raw HealthSample data for efficient time-range queries.
/// </summary>
public class HealthHourly
{
    /// <summary>
    /// Primary key (auto-increment).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Hour start timestamp (UTC, truncated to hour boundary).
    /// IMPORTANT: Use DateTime (not DateTimeOffset) - SQLite cannot order/compare DateTimeOffset.
    /// </summary>
    public DateTime HourStart { get; set; }

    /// <summary>
    /// Server identifier (e.g., "nas-input-1", "ftp", "sftp").
    /// </summary>
    public string ServerId { get; set; } = default!;

    /// <summary>
    /// Protocol type (e.g., "NAS", "FTP", "SFTP", "HTTP", "S3", "SMB", "NFS").
    /// </summary>
    public string ServerType { get; set; } = default!;

    /// <summary>
    /// Total number of samples in this hour.
    /// </summary>
    public int SampleCount { get; set; }

    /// <summary>
    /// Number of healthy samples in this hour.
    /// </summary>
    public int HealthyCount { get; set; }

    /// <summary>
    /// Average latency across healthy samples in milliseconds. Null if no healthy samples.
    /// </summary>
    public double? AvgLatencyMs { get; set; }

    /// <summary>
    /// Minimum latency across healthy samples in milliseconds. Null if no healthy samples.
    /// </summary>
    public double? MinLatencyMs { get; set; }

    /// <summary>
    /// Maximum latency across healthy samples in milliseconds. Null if no healthy samples.
    /// </summary>
    public double? MaxLatencyMs { get; set; }

    /// <summary>
    /// 95th percentile latency in milliseconds. Calculated in C# (SQLite lacks native percentile).
    /// </summary>
    public double? P95LatencyMs { get; set; }
}
