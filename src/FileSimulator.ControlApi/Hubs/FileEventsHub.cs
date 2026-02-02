namespace FileSimulator.ControlApi.Hubs;

using Microsoft.AspNetCore.SignalR;
using FileSimulator.ControlApi.Models;

/// <summary>
/// SignalR hub for real-time file system event updates.
/// Clients connect to receive file creation, modification, deletion, and rename events.
/// This is a push-only hub - no client-callable methods needed.
/// </summary>
public class FileEventsHub : Hub
{
    private readonly ILogger<FileEventsHub> _logger;

    public FileEventsHub(ILogger<FileEventsHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Client connected to file events hub: {ConnectionId}",
            Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception,
                "Client disconnected from file events hub with error: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation(
                "Client disconnected from file events hub: {ConnectionId}",
                Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
