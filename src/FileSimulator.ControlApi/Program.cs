using Microsoft.AspNetCore.SignalR;
using Serilog;
using Serilog.Events;

// Configure Serilog before creating the builder
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting File Simulator Control API...");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();

    // Configure Kestrel endpoints from appsettings
    builder.WebHost.UseKestrel(options =>
    {
        options.AddServerHeader = false; // Security: don't expose server version
    });

    // Add SignalR with JSON protocol
    builder.Services.AddSignalR();

    // Add health checks
    builder.Services.AddHealthChecks();

    // Add CORS for dashboard development
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    // Middleware pipeline
    app.UseSerilogRequestLogging();
    app.UseCors();

    // Map health check endpoint
    app.MapHealthChecks("/health");

    // Map SignalR hub
    app.MapHub<ServerStatusHub>("/hubs/status");

    // API root endpoint
    app.MapGet("/", () => new
    {
        name = "File Simulator Control API",
        version = "1.0.0",
        endpoints = new[]
        {
            "/health",
            "/hubs/status",
            "/api/version"
        }
    });

    // Version endpoint
    app.MapGet("/api/version", () => new
    {
        api = "1.0.0",
        runtime = Environment.Version.ToString(),
        framework = "ASP.NET Core 9.0"
    });

    // Graceful shutdown handling for Kubernetes
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(() =>
    {
        Log.Information("Application is shutting down...");
    });

    Log.Information("File Simulator Control API started successfully");
    Log.Information("SignalR hub available at /hubs/status");
    Log.Information("Health check available at /health");

    app.Run();

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// SignalR hub for broadcasting server status updates to connected clients.
/// Phase 7 dashboard will connect to this hub for real-time status monitoring.
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
        await base.OnConnectedAsync();
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
