namespace FileSimulator.ControlApi.Data.Entities;

/// <summary>
/// Raw health check sample stored at 5-second intervals.
/// Contains per-server health status and latency measurement.
/// </summary>
public class HealthSample
{
    /// <summary>
    /// Primary key (auto-increment).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// UTC timestamp when sample was recorded.
    /// IMPORTANT: Use DateTime (not DateTimeOffset) - SQLite cannot order/compare DateTimeOffset.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Server identifier (e.g., "nas-input-1", "ftp", "sftp").
    /// </summary>
    public string ServerId { get; set; } = default!;

    /// <summary>
    /// Protocol type (e.g., "NAS", "FTP", "SFTP", "HTTP", "S3", "SMB", "NFS").
    /// </summary>
    public string ServerType { get; set; } = default!;

    /// <summary>
    /// Whether the server responded successfully to health check.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Response latency in milliseconds. Null if unhealthy (no response).
    /// </summary>
    public double? LatencyMs { get; set; }
}
