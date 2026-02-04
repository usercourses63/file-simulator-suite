namespace FileSimulator.ControlApi.Models;

/// <summary>
/// Server discovered via Kubernetes API, before health check.
/// </summary>
public record DiscoveredServer
{
    public required string Name { get; init; }
    public required string PodName { get; init; }
    public required string Protocol { get; init; }
    public required string ServiceName { get; init; }
    public required string ClusterIp { get; init; }
    public required int Port { get; init; }
    public int? NodePort { get; init; }
    public required string PodStatus { get; init; }
    public required bool PodReady { get; init; }
    public DateTime DiscoveredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// True if created via Control API, false if deployed via Helm.
    /// </summary>
    public bool IsDynamic { get; init; }

    /// <summary>
    /// Resource manager: "control-api" for dynamic, "Helm" for static.
    /// </summary>
    public string ManagedBy { get; init; } = "Helm";

    /// <summary>
    /// Directory/mount path this server serves (e.g., "/input", "/backup", "/data").
    /// </summary>
    public string? Directory { get; init; }
}
