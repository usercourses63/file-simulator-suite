namespace FileSimulator.ControlApi.Models;

/// <summary>
/// Query parameters for metrics API endpoints.
/// </summary>
public class MetricsQueryParams
{
    /// <summary>
    /// Filter by specific server ID (optional).
    /// Examples: "nas-input-1", "ftp", "sftp"
    /// </summary>
    public string? ServerId { get; set; }

    /// <summary>
    /// Filter by protocol type (optional).
    /// Examples: "NAS", "FTP", "SFTP", "HTTP", "S3", "SMB", "NFS"
    /// </summary>
    public string? ServerType { get; set; }

    /// <summary>
    /// Start of time range (ISO 8601 format, required).
    /// Will be converted to UTC for database queries.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End of time range (ISO 8601 format, required).
    /// Will be converted to UTC for database queries.
    /// </summary>
    public DateTime EndTime { get; set; }
}
