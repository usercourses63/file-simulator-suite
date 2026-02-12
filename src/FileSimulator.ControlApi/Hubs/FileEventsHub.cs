namespace FileSimulator.ControlApi.Hubs;

using Microsoft.AspNetCore.SignalR;
using FileSimulator.ControlApi.Models;
using FileSimulator.ControlApi.Services;

/// <summary>
/// SignalR hub for real-time file system event updates.
/// Clients connect to receive file creation, modification, deletion, and rename events.
/// Sends recent event history on connect so clients don't start with an empty feed.
/// </summary>
public class FileEventsHub : Hub
{
    private readonly ILogger<FileEventsHub> _logger;
    private readonly FileWatcherService _fileWatcher;

    public FileEventsHub(ILogger<FileEventsHub> logger, FileWatcherService fileWatcher)
    {
        _logger = logger;
        _fileWatcher = fileWatcher;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Client connected to file events hub: {ConnectionId}",
            Context.ConnectionId);

        // Send recent event history to the newly connected client
        var recentEvents = _fileWatcher.GetRecentEvents(50);
        foreach (var evt in recentEvents)
        {
            await Clients.Caller.SendAsync("FileEvent", evt);
        }

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
