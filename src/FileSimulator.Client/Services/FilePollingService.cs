using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace FileSimulator.Client.Services;

#region Polling Configuration

/// <summary>
/// Configuration for a file polling endpoint
/// </summary>
public class FilePollingEndpoint
{
    public required string Name { get; set; }
    public required string Protocol { get; set; }  // "ftp", "sftp", "s3", "http", "smb", "nfs"
    public required string Path { get; set; }
    public string? FilePattern { get; set; } = "*";
    public string CronSchedule { get; set; } = "0/30 * * * * ?";  // Every 30 seconds
    public bool DeleteAfterProcess { get; set; } = false;
    public bool MoveAfterProcess { get; set; } = true;
    public string? ProcessedPath { get; set; }  // Where to move processed files
}

public class FilePollingOptions
{
    public List<FilePollingEndpoint> Endpoints { get; set; } = new();
    public string TempDirectory { get; set; } = Path.GetTempPath();
}

#endregion

#region File Discovered Event

/// <summary>
/// Event raised when a new file is discovered
/// </summary>
public record FileDiscoveredEvent
{
    public required string EndpointName { get; init; }
    public required string Protocol { get; init; }
    public required RemoteFileInfo FileInfo { get; init; }
    public DateTime DiscoveredAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Interface for handling discovered files
/// </summary>
public interface IFileDiscoveryHandler
{
    Task HandleFileDiscoveredAsync(FileDiscoveredEvent evt, CancellationToken ct = default);
}

#endregion

#region File Polling Service

/// <summary>
/// Service that polls multiple file endpoints and raises events for discovered files
/// </summary>
public class FilePollingService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly FilePollingOptions _options;
    private readonly ILogger<FilePollingService> _logger;
    private readonly ConcurrentDictionary<string, HashSet<string>> _processedFiles = new();

