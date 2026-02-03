using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using FileSimulator.ControlApi.Models;

namespace FileSimulator.ControlApi.Services;

/// <summary>
/// Kafka administrative service for topic and consumer group management.
/// Uses Confluent.Kafka AdminClient for all operations.
/// </summary>
public class KafkaAdminService : IKafkaAdminService
{
    private readonly IAdminClient _adminClient;
    private readonly ILogger<KafkaAdminService> _logger;
    private readonly string _bootstrapServers;

    public KafkaAdminService(IOptions<KafkaOptions> options, ILogger<KafkaAdminService> logger)
    {
        _logger = logger;
        _bootstrapServers = options.Value.BootstrapServers;

        _adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = _bootstrapServers
        }).Build();

        _logger.LogInformation("KafkaAdminService initialized with bootstrap servers: {Servers}", _bootstrapServers);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TopicInfo>> GetTopicsAsync(CancellationToken ct)
    {
        var metadata = _adminClient.GetMetadata(TimeSpan.FromSeconds(10));
        var topics = metadata.Topics
            .Where(t => !t.Topic.StartsWith("__"))  // Exclude internal topics
            .Select(t => new TopicInfo(
                t.Topic,
                t.Partitions.Count,
                t.Partitions.FirstOrDefault()?.Replicas.Length ?? 1,
                0,  // Message count requires consumer query per partition
                null))
            .ToList();

        _logger.LogDebug("Retrieved {Count} topics", topics.Count);
        return Task.FromResult<IReadOnlyList<TopicInfo>>(topics);
    }

    /// <inheritdoc />
    public async Task<TopicInfo?> GetTopicAsync(string name, CancellationToken ct)
    {
        var topics = await GetTopicsAsync(ct);
        return topics.FirstOrDefault(t => t.Name == name);
    }

    /// <inheritdoc />
    public async Task CreateTopicAsync(CreateTopicRequest request, CancellationToken ct)
    {
        var spec = new TopicSpecification
        {
            Name = request.Name,
            NumPartitions = request.Partitions,
            ReplicationFactor = request.ReplicationFactor
        };

        await _adminClient.CreateTopicsAsync(new[] { spec });
        _logger.LogInformation("Created topic {Name} with {Partitions} partitions, replication factor {Rf}",
            request.Name, request.Partitions, request.ReplicationFactor);
    }

    /// <inheritdoc />
    public async Task DeleteTopicAsync(string name, CancellationToken ct)
    {
        await _adminClient.DeleteTopicsAsync(new[] { name });
        _logger.LogInformation("Deleted topic {Name}", name);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConsumerGroupInfo>> GetConsumerGroupsAsync(CancellationToken ct)
    {
        var groups = await _adminClient.ListConsumerGroupsAsync();
        var groupInfos = new List<ConsumerGroupInfo>();

        foreach (var group in groups.Valid)
        {
            try
            {
                var descriptions = await _adminClient.DescribeConsumerGroupsAsync(
                    new[] { group.GroupId });
                var desc = descriptions.ConsumerGroupDescriptions.FirstOrDefault();

                if (desc != null)
                {
                    // Total lag calculated in detail view to avoid expensive queries
                    groupInfos.Add(new ConsumerGroupInfo(
                        desc.GroupId,
                        desc.State.ToString(),
                        desc.Members.Count,
                        0));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to describe consumer group {GroupId}", group.GroupId);
            }
        }

        _logger.LogDebug("Retrieved {Count} consumer groups", groupInfos.Count);
        return groupInfos;
    }

    /// <inheritdoc />
    public async Task<ConsumerGroupDetail?> GetConsumerGroupDetailAsync(string groupId, CancellationToken ct)
    {
        var descriptions = await _adminClient.DescribeConsumerGroupsAsync(new[] { groupId });
        var desc = descriptions.ConsumerGroupDescriptions.FirstOrDefault();

        if (desc == null)
        {
            _logger.LogDebug("Consumer group {GroupId} not found", groupId);
            return null;
        }

        var members = desc.Members.Select(m => new ConsumerGroupMember(
            m.ConsumerId,
            m.ClientId,
            m.Host)).ToList();

        // Get committed offsets and calculate lag
        var partitions = new List<PartitionOffset>();
        try
        {
            var offsets = await _adminClient.ListConsumerGroupOffsetsAsync(
                new[] { new ConsumerGroupTopicPartitions(groupId, null) });

            foreach (var tpo in offsets.SelectMany(o => o.Partitions))
            {
                var watermark = GetWatermarkOffsets(tpo.Topic, tpo.Partition.Value);
                var currentOffset = tpo.Offset.Value;
                var lag = currentOffset >= 0 ? watermark.high - currentOffset : 0;

                partitions.Add(new PartitionOffset(
                    tpo.Topic,
                    tpo.Partition.Value,
                    currentOffset,
                    watermark.high,
                    Math.Max(0, lag)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get offsets for consumer group {GroupId}", groupId);
        }

        var totalLag = partitions.Sum(p => p.Lag);

        _logger.LogDebug("Retrieved detail for consumer group {GroupId}: {Members} members, {Lag} total lag",
            groupId, members.Count, totalLag);

        return new ConsumerGroupDetail(
            desc.GroupId,
            desc.State.ToString(),
            desc.Members.Count,
            totalLag,
            members,
            partitions);
    }

    /// <summary>
    /// Gets the low and high watermark offsets for a topic partition.
    /// </summary>
    private (long low, long high) GetWatermarkOffsets(string topic, int partition)
    {
        using var consumer = new ConsumerBuilder<Ignore, Ignore>(new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = $"watermark-query-{Guid.NewGuid()}"
        }).Build();

        var watermark = consumer.QueryWatermarkOffsets(
            new TopicPartition(topic, partition),
            TimeSpan.FromSeconds(5));

        return (watermark.Low.Value, watermark.High.Value);
    }

    /// <inheritdoc />
    public async Task ResetOffsetsAsync(ResetOffsetsRequest request, CancellationToken ct)
    {
        // First check if group is empty (required for offset reset)
        var detail = await GetConsumerGroupDetailAsync(request.GroupId, ct);
        if (detail == null)
            throw new InvalidOperationException($"Consumer group '{request.GroupId}' not found");

        if (detail.State != "Empty")
            throw new InvalidOperationException($"Consumer group must be inactive to reset offsets. Current state: {detail.State}");

        var targetOffset = request.ResetTo.ToLower() == "earliest"
            ? Offset.Beginning
            : Offset.End;

        // Get all partitions for the topic
        var metadata = _adminClient.GetMetadata(request.Topic, TimeSpan.FromSeconds(5));
        var topicMeta = metadata.Topics.FirstOrDefault(t => t.Topic == request.Topic);

        if (topicMeta == null)
            throw new InvalidOperationException($"Topic '{request.Topic}' not found");

        var offsets = topicMeta.Partitions.Select(p =>
            new TopicPartitionOffset(request.Topic, p.PartitionId, targetOffset)).ToList();

        await _adminClient.AlterConsumerGroupOffsetsAsync(
            new[] { new ConsumerGroupTopicPartitionOffsets(request.GroupId, offsets) });

        _logger.LogInformation("Reset offsets for consumer group {GroupId} on topic {Topic} to {Target}",
            request.GroupId, request.Topic, request.ResetTo);
    }

    /// <inheritdoc />
    public async Task DeleteConsumerGroupAsync(string groupId, CancellationToken ct)
    {
        await _adminClient.DeleteGroupsAsync(new[] { groupId });
        _logger.LogInformation("Deleted consumer group {GroupId}", groupId);
    }

    /// <inheritdoc />
    public Task<bool> HealthCheckAsync(CancellationToken ct)
    {
        try
        {
            var metadata = _adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            var healthy = metadata.Brokers.Count > 0;
            _logger.LogDebug("Kafka health check: {BrokerCount} brokers available", metadata.Brokers.Count);
            return Task.FromResult(healthy);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kafka health check failed");
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _adminClient?.Dispose();
    }
}
