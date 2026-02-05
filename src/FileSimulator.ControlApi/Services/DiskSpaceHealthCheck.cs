using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FileSimulator.ControlApi.Services;

/// <summary>
/// Health check for monitoring available disk space.
/// Returns Degraded when available space falls below threshold.
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly ILogger<DiskSpaceHealthCheck> _logger;
    private const long ThresholdBytes = 1024L * 1024L * 1024L; // 1 GB

    public DiskSpaceHealthCheck(ILogger<DiskSpaceHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dataPath = Environment.GetEnvironmentVariable("CONTROL_DATA_PATH")
                ?? (OperatingSystem.IsWindows() ? @"C:\simulator-data" : "/mnt/simulator-data");

            // Ensure the path exists
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            // Get drive info for the path
            var driveInfo = new DriveInfo(Path.GetPathRoot(dataPath) ?? dataPath);

            if (!driveInfo.IsReady)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Drive not ready: {driveInfo.Name}"));
            }

            var availableBytes = driveInfo.AvailableFreeSpace;
            var totalBytes = driveInfo.TotalSize;
            var percentFree = (double)availableBytes / totalBytes * 100;

            var data = new Dictionary<string, object>
            {
                { "available_bytes", availableBytes },
                { "total_bytes", totalBytes },
                { "percent_free", Math.Round(percentFree, 2) },
                { "threshold_bytes", ThresholdBytes },
                { "path", dataPath }
            };

            if (availableBytes < ThresholdBytes)
            {
                var message = $"Low disk space: {FormatBytes(availableBytes)} available " +
                             $"({percentFree:F1}% free), threshold is {FormatBytes(ThresholdBytes)}";

                _logger.LogWarning(message);

                return Task.FromResult(HealthCheckResult.Degraded(
                    message,
                    data: data));
            }

            _logger.LogDebug("Disk space check passed: {Available} available ({Percent:F1}% free)",
                FormatBytes(availableBytes), percentFree);

            return Task.FromResult(HealthCheckResult.Healthy(
                $"{FormatBytes(availableBytes)} available ({percentFree:F1}% free)",
                data: data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking disk space");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Error checking disk space",
                ex));
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
