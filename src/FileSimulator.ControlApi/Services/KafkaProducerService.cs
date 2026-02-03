using Confluent.Kafka;
using Microsoft.Extensions.Options;
using FileSimulator.ControlApi.Models;

namespace FileSimulator.ControlApi.Services;

/// <summary>
/// Kafka producer service for sending messages to topics.
/// Uses Confluent.Kafka IProducer with idempotence enabled.
/// </summary>
public class KafkaProducerService : IKafkaProducerService
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(IOptions<KafkaOptions> options, ILogger<KafkaProducerService> logger)
    {
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.All,  // Wait for all replicas to acknowledge
            EnableIdempotence = true  // Exactly-once delivery semantics
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
        _logger.LogInformation("KafkaProducerService initialized with bootstrap servers: {Servers}",
            options.Value.BootstrapServers);
    }

    /// <inheritdoc />
    public async Task<ProduceMessageResult> ProduceAsync(ProduceMessageRequest request, CancellationToken ct)
    {
        var message = new Message<string, string>
        {
            Key = request.Key,
            Value = request.Value
        };

        var result = await _producer.ProduceAsync(request.Topic, message, ct);

        _logger.LogDebug("Produced message to {Topic}:{Partition}@{Offset}",
            result.Topic, result.Partition.Value, result.Offset.Value);

        return new ProduceMessageResult(
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            result.Timestamp.UtcDateTime);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _producer?.Dispose();
    }
}
