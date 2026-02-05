namespace FileSimulator.TestConsole.Models;

/// <summary>
/// Source of configuration data.
/// </summary>
public enum ConfigurationSource
{
    /// <summary>Configuration loaded from Control API.</summary>
    Api,

    /// <summary>Configuration loaded from appsettings.json.</summary>
    AppSettings
}

/// <summary>
/// Test configuration for all protocols.
/// </summary>
public class TestConfiguration
{
    /// <summary>Source of this configuration.</summary>
    public ConfigurationSource Source { get; set; }

    /// <summary>Servers indexed by protocol name (FTP, SFTP, etc.).</summary>
    public Dictionary<string, ServerConfig> Servers { get; set; } = new();
}

/// <summary>
/// Configuration for a single server.
/// </summary>
public class ServerConfig
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string Type { get; set; } = ""; // "ftp", "sftp", "s3", etc.
    public string Status { get; set; } = "unknown";
    public string Protocol { get; set; } = "";
    public string? Directory { get; set; }
    public string? BasePath { get; set; }
    public string? BucketName { get; set; } // S3-specific
    public string? ShareName { get; set; } // SMB-specific
    public string? MountPath { get; set; } // NFS-specific
    public string? ServiceUrl { get; set; } // S3-specific
    public string? BaseUrl { get; set; } // HTTP-specific
}
