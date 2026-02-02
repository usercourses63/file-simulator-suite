namespace FileSimulator.ControlApi.Models;

/// <summary>
/// Real-time status of a protocol server, broadcast via SignalR.
/// </summary>
public record ServerStatus
{
    public required string Name { get; init; }
    public required string Protocol { get; init; }  // FTP, SFTP, NFS, HTTP, S3, SMB
    public required string PodStatus { get; init; }  // Running, Pending, Failed
    public required bool IsHealthy { get; init; }
    public string? HealthMessage { get; init; }
    public int? LatencyMs { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Collection of all server statuses, broadcast as single message.
/// </summary>
public record ServerStatusUpdate
{
    public required IReadOnlyList<ServerStatus> Servers { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int TotalServers => Servers.Count;
    public int HealthyServers => Servers.Count(s => s.IsHealthy);
}
