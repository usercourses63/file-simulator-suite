using FileSimulator.ControlApi.Models;

namespace FileSimulator.ControlApi.Services;

/// <summary>
/// Service for Kafka administrative operations (topics, consumer groups).
/// </summary>
public interface IKafkaAdminService : IDisposable
{
    /// <summary>
    /// Gets all topics (excluding internal __ prefixed topics).
    /// </summary>
    Task<IReadOnlyList<TopicInfo>> GetTopicsAsync(CancellationToken ct);

    /// <summary>
    /// Gets a specific topic by name.
    /// </summary>
    Task<TopicInfo?> GetTopicAsync(string name, CancellationToken ct);

    /// <summary>
    /// Creates a new topic.
    /// </summary>
    Task CreateTopicAsync(CreateTopicRequest request, CancellationToken ct);

    /// <summary>
    /// Deletes a topic.
    /// </summary>
    Task DeleteTopicAsync(string name, CancellationToken ct);

    /// <summary>
    /// Gets all consumer groups.
    /// </summary>
    Task<IReadOnlyList<ConsumerGroupInfo>> GetConsumerGroupsAsync(CancellationToken ct);

    /// <summary>
    /// Gets detailed information about a specific consumer group.
    /// </summary>
    Task<ConsumerGroupDetail?> GetConsumerGroupDetailAsync(string groupId, CancellationToken ct);

    /// <summary>
    /// Resets consumer group offsets to earliest or latest.
    /// Group must be inactive (empty state).
    /// </summary>
    Task ResetOffsetsAsync(ResetOffsetsRequest request, CancellationToken ct);

    /// <summary>
    /// Deletes a consumer group.
    /// </summary>
    Task DeleteConsumerGroupAsync(string groupId, CancellationToken ct);

    /// <summary>
    /// Checks connectivity to Kafka broker.
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken ct);
}
