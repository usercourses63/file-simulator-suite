namespace FileSimulator.ControlApi.Models;

/// <summary>
/// Severity levels for alerts in the system.
/// </summary>
public enum AlertSeverity
{
    /// <summary>
    /// Informational alert - no action required.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning - potential issue that should be monitored.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Critical - immediate attention required.
    /// </summary>
    Critical = 2
}
