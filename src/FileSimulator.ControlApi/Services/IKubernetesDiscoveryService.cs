namespace FileSimulator.ControlApi.Services;

using FileSimulator.ControlApi.Models;

public interface IKubernetesDiscoveryService
{
    /// <summary>
    /// Discover all protocol servers in the namespace.
    /// </summary>
    Task<IReadOnlyList<DiscoveredServer>> DiscoverServersAsync(CancellationToken ct = default);

    /// <summary>
    /// Get server details by name.
    /// </summary>
    Task<DiscoveredServer?> GetServerAsync(string name, CancellationToken ct = default);
}
