namespace FileSimulator.ControlApi.Models;

/// <summary>
/// Represents a file or directory in the file tree.
/// </summary>
public record FileNodeDto
{
    /// <summary>Path relative to base directory (used as ID in frontend tree)</summary>
    public required string Id { get; init; }

    /// <summary>File or directory name</summary>
    public required string Name { get; init; }

    /// <summary>True if this is a directory</summary>
    public required bool IsDirectory { get; init; }

    /// <summary>File size in bytes (null for directories)</summary>
    public long? Size { get; init; }

    /// <summary>Last modified timestamp (ISO 8601)</summary>
    public required string Modified { get; init; }

    /// <summary>Protocols that can access this path</summary>
    public required List<string> Protocols { get; init; }

    /// <summary>Child nodes (populated if directory and expanded)</summary>
    public List<FileNodeDto>? Children { get; init; }
}
