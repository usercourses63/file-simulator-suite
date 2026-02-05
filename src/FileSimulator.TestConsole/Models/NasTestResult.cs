namespace FileSimulator.TestConsole.Models;

/// <summary>
/// Test result for a single NAS server.
/// </summary>
public class NasTestResult
{
    /// <summary>Server name (e.g., "nas-input-1", "nas-backup").</summary>
    public string ServerName { get; set; } = "";

    /// <summary>Server type: input, backup, or output.</summary>
    public string ServerType { get; set; } = "";

    /// <summary>TCP connection succeeded.</summary>
    public bool TcpConnected { get; set; }

    /// <summary>Time taken to establish TCP connection (ms).</summary>
    public long ConnectMs { get; set; }

    /// <summary>Windows mount path exists at C:\simulator-data\nas-{name}\.</summary>
    public bool MountPathExists { get; set; }

    /// <summary>Write operation succeeded.</summary>
    public bool? WriteSuccess { get; set; }

    /// <summary>Time taken to write file (ms).</summary>
    public long? WriteMs { get; set; }

    /// <summary>Read operation succeeded.</summary>
    public bool? ReadSuccess { get; set; }

    /// <summary>Time taken to read file (ms).</summary>
    public long? ReadMs { get; set; }

    /// <summary>List operation succeeded.</summary>
    public bool? ListSuccess { get; set; }

    /// <summary>Time taken to list directory (ms).</summary>
    public long? ListMs { get; set; }

    /// <summary>Delete operation succeeded.</summary>
    public bool? DeleteSuccess { get; set; }

    /// <summary>Time taken to delete file (ms).</summary>
    public long? DeleteMs { get; set; }

    /// <summary>For output servers: sync from NFS to Windows verified.</summary>
    public bool? SyncVerified { get; set; }

    /// <summary>Error message if any operation failed.</summary>
    public string? Error { get; set; }
}
