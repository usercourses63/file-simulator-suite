namespace FileSimulator.ControlApi.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>
/// SignalR hub for streaming real-time metrics samples to dashboard.
/// Broadcasts new samples as they are recorded after each health check cycle.
/// </summary>
public class MetricsHub : Hub
{
    private readonly ILogger<MetricsHub> _logger;

    public MetricsHub(ILogger<MetricsHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Metrics client connected: {ConnectionId}",
            Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception,
                "Metrics client disconnected with error: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation(
                "Metrics client disconnected: {ConnectionId}",
                Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
