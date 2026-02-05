namespace FileSimulator.TestConsole.Models;

/// <summary>
/// Test result for dynamic server lifecycle (create, wait, connect, delete).
/// </summary>
public class DynamicServerTestResult
{
    /// <summary>Protocol being tested (FTP, SFTP, NAS).</summary>
    public string Protocol { get; set; } = "";

    /// <summary>Name of the dynamic server created.</summary>
    public string ServerName { get; set; } = "";

    /// <summary>Whether server was successfully created.</summary>
    public bool CreateSuccess { get; set; }

    /// <summary>Time taken to create server (milliseconds).</summary>
    public long CreateMs { get; set; }

    /// <summary>Whether server became ready within timeout.</summary>
    public bool WaitForReadySuccess { get; set; }

    /// <summary>Time taken for server to become ready (milliseconds).</summary>
    public long WaitForReadyMs { get; set; }

    /// <summary>Whether connectivity test succeeded.</summary>
    public bool ConnectivitySuccess { get; set; }

    /// <summary>Time taken for connectivity test (milliseconds).</summary>
    public long ConnectivityMs { get; set; }

    /// <summary>Whether file operations test succeeded (null if skipped).</summary>
    public bool? FileOperationSuccess { get; set; }

    /// <summary>Time taken for file operations test (null if skipped).</summary>
    public long? FileOperationMs { get; set; }

    /// <summary>Whether server was successfully deleted.</summary>
    public bool DeleteSuccess { get; set; }

    /// <summary>Time taken to delete server (milliseconds).</summary>
    public long DeleteMs { get; set; }

    /// <summary>Total time for entire test lifecycle (milliseconds).</summary>
    public long TotalMs { get; set; }

    /// <summary>Error message if any step failed.</summary>
    public string? Error { get; set; }
}
