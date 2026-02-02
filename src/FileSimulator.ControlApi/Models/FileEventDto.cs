namespace FileSimulator.ControlApi.Models;

/// <summary>
/// Data transfer object for file system events.
/// Represents a file creation, modification, deletion, or rename event.
/// </summary>
public class FileEventDto
{
    /// <summary>
    /// Full path to the file on the Windows host.
    /// Example: C:\simulator-data\input\data.csv
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Path relative to the base simulator-data directory.
    /// Example: input\data.csv
    /// </summary>
    public required string RelativePath { get; set; }

    /// <summary>
    /// Just the filename (without directory path).
    /// Example: data.csv
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Type of event: Created, Modified, Deleted, Renamed
    /// </summary>
    public required string EventType { get; set; }

    /// <summary>
    /// For rename events, contains the previous path.
    /// Null for other event types.
    /// </summary>
    public string? OldPath { get; set; }

    /// <summary>
    /// UTC timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Which protocol servers can see this file based on directory mapping.
    /// Example: ["FTP", "SFTP", "HTTP", "S3", "SMB", "NFS"]
    /// </summary>
    public List<string> Protocols { get; set; } = new();

    /// <summary>
    /// Whether this event is for a directory (vs a file).
    /// </summary>
    public bool IsDirectory { get; set; }
}
