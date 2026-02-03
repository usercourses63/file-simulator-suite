using FileSimulator.ControlApi.Models;

namespace FileSimulator.ControlApi.Services;

/// <summary>
/// Service for producing messages to Kafka topics.
/// </summary>
public interface IKafkaProducerService : IDisposable
{
    /// <summary>
    /// Produces a message to a Kafka topic.
    /// </summary>
    /// <param name="request">Message to produce (topic, optional key, value)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result with partition and offset of produced message</returns>
    Task<ProduceMessageResult> ProduceAsync(ProduceMessageRequest request, CancellationToken ct);
}
