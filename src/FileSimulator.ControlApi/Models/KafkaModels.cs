namespace FileSimulator.ControlApi.Models;

/// <summary>
/// Kafka configuration options from appsettings.
/// </summary>
public class KafkaOptions
{
    /// <summary>
    /// Kafka bootstrap servers connection string.
    /// </summary>
    public string BootstrapServers { get; set; } = "kafka:9092";
}

/// <summary>
/// Topic information retrieved from Kafka.
/// </summary>
public record TopicInfo(
    string Name,
    int PartitionCount,
    int ReplicationFactor,
    long MessageCount,
    DateTime? LastActivity);

/// <summary>
/// Request to create a new topic.
/// </summary>
public record CreateTopicRequest(
    string Name,
    int Partitions = 1,
    short ReplicationFactor = 1);

/// <summary>
/// Consumer group summary information.
/// </summary>
public record ConsumerGroupInfo(
    string GroupId,
    string State,
    int MemberCount,
    long TotalLag);

/// <summary>
/// Detailed consumer group information with members and partitions.
/// </summary>
public record ConsumerGroupDetail(
    string GroupId,
    string State,
    int MemberCount,
    long TotalLag,
    IReadOnlyList<ConsumerGroupMember> Members,
    IReadOnlyList<PartitionOffset> Partitions);

/// <summary>
/// A member of a consumer group.
/// </summary>
public record ConsumerGroupMember(
    string MemberId,
    string ClientId,
    string Host);

/// <summary>
/// Partition offset tracking for consumer lag calculation.
/// </summary>
public record PartitionOffset(
    string Topic,
    int Partition,
    long CurrentOffset,
    long HighWatermark,
    long Lag);

/// <summary>
/// Request to produce a message to a topic.
/// </summary>
public record ProduceMessageRequest(
    string Topic,
    string? Key,
    string Value);

/// <summary>
/// Result of a produced message.
/// </summary>
public record ProduceMessageResult(
    string Topic,
    int Partition,
    long Offset,
    DateTime Timestamp);

/// <summary>
/// Kafka message for display in the UI.
/// </summary>
public record KafkaMessage(
    string Topic,
    int Partition,
    long Offset,
    string? Key,
    string Value,
    DateTime Timestamp);

/// <summary>
/// Request to reset consumer group offsets.
/// </summary>
public record ResetOffsetsRequest(
    string GroupId,
    string Topic,
    string ResetTo);  // "earliest" or "latest"
