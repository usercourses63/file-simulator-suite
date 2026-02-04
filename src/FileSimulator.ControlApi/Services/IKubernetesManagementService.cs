namespace FileSimulator.ControlApi.Services;

using k8s.Models;
using FileSimulator.ControlApi.Models;

/// <summary>
/// Service for creating, updating, and deleting dynamic server instances.
/// Uses Kubernetes API directly (not Helm) for runtime management.
/// </summary>
public interface IKubernetesManagementService
{
    /// <summary>
    /// Create a new FTP server instance.
    /// </summary>
    /// <param name="request">FTP server configuration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created server details</returns>
    Task<DiscoveredServer> CreateFtpServerAsync(CreateFtpServerRequest request, CancellationToken ct = default);

    /// <summary>
    /// Create a new SFTP server instance.
    /// </summary>
    Task<DiscoveredServer> CreateSftpServerAsync(CreateSftpServerRequest request, CancellationToken ct = default);

    /// <summary>
    /// Create a new NAS (NFS) server instance.
    /// </summary>
    Task<DiscoveredServer> CreateNasServerAsync(CreateNasServerRequest request, CancellationToken ct = default);

    /// <summary>
    /// Delete a dynamic server by name. Cleans up deployment, service, and optionally data.
    /// </summary>
    /// <param name="serverName">Name of the server instance</param>
    /// <param name="deleteData">For NAS servers: whether to delete files from Windows directory</param>
    /// <param name="ct">Cancellation token</param>
    Task DeleteServerAsync(string serverName, bool deleteData = false, CancellationToken ct = default);

    /// <summary>
    /// Stop a server (scale deployment to 0 replicas).
    /// </summary>
    Task StopServerAsync(string serverName, CancellationToken ct = default);

    /// <summary>
    /// Start a stopped server (scale deployment to 1 replica).
    /// </summary>
    Task StartServerAsync(string serverName, CancellationToken ct = default);

    /// <summary>
    /// Restart a server by deleting its pod (deployment recreates it).
    /// </summary>
    Task RestartServerAsync(string serverName, CancellationToken ct = default);

    /// <summary>
    /// Check if a server name is available (not already used).
    /// </summary>
    Task<bool> IsServerNameAvailableAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Get the control plane pod (owner for dynamic resources).
    /// </summary>
    Task<V1Pod> GetControlPlanePodAsync(CancellationToken ct = default);
}

// Note: CreateFtpServerRequest, CreateSftpServerRequest, and CreateNasServerRequest
// are defined in Models/CreateServerRequests.cs and imported via the using statement above.
