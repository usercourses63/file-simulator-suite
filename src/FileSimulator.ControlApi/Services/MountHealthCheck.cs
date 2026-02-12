namespace FileSimulator.ControlApi.Services;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using FileSimulator.ControlApi.Models;

/// <summary>
/// ASP.NET Core health check that reports mount health status.
/// Reads cached state from MountHealthMonitor.
/// </summary>
public class MountHealthCheck : IHealthCheck
{
    private readonly MountHealthMonitor _monitor;

    public MountHealthCheck(MountHealthMonitor monitor)
    {
        _monitor = monitor;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var status = _monitor.GetStatus();

        var data = new Dictionary<string, object>
        {
            ["state"] = status.State.ToString(),
            ["mount_path"] = status.MountPath,
            ["consecutive_failures"] = status.ConsecutiveFailures
        };

        if (status.LastHealthyAt.HasValue)
            data["last_healthy_at"] = status.LastHealthyAt.Value;

        var result = status.State switch
        {
            MountState.Healthy => HealthCheckResult.Healthy(
                description: "Mount is responsive", data: data),

            MountState.Degraded or MountState.Recovering => HealthCheckResult.Degraded(
                description: status.Message ?? "Mount is degraded", data: data),

            MountState.Stale => HealthCheckResult.Unhealthy(
                description: status.Message ?? "Mount is stale", data: data),

            _ => HealthCheckResult.Unhealthy(
                description: "Unknown mount state", data: data)
        };

        return Task.FromResult(result);
    }
}
