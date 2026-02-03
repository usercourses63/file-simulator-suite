using Microsoft.AspNetCore.SignalR;
using FileSimulator.ControlApi.Models;
using FileSimulator.ControlApi.Services;

namespace FileSimulator.ControlApi.Hubs;

/// <summary>
/// SignalR hub for real-time Kafka message streaming.
/// Clients can subscribe to topics and receive messages as they arrive.
/// </summary>
public class KafkaHub : Hub
{
    private readonly IKafkaConsumerService _consumerService;
    private readonly ILogger<KafkaHub> _logger;

    public KafkaHub(IKafkaConsumerService consumerService, ILogger<KafkaHub> logger)
    {
        _consumerService = consumerService;
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to receive messages from a topic.
    /// Messages will be pushed via "KafkaMessage" event.
    /// </summary>
    /// <param name="topic">Topic name to subscribe to</param>
    public async Task SubscribeToTopic(string topic)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"topic:{topic}");
        _logger.LogInformation("Client {ConnectionId} subscribed to topic {Topic}",
            Context.ConnectionId, topic);

        // Start streaming in background
        _ = StreamMessagesAsync(topic, Context.ConnectionAborted);
    }

    /// <summary>
    /// Unsubscribe from a topic.
    /// </summary>
    /// <param name="topic">Topic name to unsubscribe from</param>
    public async Task UnsubscribeFromTopic(string topic)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"topic:{topic}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from topic {Topic}",
            Context.ConnectionId, topic);
    }

    /// <summary>
    /// Background task to stream messages from Kafka to SignalR group.
    /// </summary>
    private async Task StreamMessagesAsync(string topic, CancellationToken ct)
    {
        try
        {
            await foreach (var message in _consumerService.StreamMessagesAsync(topic, ct))
            {
                await Clients.Group($"topic:{topic}").SendAsync("KafkaMessage", message, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected, normal termination
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming messages from topic {Topic}", topic);
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to KafkaHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected from KafkaHub with error: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected from KafkaHub: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
