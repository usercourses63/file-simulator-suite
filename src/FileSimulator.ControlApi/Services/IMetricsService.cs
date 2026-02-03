using FileSimulator.ControlApi.Data.Entities;

namespace FileSimulator.ControlApi.Services;

/// <summary>
/// Service for recording and querying health metrics.
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Records a health check sample to the database.
    /// </summary>
    /// <param name="serverId">Server identifier (e.g., "nas-input-1", "ftp")</param>
    /// <param name="serverType">Protocol type (e.g., "NAS", "FTP")</param>
    /// <param name="isHealthy">Whether the health check passed</param>
    /// <param name="latencyMs">Response latency in milliseconds (null if unhealthy)</param>
    /// <param name="ct">Cancellation token</param>
    Task RecordSampleAsync(string serverId, string serverType, bool isHealthy, double? latencyMs, CancellationToken ct);

    /// <summary>
    /// Queries raw health samples within a time range.
    /// </summary>
    /// <param name="serverId">Optional server filter (null for all servers)</param>
    /// <param name="startTime">Start of time range (UTC)</param>
    /// <param name="endTime">End of time range (UTC)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of samples ordered by timestamp descending</returns>
    Task<IReadOnlyList<HealthSample>> QuerySamplesAsync(string? serverId, DateTime startTime, DateTime endTime, CancellationToken ct);

    /// <summary>
    /// Queries hourly aggregated metrics within a time range.
    /// </summary>
    /// <param name="serverId">Optional server filter (null for all servers)</param>
    /// <param name="startTime">Start of time range (UTC)</param>
    /// <param name="endTime">End of time range (UTC)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of hourly rollups ordered by hour start descending</returns>
    Task<IReadOnlyList<HealthHourly>> QueryHourlyAsync(string? serverId, DateTime startTime, DateTime endTime, CancellationToken ct);
}
