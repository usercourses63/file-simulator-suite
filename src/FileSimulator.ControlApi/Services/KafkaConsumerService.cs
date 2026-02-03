using System.Runtime.CompilerServices;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using FileSimulator.ControlApi.Models;

namespace FileSimulator.ControlApi.Services;

/// <summary>
/// Kafka consumer service for reading and streaming messages from topics.
/// Creates short-lived consumers with unique group IDs to avoid offset conflicts.
/// </summary>
public class KafkaConsumerService : IKafkaConsumerService
{
    private readonly string _bootstrapServers;
    private readonly ILogger<KafkaConsumerService> _logger;

    public KafkaConsumerService(IOptions<KafkaOptions> options, ILogger<KafkaConsumerService> logger)
    {
        _bootstrapServers = options.Value.BootstrapServers;
        _logger = logger;
        _logger.LogInformation("KafkaConsumerService initialized with bootstrap servers: {Servers}", _bootstrapServers);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KafkaMessage>> GetRecentMessagesAsync(string topic, int count, CancellationToken ct)
    {
        var messages = new List<KafkaMessage>();
        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = $"dashboard-viewer-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetValueDeserializer(Deserializers.Utf8)
            .SetKeyDeserializer(Deserializers.Utf8)
            .Build();

        // Get partitions for topic
        var partitions = GetTopicPartitions(topic);
        if (!partitions.Any())
        {
            _logger.LogWarning("No partitions found for topic {Topic}", topic);
            return messages;
        }

        foreach (var partition in partitions)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var tp = new TopicPartition(topic, partition);
                var watermark = consumer.QueryWatermarkOffsets(tp, TimeSpan.FromSeconds(5));

                // Calculate start offset (N messages before high watermark)
                var startOffset = Math.Max(watermark.Low.Value, watermark.High.Value - count);
                consumer.Assign(new TopicPartitionOffset(tp, startOffset));

                // Consume messages until high watermark
                while (!ct.IsCancellationRequested)
                {
                    var result = consumer.Consume(TimeSpan.FromMilliseconds(100));
                    if (result == null) break;
                    if (result.Offset.Value >= watermark.High.Value) break;

                    messages.Add(new KafkaMessage(
                        result.Topic,
                        result.Partition.Value,
                        result.Offset.Value,
                        result.Message.Key,
                        result.Message.Value,
                        result.Message.Timestamp.UtcDateTime));

                    if (messages.Count >= count) break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read from partition {Partition} of topic {Topic}", partition, topic);
            }
        }

        // Sort by timestamp descending and take requested count
        var recentMessages = messages
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToList();

        _logger.LogDebug("Retrieved {Count} recent messages from topic {Topic}", recentMessages.Count, topic);
        return await Task.FromResult<IReadOnlyList<KafkaMessage>>(recentMessages);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<KafkaMessage> StreamMessagesAsync(
        string topic,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = $"dashboard-stream-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetValueDeserializer(Deserializers.Utf8)
            .SetKeyDeserializer(Deserializers.Utf8)
            .Build();

        consumer.Subscribe(topic);
        _logger.LogInformation("Started streaming messages from topic {Topic}", topic);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = consumer.Consume(TimeSpan.FromMilliseconds(100));
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                {
                    _logger.LogWarning("Topic {Topic} does not exist or has no partitions", topic);
                    await Task.Delay(1000, ct);
                    continue;
                }

                if (result != null)
                {
                    yield return new KafkaMessage(
                        result.Topic,
                        result.Partition.Value,
                        result.Offset.Value,
                        result.Message.Key,
                        result.Message.Value,
                        result.Message.Timestamp.UtcDateTime);
                }
            }
        }
        finally
        {
            consumer.Unsubscribe();
            _logger.LogInformation("Stopped streaming messages from topic {Topic}", topic);
        }
    }

    /// <summary>
    /// Gets partition IDs for a topic using AdminClient.
    /// </summary>
    private IEnumerable<int> GetTopicPartitions(string topic)
    {
        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = _bootstrapServers
        }).Build();

        try
        {
            var metadata = adminClient.GetMetadata(topic, TimeSpan.FromSeconds(5));
            var topicMeta = metadata.Topics.FirstOrDefault(t => t.Topic == topic);

            return topicMeta?.Partitions.Select(p => p.PartitionId) ?? Enumerable.Empty<int>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get partitions for topic {Topic}", topic);
            return Enumerable.Empty<int>();
        }
    }
}
