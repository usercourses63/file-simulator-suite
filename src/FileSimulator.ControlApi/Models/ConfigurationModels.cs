namespace FileSimulator.ControlApi.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Full configuration export including all servers and metadata.
/// </summary>
public record ServerConfigurationExport
{
    /// <summary>Export format version for compatibility.</summary>
    public string Version { get; init; } = "2.0";

    /// <summary>When this export was created.</summary>
    public DateTime ExportedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Source namespace.</summary>
    public required string Namespace { get; init; }

    /// <summary>Helm release prefix used.</summary>
    public required string ReleasePrefix { get; init; }

    /// <summary>All server configurations.</summary>
    public required List<ServerConfiguration> Servers { get; init; }

    /// <summary>Export metadata for auditing.</summary>
    public ExportMetadata? Metadata { get; init; }
}

/// <summary>
/// Individual server configuration for export/import.
/// </summary>
public record ServerConfiguration
{
    public required string Name { get; init; }
    public required string Protocol { get; init; }
    public int? NodePort { get; init; }

    /// <summary>True if dynamically created, false if Helm-deployed.</summary>
    public bool IsDynamic { get; init; }

    /// <summary>FTP-specific settings.</summary>
    public FtpConfiguration? Ftp { get; init; }

    /// <summary>SFTP-specific settings.</summary>
    public SftpConfiguration? Sftp { get; init; }

    /// <summary>NAS-specific settings.</summary>
    public NasConfiguration? Nas { get; init; }
}

public record FtpConfiguration
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public int? PassivePortStart { get; init; }
    public int? PassivePortEnd { get; init; }
}

public record SftpConfiguration
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public int Uid { get; init; } = 1000;
    public int Gid { get; init; } = 1000;
}

public record NasConfiguration
{
    public required string Directory { get; init; }
    public string ExportOptions { get; init; } = "rw,sync,no_subtree_check,no_root_squash";
}

public record ExportMetadata
{
    public string? Description { get; init; }
    public string? ExportedBy { get; init; }
    public string? Environment { get; init; }
}

/// <summary>
/// Result of an import operation.
/// </summary>
public record ImportResult
{
    public List<string> Created { get; init; } = new();
    public List<string> Skipped { get; init; } = new();
    public Dictionary<string, string> Failed { get; init; } = new();
    public int TotalProcessed => Created.Count + Skipped.Count + Failed.Count;
}

/// <summary>
/// Strategy for handling import conflicts.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConflictResolutionStrategy
{
    /// <summary>Skip servers that conflict.</summary>
    Skip,

    /// <summary>Replace existing servers with imported ones.</summary>
    Replace,

    /// <summary>Rename imported servers to avoid conflicts.</summary>
    Rename
}

/// <summary>
/// Request body for import operation.
/// </summary>
public record ImportConfigurationRequest
{
    public required ServerConfigurationExport Configuration { get; init; }
    public ConflictResolutionStrategy Strategy { get; init; } = ConflictResolutionStrategy.Skip;
}
