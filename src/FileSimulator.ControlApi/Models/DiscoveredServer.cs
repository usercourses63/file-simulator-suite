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
}
