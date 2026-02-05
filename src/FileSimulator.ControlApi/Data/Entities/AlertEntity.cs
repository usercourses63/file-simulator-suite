using FileSimulator.ControlApi.Models;

namespace FileSimulator.ControlApi.Data.Entities;

/// <summary>
/// EF Core entity for persisting alerts to SQLite.
/// Maps to the 'alerts' table with snake_case column names.
/// </summary>
public class AlertEntity
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

    /// <summary>
    /// Converts this entity to a domain model.
    /// </summary>
    public Alert ToModel()
    {
        return new Alert
        {
            Id = Id,
            Type = Type,
            Severity = Severity,
            Title = Title,
            Message = Message,
            Source = Source,
            TriggeredAt = TriggeredAt,
            ResolvedAt = ResolvedAt,
            IsResolved = IsResolved
        };
    }

    /// <summary>
    /// Creates an entity from a domain model.
    /// </summary>
    public static AlertEntity FromModel(Alert alert)
    {
        return new AlertEntity
        {
            Id = alert.Id,
            Type = alert.Type,
            Severity = alert.Severity,
            Title = alert.Title,
            Message = alert.Message,
            Source = alert.Source,
            TriggeredAt = alert.TriggeredAt,
            ResolvedAt = alert.ResolvedAt,
            IsResolved = alert.IsResolved
        };
    }
}
