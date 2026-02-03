using FileSimulator.ControlApi.Models;

namespace FileSimulator.ControlApi.Services;

/// <summary>
/// Service for consuming messages from Kafka topics.
/// Supports both batch retrieval and real-time streaming.
/// </summary>
public interface IKafkaConsumerService
{
    /// <summary>
    /// Gets the most recent N messages from a topic (for initial load).
    /// Messages are returned in reverse chronological order.
    /// </summary>
    /// <param name="topic">Topic name to read from</param>
    /// <param name="count">Maximum number of messages to retrieve</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of recent messages</returns>
    Task<IReadOnlyList<KafkaMessage>> GetRecentMessagesAsync(string topic, int count, CancellationToken ct);

    /// <summary>
    /// Streams new messages from a topic in real-time (for live feed).
    /// Starts from the latest offset and yields messages as they arrive.
    /// </summary>
    /// <param name="topic">Topic name to stream from</param>
    /// <param name="ct">Cancellation token to stop streaming</param>
    /// <returns>Async enumerable of messages</returns>
    IAsyncEnumerable<KafkaMessage> StreamMessagesAsync(string topic, CancellationToken ct);
}
