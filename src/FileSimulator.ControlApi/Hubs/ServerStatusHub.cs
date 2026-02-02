namespace FileSimulator.ControlApi.Hubs;

using Microsoft.AspNetCore.SignalR;
using FileSimulator.ControlApi.Models;

/// <summary>
/// SignalR hub for real-time server status updates.
/// Clients connect to receive periodic status broadcasts.
/// </summary>
public class ServerStatusHub : Hub
{
    private readonly ILogger<ServerStatusHub> _logger;

    public ServerStatusHub(ILogger<ServerStatusHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Client connected: {ConnectionId}",
            Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception,
                "Client disconnected with error: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation(
                "Client disconnected: {ConnectionId}",
                Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client can request immediate status update.
    /// </summary>
    public async Task RequestStatus()
    {
        _logger.LogDebug(
            "Status request from: {ConnectionId}",
            Context.ConnectionId);

        // Broadcaster will handle this by sending to just this client
        await Clients.Caller.SendAsync("StatusRequested");
    }
}
