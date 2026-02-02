namespace FileSimulator.ControlApi.Services;

using System.Diagnostics;
using System.Net.Sockets;
using FileSimulator.ControlApi.Models;

public class HealthCheckService : IHealthCheckService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    public HealthCheckService(ILogger<HealthCheckService> logger)
    {
        _logger = logger;
    }

    public async Task<ServerStatus> CheckHealthAsync(DiscoveredServer server, CancellationToken ct = default)
    {
        // If pod isn't ready, don't bother with TCP check
        if (!server.PodReady)
        {
            return new ServerStatus
            {
                Name = server.Name,
                Protocol = server.Protocol,
                PodStatus = server.PodStatus,
                IsHealthy = false,
                HealthMessage = $"Pod not ready: {server.PodStatus}"
            };
        }

        var sw = Stopwatch.StartNew();
        bool isHealthy;
        string? message = null;

        try
        {
            isHealthy = await CheckTcpConnectivityAsync(
                server.ClusterIp,
                server.Port,
                ct);

            if (!isHealthy)
            {
                message = "TCP connection failed";
            }
        }
        catch (OperationCanceledException)
        {
            isHealthy = false;
            message = "Health check cancelled";
        }
        catch (Exception ex)
        {
            isHealthy = false;
            message = $"Health check error: {ex.Message}";
            _logger.LogWarning(ex, "Health check failed for {Server}", server.Name);
        }

        sw.Stop();

        return new ServerStatus
        {
            Name = server.Name,
            Protocol = server.Protocol,
            PodStatus = server.PodStatus,
            IsHealthy = isHealthy,
            HealthMessage = message,
            LatencyMs = (int)sw.ElapsedMilliseconds
        };
    }

    public async Task<IReadOnlyList<ServerStatus>> CheckAllHealthAsync(
        IEnumerable<DiscoveredServer> servers,
        CancellationToken ct = default)
    {
        // Run health checks in parallel for speed
        var tasks = servers.Select(s => CheckHealthAsync(s, ct));
        var results = await Task.WhenAll(tasks);

        var healthy = results.Count(r => r.IsHealthy);
        _logger.LogInformation(
            "Health check complete: {Healthy}/{Total} servers healthy",
            healthy, results.Length);

        return results;
    }

    private async Task<bool> CheckTcpConnectivityAsync(
        string host,
        int port,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cts.Token);
            return client.Connected;
        }
        catch (SocketException ex)
        {
            _logger.LogDebug(
                "TCP check failed for {Host}:{Port} - {Error}",
                host, port, ex.SocketErrorCode);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug(
                "TCP check timed out for {Host}:{Port}",
                host, port);
            return false;
        }
    }
}
