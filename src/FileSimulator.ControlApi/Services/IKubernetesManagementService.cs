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

/// <summary>
/// Request to create a new FTP server instance.
/// </summary>
public record CreateFtpServerRequest
{
    /// <summary>
    /// Unique name for the server instance (used in resource naming).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// FTP username for authentication.
    /// </summary>
    public string Username { get; init; } = "ftpuser";

    /// <summary>
    /// FTP password for authentication.
    /// </summary>
    public string Password { get; init; } = "ftppass";

    /// <summary>
    /// Optional specific NodePort (null = auto-assign from range 30000-32767).
    /// </summary>
    public int? NodePort { get; init; }

    /// <summary>
    /// Passive mode port range start (for FTP data connections).
    /// </summary>
    public int? PassivePortStart { get; init; }

    /// <summary>
    /// Passive mode port range end (for FTP data connections).
    /// </summary>
    public int? PassivePortEnd { get; init; }
}

/// <summary>
/// Request to create a new SFTP server instance.
/// </summary>
public record CreateSftpServerRequest
{
    /// <summary>
    /// Unique name for the server instance.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// SSH username for authentication.
    /// </summary>
    public string Username { get; init; } = "sftpuser";

    /// <summary>
    /// SSH password for authentication.
    /// </summary>
    public string Password { get; init; } = "sftppass";

    /// <summary>
    /// User ID for the SFTP user (atmoz/sftp format).
    /// </summary>
    public int Uid { get; init; } = 1000;

    /// <summary>
    /// Group ID for the SFTP user (atmoz/sftp format).
    /// </summary>
    public int Gid { get; init; } = 1000;

    /// <summary>
    /// Optional specific NodePort.
    /// </summary>
    public int? NodePort { get; init; }
}

/// <summary>
/// Request to create a new NAS (NFS) server instance.
/// </summary>
public record CreateNasServerRequest
{
    /// <summary>
    /// Unique name for the server instance.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Directory preset or custom path. Presets: "input", "output", "backup".
    /// Custom paths use the value as subdirectory under shared storage.
    /// </summary>
    public string Directory { get; init; } = "dynamic";

    /// <summary>
    /// NFS export options (e.g., "rw,sync,no_subtree_check,no_root_squash").
    /// </summary>
    public string ExportOptions { get; init; } = "rw,sync,no_subtree_check,no_root_squash";

    /// <summary>
    /// Optional specific NodePort.
    /// </summary>
    public int? NodePort { get; init; }
}