    public FilePollingService(
        IServiceProvider serviceProvider,
        IOptions<FilePollingOptions> options,
        ILogger<FilePollingService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Poll a specific endpoint for new files
    /// </summary>
    public async Task<IEnumerable<FileDiscoveredEvent>> PollEndpointAsync(
        string endpointName, 
        CancellationToken ct = default)
    {
        var endpoint = _options.Endpoints.FirstOrDefault(e => e.Name == endpointName)
            ?? throw new ArgumentException($"Unknown endpoint: {endpointName}");

        return await PollEndpointAsync(endpoint, ct);
    }

    /// <summary>
    /// Poll an endpoint for new files
    /// </summary>
    public async Task<IEnumerable<FileDiscoveredEvent>> PollEndpointAsync(
        FilePollingEndpoint endpoint,
        CancellationToken ct = default)
    {
        var service = GetProtocolService(endpoint.Protocol);
        var discoveredEvents = new List<FileDiscoveredEvent>();

        try
        {
            var files = await service.DiscoverFilesAsync(endpoint.Path, endpoint.FilePattern, ct);
            var processedSet = _processedFiles.GetOrAdd(endpoint.Name, _ => new HashSet<string>());

            foreach (var file in files)
            {
                // Skip already processed files (in-memory tracking)
                if (processedSet.Contains(file.FullPath))
                    continue;

                var evt = new FileDiscoveredEvent
                {
                    EndpointName = endpoint.Name,
                    Protocol = endpoint.Protocol,
                    FileInfo = file
                };

                discoveredEvents.Add(evt);
                
                _logger.LogInformation(
                    "Discovered new file: {FileName} at {Path} via {Protocol}",
                    file.Name, file.FullPath, endpoint.Protocol);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to poll endpoint {Name}", endpoint.Name);
        }

        return discoveredEvents;
    }

    /// <summary>
    /// Mark a file as processed (prevents re-processing)
    /// </summary>
    public void MarkAsProcessed(string endpointName, string filePath)
    {
        var processedSet = _processedFiles.GetOrAdd(endpointName, _ => new HashSet<string>());
        processedSet.Add(filePath);
    }

    /// <summary>
    /// Download a discovered file to local temp directory
    /// </summary>
    public async Task<string> DownloadFileAsync(
        FileDiscoveredEvent evt,
        CancellationToken ct = default)
    {
        var service = GetProtocolService(evt.Protocol);
        var localPath = Path.Combine(_options.TempDirectory, $"{Guid.NewGuid()}_{evt.FileInfo.Name}");

        await service.DownloadFileAsync(evt.FileInfo.FullPath, localPath, ct);

        return localPath;
    }

    /// <summary>
    /// Process file after handling (move or delete based on config)
    /// </summary>
    public async Task PostProcessFileAsync(
        FileDiscoveredEvent evt,
        CancellationToken ct = default)
    {
        var endpoint = _options.Endpoints.First(e => e.Name == evt.EndpointName);
        var service = GetProtocolService(evt.Protocol);

        if (endpoint.DeleteAfterProcess)
        {
            await service.DeleteFileAsync(evt.FileInfo.FullPath, ct);
            _logger.LogInformation("Deleted processed file: {Path}", evt.FileInfo.FullPath);
        }
        else if (endpoint.MoveAfterProcess && !string.IsNullOrEmpty(endpoint.ProcessedPath))
        {
            // Read, write to new location, delete original
            var content = await service.ReadFileAsync(evt.FileInfo.FullPath, ct);
            var newPath = $"{endpoint.ProcessedPath.TrimEnd('/')}/{evt.FileInfo.Name}";
            await service.WriteFileAsync(newPath, content, ct);
            await service.DeleteFileAsync(evt.FileInfo.FullPath, ct);
            
            _logger.LogInformation("Moved processed file from {From} to {To}", evt.FileInfo.FullPath, newPath);
        }

        MarkAsProcessed(evt.EndpointName, evt.FileInfo.FullPath);
    }

    private IFileProtocolService GetProtocolService(string protocol)
    {
        return protocol.ToLowerInvariant() switch
        {
            "ftp" => _serviceProvider.GetRequiredService<FtpFileService>(),
            "sftp" => _serviceProvider.GetRequiredService<SftpFileService>(),
            "s3" => _serviceProvider.GetRequiredService<S3FileService>(),
            "http" => _serviceProvider.GetRequiredService<HttpFileService>(),
            "smb" => _serviceProvider.GetRequiredService<SmbFileService>(),
            "nfs" => _serviceProvider.GetRequiredService<NfsFileService>(),
            _ => throw new ArgumentException($"Unknown protocol: {protocol}")
        };
    }
}

#endregion

#region Quartz Job for Scheduled Polling

/// <summary>
/// Quartz job that polls a specific endpoint on a schedule
/// </summary>
[DisallowConcurrentExecution]
public class FilePollingJob : IJob
{
    private readonly FilePollingService _pollingService;
    private readonly IEnumerable<IFileDiscoveryHandler> _handlers;
    private readonly ILogger<FilePollingJob> _logger;

    public FilePollingJob(
        FilePollingService pollingService,
        IEnumerable<IFileDiscoveryHandler> handlers,
        ILogger<FilePollingJob> logger)
    {
        _pollingService = pollingService;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var endpointName = context.MergedJobDataMap.GetString("EndpointName");
        if (string.IsNullOrEmpty(endpointName))
        {
            _logger.LogError("FilePollingJob executed without EndpointName");
            return;
        }

        _logger.LogDebug("Polling endpoint: {EndpointName}", endpointName);

        try
        {
            var events = await _pollingService.PollEndpointAsync(endpointName, context.CancellationToken);

            foreach (var evt in events)
            {
                foreach (var handler in _handlers)
                {
                    try
                    {
                        await handler.HandleFileDiscoveredAsync(evt, context.CancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Handler {Handler} failed for file {File}",
                            handler.GetType().Name, evt.FileInfo.Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FilePollingJob failed for endpoint {EndpointName}", endpointName);
        }
    }
}

#endregion

#region Service Registration Extensions

public static class FilePollingExtensions
{
    /// <summary>
    /// Register all file protocol services
    /// </summary>
    public static IServiceCollection AddFileProtocolServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register protocol services
        services.Configure<FtpServerOptions>(configuration.GetSection("FileSimulator:Ftp"));
        services.Configure<SftpServerOptions>(configuration.GetSection("FileSimulator:Sftp"));
        services.Configure<S3ServerOptions>(configuration.GetSection("FileSimulator:S3"));
        services.Configure<HttpServerOptions>(configuration.GetSection("FileSimulator:Http"));
        services.Configure<SmbServerOptions>(configuration.GetSection("FileSimulator:Smb"));
        services.Configure<NfsServerOptions>(configuration.GetSection("FileSimulator:Nfs"));

        services.AddSingleton<FtpFileService>();
        services.AddSingleton<SftpFileService>();
        services.AddSingleton<S3FileService>();
        services.AddSingleton<HttpFileService>();
        services.AddSingleton<SmbFileService>();
        services.AddSingleton<NfsFileService>();

        // Register polling service
        services.Configure<FilePollingOptions>(configuration.GetSection("FileSimulator:Polling"));
        services.AddSingleton<FilePollingService>();

        return services;
    }

    /// <summary>
    /// Configure Quartz jobs for file polling
    /// </summary>
    public static IServiceCollectionQuartzConfigurator AddFilePollingJobs(
        this IServiceCollectionQuartzConfigurator quartz,
        FilePollingOptions options)
    {
        foreach (var endpoint in options.Endpoints)
        {
            var jobKey = new JobKey($"FilePolling_{endpoint.Name}");
            
            quartz.AddJob<FilePollingJob>(opts => opts
                .WithIdentity(jobKey)
                .UsingJobData("EndpointName", endpoint.Name)
                .StoreDurably());

            quartz.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"FilePolling_{endpoint.Name}_Trigger")
                .WithCronSchedule(endpoint.CronSchedule));
        }

        return quartz;
    }
}

#endregion
