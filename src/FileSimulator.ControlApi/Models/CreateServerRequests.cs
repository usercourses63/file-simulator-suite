namespace FileSimulator.ControlApi.Models;

/// <summary>
/// Base properties shared by all server creation requests.
/// </summary>
public abstract record CreateServerRequestBase
{
    /// <summary>
    /// Server instance name (lowercase alphanumeric with hyphens, 3-32 chars).
    /// Combined with protocol to form unique resource names.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional NodePort (30000-32767). If null, Kubernetes auto-assigns.
    /// </summary>
    public int? NodePort { get; init; }
}

/// <summary>
/// Request to create a new FTP server instance.
/// </summary>
public record CreateFtpServerRequest : CreateServerRequestBase
{
    /// <summary>FTP username for authentication.</summary>
    public required string Username { get; init; }

    /// <summary>FTP password (min 8 chars).</summary>
    public required string Password { get; init; }

    /// <summary>Passive mode start port (optional, defaults based on NodePort).</summary>
    public int? PassivePortStart { get; init; }

    /// <summary>Passive mode end port (optional, defaults to start + 10).</summary>
    public int? PassivePortEnd { get; init; }

    /// <summary>
    /// Directory path relative to shared PVC root (optional).
    /// If specified, FTP root will be this subdirectory.
    /// Examples: "input", "output", "mydata"
    /// </summary>
    public string? Directory { get; init; }
}

/// <summary>
/// Request to create a new SFTP server instance.
/// </summary>
public record CreateSftpServerRequest : CreateServerRequestBase
{
    /// <summary>SFTP username.</summary>
    public required string Username { get; init; }

    /// <summary>SFTP password (min 8 chars).</summary>
    public required string Password { get; init; }

    /// <summary>User UID (default: 1000).</summary>
    public int Uid { get; init; } = 1000;

    /// <summary>User GID (default: 1000).</summary>
    public int Gid { get; init; } = 1000;

    /// <summary>
    /// Directory path relative to shared PVC root (optional).
    /// If specified, SFTP root will be this subdirectory.
    /// Examples: "input", "output", "mydata"
    /// </summary>
    public string? Directory { get; init; }
}

/// <summary>
/// Request to create a new NAS (NFS) server instance.
/// </summary>
public record CreateNasServerRequest : CreateServerRequestBase
{
    /// <summary>
    /// Directory path relative to shared PVC root.
    /// Presets: "input", "output", "backup" or custom path.
    /// </summary>
    public required string Directory { get; init; }

    /// <summary>NFS export options (default: rw,sync,no_subtree_check,no_root_squash).</summary>
    public string ExportOptions { get; init; } = "rw,sync,no_subtree_check,no_root_squash";
}
