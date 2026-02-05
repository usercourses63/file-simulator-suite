using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Sockets;

namespace FileSimulator.ControlApi.Services;

/// <summary>
/// Health check for monitoring Kafka broker connectivity.
/// Returns Unhealthy when broker is unreachable.
/// </summary>
public class KafkaHealthCheck : IHealthCheck
{
    private readonly ILogger<KafkaHealthCheck> _logger;
    private readonly string _brokerHost;
    private readonly int _brokerPort;

    public KafkaHealthCheck(ILogger<KafkaHealthCheck> logger, IConfiguration configuration)
    {
        _logger = logger;

        // Get Kafka broker from configuration (Phase 10 pattern)
        var kafkaBootstrap = configuration.GetValue<string>("Kafka:BootstrapServers")
            ?? "file-sim-file-simulator-kafka:9092";

        var parts = kafkaBootstrap.Split(':');
        _brokerHost = parts[0];
        _brokerPort = parts.Length > 1 && int.TryParse(parts[1], out var port) ? port : 9092;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(_brokerHost, _brokerPort);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                var message = $"Kafka broker connection timeout: {_brokerHost}:{_brokerPort}";
                _logger.LogWarning(message);

                return HealthCheckResult.Unhealthy(
                    message,
                    data: new Dictionary<string, object>
                    {
                        { "broker_host", _brokerHost },
                        { "broker_port", _brokerPort },
                        { "timeout_seconds", 5 }
                    });
            }

            await connectTask;

            if (client.Connected)
            {
                _logger.LogDebug("Kafka broker connection successful: {Host}:{Port}",
                    _brokerHost, _brokerPort);

                return HealthCheckResult.Healthy(
                    $"Kafka broker reachable at {_brokerHost}:{_brokerPort}",
                    data: new Dictionary<string, object>
                    {
                        { "broker_host", _brokerHost },
                        { "broker_port", _brokerPort }
                    });
            }

            var failMessage = $"Kafka broker unreachable: {_brokerHost}:{_brokerPort}";
            _logger.LogWarning(failMessage);

            return HealthCheckResult.Unhealthy(
                failMessage,
                data: new Dictionary<string, object>
                {
                    { "broker_host", _brokerHost },
                    { "broker_port", _brokerPort }
                });
        }
        catch (Exception ex)
        {
            var errorMessage = $"Kafka broker connection failed: {_brokerHost}:{_brokerPort}";
            _logger.LogError(ex, errorMessage);

            return HealthCheckResult.Unhealthy(
                errorMessage,
                ex,
                data: new Dictionary<string, object>
                {
                    { "broker_host", _brokerHost },
                    { "broker_port", _brokerPort },
                    { "exception_message", ex.Message }
                });
        }
    }
}
