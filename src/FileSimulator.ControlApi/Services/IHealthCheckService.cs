namespace FileSimulator.ControlApi.Services;

using FileSimulator.ControlApi.Models;

public interface IHealthCheckService
{
    /// <summary>
    /// Check health of a discovered server.
    /// </summary>
    Task<ServerStatus> CheckHealthAsync(DiscoveredServer server, CancellationToken ct = default);

    /// <summary>
    /// Check health of all discovered servers.
    /// </summary>
    Task<IReadOnlyList<ServerStatus>> CheckAllHealthAsync(
        IEnumerable<DiscoveredServer> servers,
        CancellationToken ct = default);
}
