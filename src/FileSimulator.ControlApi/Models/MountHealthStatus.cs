using System.Text.Json.Serialization;

namespace FileSimulator.ControlApi.Models;

/// <summary>
/// State of the 9p mount used for shared storage.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MountState
{
    /// <summary>
    /// Mount is responsive and probe files can be written/read.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// 1-2 consecutive probe failures. May be transient.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// 3+ consecutive failures. Mount is considered stale.
    /// </summary>
    Stale = 2,

    /// <summary>
    /// Mount recovered after being Stale. Pods are being restarted.
    /// </summary>
    Recovering = 3
}

/// <summary>
/// Current health status of the 9p mount at /mnt/simulator-data.
/// </summary>
public record MountHealthStatus
{
    public required MountState State { get; init; }
    public required string MountPath { get; init; }
    public DateTime? LastHealthyAt { get; init; }
    public required DateTime LastCheckAt { get; init; }
    public required int ConsecutiveFailures { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> AffectedPods { get; init; } = [];
}
