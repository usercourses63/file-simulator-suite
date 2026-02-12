namespace FileSimulator.ControlApi.Services;

using FileSimulator.ControlApi.Models;

/// <summary>
/// Background service that monitors the 9p mount at /mnt/simulator-data for staleness.
/// Writes probe files to detect I/O failures and auto-restarts PVC-dependent pods on recovery.
/// </summary>
public class MountHealthMonitor : BackgroundService
{
    private readonly IKubernetesManagementService _managementService;
    private readonly IKubernetesDiscoveryService _discoveryService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MountHealthMonitor> _logger;

    // Current state (thread-safe via lock)
    private readonly object _stateLock = new();
    private MountState _state = MountState.Healthy;
    private int _consecutiveFailures;
    private DateTime? _lastHealthyAt;
    private DateTime _lastCheckAt = DateTime.UtcNow;
    private string? _lastMessage;
    private bool _restartTriggered;

    // PVC-dependent components (NAS/NFS and S3/MinIO use emptyDir, so they're immune)
    private static readonly string[] AffectedComponents =
        ["ftp", "sftp", "http", "webdav", "smb", "management"];

    public MountHealthMonitor(
        IKubernetesManagementService managementService,
        IKubernetesDiscoveryService discoveryService,
        IConfiguration configuration,
        ILogger<MountHealthMonitor> logger)
    {
        _managementService = managementService;
        _discoveryService = discoveryService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get the current cached mount health status.
    /// </summary>
    public MountHealthStatus GetStatus()
    {
        lock (_stateLock)
        {
            return new MountHealthStatus
            {
                State = _state,
                MountPath = GetMountPath(),
                LastHealthyAt = _lastHealthyAt,
                LastCheckAt = _lastCheckAt,
                ConsecutiveFailures = _consecutiveFailures,
                Message = _lastMessage,
                AffectedPods = _state == MountState.Healthy
                    ? []
                    : AffectedComponents.ToList()
            };
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mountPath = GetMountPath();
        var checkInterval = TimeSpan.FromSeconds(
            _configuration.GetValue("MountHealth:CheckIntervalSeconds", 10));
        var staleThreshold = _configuration.GetValue("MountHealth:StaleThresholdFailures", 3);
        var probeTimeoutMs = _configuration.GetValue("MountHealth:ProbeTimeoutMs", 2000);
        var autoRestart = _configuration.GetValue("MountHealth:AutoRestartOnRecovery", true);

        _logger.LogInformation(
            "MountHealthMonitor started. Path: {Path}, Interval: {Interval}s, StaleThreshold: {Threshold}",
            mountPath, checkInterval.TotalSeconds, staleThreshold);

        // Initial delay to let other services start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var probeSuccess = await RunProbeAsync(mountPath, probeTimeoutMs, stoppingToken);
                UpdateState(probeSuccess, staleThreshold);

                // Auto-restart affected pods when recovering from stale
                if (autoRestart && ShouldTriggerRestart())
                {
                    await RestartAffectedPodsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during mount health check cycle");
            }

            await Task.Delay(checkInterval, stoppingToken);
        }

        _logger.LogInformation("MountHealthMonitor stopped");
    }

    /// <summary>
    /// Write a probe file, read it back, then delete it.
    /// Returns true if the probe succeeds within the timeout.
    /// </summary>
    private async Task<bool> RunProbeAsync(string mountPath, int timeoutMs, CancellationToken ct)
    {
        var healthDir = Path.Combine(mountPath, ".health");
        var probeFile = Path.Combine(healthDir, $"probe-{DateTime.UtcNow:yyyyMMddHHmmss}");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            // Ensure .health directory exists
            if (!Directory.Exists(healthDir))
            {
                Directory.CreateDirectory(healthDir);
            }

            // Write probe
            var probeContent = $"probe-{DateTime.UtcNow:O}";
            await File.WriteAllTextAsync(probeFile, probeContent, cts.Token);

            // Read back
            var readContent = await File.ReadAllTextAsync(probeFile, cts.Token);

            // Verify content
            if (readContent != probeContent)
            {
                _logger.LogWarning("Mount probe content mismatch: wrote '{Expected}', read '{Actual}'",
                    probeContent, readContent);
                return false;
            }

            // Cleanup
            File.Delete(probeFile);

            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Probe timed out (not app shutdown)
            _logger.LogWarning("Mount probe timed out after {TimeoutMs}ms", timeoutMs);
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogWarning("Mount probe I/O error: {Message}", ex.Message);
            return false;
        }
        finally
        {
            // Best-effort cleanup of probe file
            try { if (File.Exists(probeFile)) File.Delete(probeFile); } catch { }
        }
    }

