namespace FileSimulator.ControlApi.Models;

/// <summary>
/// Domain model for system alerts.
/// Represents runtime conditions requiring user attention.
/// </summary>
public class Alert
{
    /// <summary>
    /// Unique identifier for the alert.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Type of alert (e.g., "DiskSpace", "KafkaConnection", "ServerHealth").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Severity level of the alert.
    /// </summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>
    /// Short title for the alert.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed message explaining the alert condition.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Source of the alert (e.g., server ID, system component).
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// When the alert was first triggered (UTC).
    /// </summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>
    /// When the alert condition was resolved (UTC). Null if still active.
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Whether the alert has been resolved.
    /// </summary>
    public bool IsResolved { get; set; }
}
