using FluentValidation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using FileSimulator.ControlApi.Data;
using FileSimulator.ControlApi.Hubs;
using FileSimulator.ControlApi.Services;
using FileSimulator.ControlApi.Models;
using FileSimulator.ControlApi.Validators;
using Microsoft.Extensions.Options;

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
        options.Limits.MaxRequestBodySize = 104857600; // 100 MB for file uploads
    });

    // Add SignalR with JSON protocol
    builder.Services.AddSignalR();

    // Add health checks
    builder.Services.AddHealthChecks();

    // Add controllers for file operations
    builder.Services.AddControllers();

    // Add CORS for dashboard development
    // SignalR requires credentials mode which doesn't work with AllowAnyOrigin()
    // Must use specific origins with AllowCredentials()
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",
                    "http://localhost:5174",
                    "http://localhost:5175",
                    "http://localhost:5176",
                    "http://localhost:5177",
                    "http://localhost:5178",
                    "http://localhost:5179",
                    "http://localhost:3000",
                    "http://127.0.0.1:5173",
                    "http://127.0.0.1:5174",
                    "http://127.0.0.1:5175",
                    "http://127.0.0.1:5176",
                    "http://127.0.0.1:5177",
                    "http://127.0.0.1:5178",
                    "http://127.0.0.1:5179",
                    "http://127.0.0.1:3000"
                  )
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    // Configuration
    builder.Services.Configure<KubernetesOptions>(
        builder.Configuration.GetSection("Kubernetes"));
    builder.Services.Configure<KafkaOptions>(
        builder.Configuration.GetSection("Kafka"));

    // Register Kubernetes client for injection into management service
    builder.Services.AddSingleton<k8s.IKubernetes>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<KubernetesOptions>>().Value;
        var config = options.InCluster
            ? k8s.KubernetesClientConfiguration.InClusterConfig()
            : k8s.KubernetesClientConfiguration.BuildConfigFromConfigFile();
        return new k8s.Kubernetes(config);
    });

    // EF Core SQLite for metrics persistence
    // Use IDbContextFactory for background service compatibility
    var dbPath = Environment.GetEnvironmentVariable("CONTROL_DATA_PATH")
        ?? (OperatingSystem.IsWindows() ? @"C:\simulator-data\control-data" : "/mnt/control-data");
    Directory.CreateDirectory(dbPath);
    var connectionString = $"Data Source={Path.Combine(dbPath, "metrics.db")}";
    builder.Services.AddDbContextFactory<MetricsDbContext>(options =>
        options.UseSqlite(connectionString));

    // Services
    builder.Services.AddScoped<IMetricsService, MetricsService>();
    builder.Services.AddSingleton<IKubernetesDiscoveryService, KubernetesDiscoveryService>();
    // ConfigMap update service must be registered before management service (dependency)
    builder.Services.AddSingleton<IConfigMapUpdateService, ConfigMapUpdateService>();
    builder.Services.AddSingleton<IKubernetesManagementService, KubernetesManagementService>();
    builder.Services.AddSingleton<IConfigurationExportService, ConfigurationExportService>();
    builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();

    // FluentValidation validators for server creation requests
    builder.Services.AddValidatorsFromAssemblyContaining<CreateFtpServerValidator>();
    builder.Services.AddSingleton<ServerStatusBroadcaster>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ServerStatusBroadcaster>());
    builder.Services.AddSingleton<FileWatcherService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<FileWatcherService>());

    // Metrics background services
    builder.Services.AddHostedService<RollupGenerationService>();
    builder.Services.AddHostedService<RetentionCleanupService>();

    // Kafka services
    builder.Services.AddSingleton<IKafkaAdminService, KafkaAdminService>();
    builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
    builder.Services.AddSingleton<IKafkaConsumerService, KafkaConsumerService>();

    var app = builder.Build();

    // Ensure SQLite database exists with schema
    using (var scope = app.Services.CreateScope())
    {
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MetricsDbContext>>();
        using var context = factory.CreateDbContext();
        context.Database.EnsureCreated();
        Log.Information("Metrics database initialized at {DbPath}", connectionString);
    }

    // Middleware pipeline
    app.UseSerilogRequestLogging();
    app.UseCors();

    // Map health check endpoint
    app.MapHealthChecks("/health");

    // Map SignalR hubs
    app.MapHub<ServerStatusHub>("/hubs/status");
    app.MapHub<FileEventsHub>("/hubs/fileevents");
    app.MapHub<MetricsHub>("/hubs/metrics");
    app.MapHub<KafkaHub>("/hubs/kafka");

    // Map REST API controllers
    app.MapControllers();

    // API root endpoint
    app.MapGet("/", () => new
    {
        name = "File Simulator Control API",
        version = "1.0.0",
        endpoints = new[]
        {
            "/health",
            "/hubs/status",
            "/hubs/fileevents",
            "/hubs/metrics",
            "/hubs/kafka",
            "/api/version",
            "/api/servers",
            "/api/status",
            "/api/servers/{name}",
            "/api/files",
            "/api/files/tree",
            "/api/files/upload",
            "/api/files/download",
            "/api/metrics/samples",
            "/api/metrics/hourly",
            "/api/metrics/servers",
            "/api/kafka/topics",
            "/api/kafka/consumer-groups",
            "/api/kafka/health",
            "/api/configuration/export",
            "/api/configuration/preview",
            "/api/configuration/import",
            "/api/configuration/import/file",
            "/api/configuration/validate",
            "/api/configuration/templates"
        }
    });

    // Version endpoint
    app.MapGet("/api/version", () => new
    {
        api = "1.0.0",
        runtime = Environment.Version.ToString(),
        framework = "ASP.NET Core 9.0"
    });

    // API: List all discovered servers
    app.MapGet("/api/servers", async (IKubernetesDiscoveryService discovery, CancellationToken ct) =>
    {
        var servers = await discovery.DiscoverServersAsync(ct);
        return Results.Ok(servers);
    })
    .WithName("GetServers");

    // API: Get current status (from broadcaster cache)
    app.MapGet("/api/status", (ServerStatusBroadcaster broadcaster) =>
    {
        var status = broadcaster.GetLatestStatus();
        return status != null
            ? Results.Ok(status)
            : Results.NotFound("Status not yet available");
    })
    .WithName("GetStatus");

    // API: Get specific server
    app.MapGet("/api/servers/{name}", async (
        string name,
        IKubernetesDiscoveryService discovery,
        CancellationToken ct) =>
    {
        var server = await discovery.GetServerAsync(name, ct);
        return server != null
            ? Results.Ok(server)
            : Results.NotFound($"Server '{name}' not found");
    })
    .WithName("GetServer");

    // Graceful shutdown handling for Kubernetes
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(() =>
    {
        Log.Information("Application is shutting down...");
    });

    Log.Information("File Simulator Control API started successfully");
    Log.Information("SignalR hub available at /hubs/status");
    Log.Information("FileEvents hub available at /hubs/fileevents");
    Log.Information("Metrics hub available at /hubs/metrics");
    Log.Information("Kafka hub available at /hubs/kafka");
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