    /// <summary>
    /// Update internal state machine based on probe result.
    /// </summary>
    private void UpdateState(bool probeSuccess, int staleThreshold)
    {
        lock (_stateLock)
        {
            var previousState = _state;
            _lastCheckAt = DateTime.UtcNow;

            if (probeSuccess)
            {
                _consecutiveFailures = 0;

                switch (_state)
                {
                    case MountState.Healthy:
                        _lastHealthyAt = DateTime.UtcNow;
                        break;

                    case MountState.Degraded:
                        _state = MountState.Healthy;
                        _lastHealthyAt = DateTime.UtcNow;
                        _lastMessage = "Mount recovered from degraded state";
                        break;

                    case MountState.Stale:
                        _state = MountState.Recovering;
                        _lastMessage = "Mount recovered after stale. Restarting affected pods...";
                        break;

                    case MountState.Recovering:
                        // Stay in Recovering until restart completes (set to Healthy by RestartAffectedPodsAsync)
                        break;
                }
            }
            else
            {
                _consecutiveFailures++;

                if (_consecutiveFailures >= staleThreshold && _state != MountState.Stale)
                {
                    _state = MountState.Stale;
                    _lastMessage = $"Mount stale: {_consecutiveFailures} consecutive probe failures";
                }
                else if (_state == MountState.Healthy)
                {
                    _state = MountState.Degraded;
                    _lastMessage = $"Mount degraded: {_consecutiveFailures} consecutive probe failure(s)";
                }
            }

            if (_state != previousState)
            {
                _logger.LogWarning("Mount state changed: {Previous} -> {Current}. {Message}",
                    previousState, _state, _lastMessage);
            }
        }
    }

    /// <summary>
    /// Check if we should trigger a pod restart (only once per stale episode).
    /// </summary>
    private bool ShouldTriggerRestart()
    {
        lock (_stateLock)
        {
            if (_state == MountState.Recovering && !_restartTriggered)
            {
                _restartTriggered = true;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Restart all PVC-dependent pods by deleting them (deployment will recreate).
    /// </summary>
    private async Task RestartAffectedPodsAsync(CancellationToken ct)
    {
        _logger.LogWarning("Auto-restarting PVC-dependent pods after mount recovery");

        var servers = await _discoveryService.DiscoverServersAsync(ct);
        var restarted = 0;

        foreach (var server in servers)
        {
            var component = server.Protocol.ToLowerInvariant();
            if (!AffectedComponents.Contains(component))
                continue;

            try
            {
                await _managementService.RestartServerAsync(server.Name, ct);
                restarted++;
                _logger.LogInformation("Restarted PVC-dependent server: {Name} ({Protocol})",
                    server.Name, server.Protocol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restart server {Name}", server.Name);
            }
        }

        _logger.LogInformation("Restarted {Count} PVC-dependent pods", restarted);

        // Transition to Healthy
        lock (_stateLock)
        {
            _state = MountState.Healthy;
            _lastHealthyAt = DateTime.UtcNow;
            _lastMessage = $"Mount recovered. Restarted {restarted} pods.";
            _restartTriggered = false;
        }
    }

    private string GetMountPath()
    {
        return _configuration["MountHealth:MountPath"] ?? "/mnt/simulator-data";
    }
}
