// ============================================================================
// COMPLETE EXAMPLE: File Processing Microservice
// Shows discovery, reading, and writing for ALL protocols
// ============================================================================

using FileSimulator.Client.Services;
using MassTransit;
using Microsoft.Extensions.Options;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. REGISTER ALL FILE PROTOCOL SERVICES
// ============================================================================

builder.Services.AddFileProtocolServices(builder.Configuration);

// Register your file discovery handlers
builder.Services.AddScoped<IFileDiscoveryHandler, ExampleFileDiscoveryHandler>();

// ============================================================================
// 2. CONFIGURE QUARTZ FOR SCHEDULED POLLING
// ============================================================================

var pollingOptions = builder.Configuration
    .GetSection("FileSimulator:Polling")
    .Get<FilePollingOptions>() ?? new FilePollingOptions();

builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();
    q.AddFilePollingJobs(pollingOptions);
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// ============================================================================
// 3. CONFIGURE MASSTRANSIT (Optional - for distributed processing)
// ============================================================================

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProcessFileCommandConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost");
        cfg.ConfigureEndpoints(context);
    });
});

// ============================================================================
// 4. HEALTH CHECKS
// ============================================================================

builder.Services.AddHealthChecks()
    .AddCheck<FileServicesHealthCheck>("file-services");

var app = builder.Build();

// ============================================================================
// 5. API ENDPOINTS FOR MANUAL FILE OPERATIONS
// ============================================================================

var fileApi = app.MapGroup("/api/files");

// --- FTP Operations ---
fileApi.MapGet("/ftp/list", async (FtpFileService ftp, string path = "/") =>
{
    var files = await ftp.DiscoverFilesAsync(path);
    return Results.Ok(files);
});

fileApi.MapGet("/ftp/read", async (FtpFileService ftp, string path) =>
{
    var content = await ftp.ReadFileAsync(path);
    return Results.File(content, "application/octet-stream", Path.GetFileName(path));
});

fileApi.MapPost("/ftp/write", async (FtpFileService ftp, string path, HttpRequest request) =>
{
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms);
    await ftp.WriteFileAsync(path, ms.ToArray());
    return Results.Ok(new { Message = "File written", Path = path });
});

// --- SFTP Operations ---
fileApi.MapGet("/sftp/list", async (SftpFileService sftp, string path = "/") =>
{
    var files = await sftp.DiscoverFilesAsync(path);
    return Results.Ok(files);
});

fileApi.MapGet("/sftp/read", async (SftpFileService sftp, string path) =>
{
    var content = await sftp.ReadFileAsync(path);
    return Results.File(content, "application/octet-stream", Path.GetFileName(path));
});

fileApi.MapPost("/sftp/write", async (SftpFileService sftp, string path, HttpRequest request) =>
{
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms);
    await sftp.WriteFileAsync(path, ms.ToArray());
    return Results.Ok(new { Message = "File written", Path = path });
});

// --- S3 Operations ---
fileApi.MapGet("/s3/list", async (S3FileService s3, string bucket, string? prefix = null) =>
{
    var path = string.IsNullOrEmpty(prefix) ? bucket : $"{bucket}/{prefix}";
    var files = await s3.DiscoverFilesAsync(path);
    return Results.Ok(files);
});

fileApi.MapGet("/s3/read", async (S3FileService s3, string bucket, string key) =>
{
    var content = await s3.ReadFileAsync($"{bucket}/{key}");
    return Results.File(content, "application/octet-stream", Path.GetFileName(key));
});

fileApi.MapPost("/s3/write", async (S3FileService s3, string bucket, string key, HttpRequest request) =>
{
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms);
    await s3.WriteFileAsync($"{bucket}/{key}", ms.ToArray());
    return Results.Ok(new { Message = "File written", Bucket = bucket, Key = key });
});

// --- HTTP Operations ---
fileApi.MapGet("/http/list", async (HttpFileService http, string path = "/") =>
{
    var files = await http.DiscoverFilesAsync(path);
    return Results.Ok(files);
});

fileApi.MapGet("/http/read", async (HttpFileService http, string path) =>
{
    var content = await http.ReadFileAsync(path);
    return Results.File(content, "application/octet-stream", Path.GetFileName(path));
});

fileApi.MapPost("/http/write", async (HttpFileService http, string path, HttpRequest request) =>
{
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms);
    await http.WriteFileAsync(path, ms.ToArray());
    return Results.Ok(new { Message = "File written", Path = path });
});

// --- SMB Operations ---
fileApi.MapGet("/smb/list", async (SmbFileService smb, string path = "/") =>
{
    var files = await smb.DiscoverFilesAsync(path);
    return Results.Ok(files);
});

fileApi.MapGet("/smb/read", async (SmbFileService smb, string path) =>
{
    var content = await smb.ReadFileAsync(path);
    return Results.File(content, "application/octet-stream", Path.GetFileName(path));
});

fileApi.MapPost("/smb/write", async (SmbFileService smb, string path, HttpRequest request) =>
{
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms);
    await smb.WriteFileAsync(path, ms.ToArray());
    return Results.Ok(new { Message = "File written", Path = path });
});

// --- NFS Operations ---
fileApi.MapGet("/nfs/list", async (NfsFileService nfs, string path = "/") =>
{
    var files = await nfs.DiscoverFilesAsync(path);
    return Results.Ok(files);
});

fileApi.MapGet("/nfs/read", async (NfsFileService nfs, string path) =>
{
    var content = await nfs.ReadFileAsync(path);
    return Results.File(content, "application/octet-stream", Path.GetFileName(path));
});

fileApi.MapPost("/nfs/write", async (NfsFileService nfs, string path, HttpRequest request) =>
{
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms);
    await nfs.WriteFileAsync(path, ms.ToArray());
    return Results.Ok(new { Message = "File written", Path = path });
});

// --- Health & Status ---
app.MapHealthChecks("/health");

app.MapGet("/status", async (
    FtpFileService ftp,
    SftpFileService sftp,
    S3FileService s3,
    HttpFileService http,
    SmbFileService smb,
    NfsFileService nfs) =>
{
    return Results.Ok(new
    {
        FTP = await ftp.HealthCheckAsync(),
        SFTP = await sftp.HealthCheckAsync(),
        S3 = await s3.HealthCheckAsync(),
        HTTP = await http.HealthCheckAsync(),
        SMB = await smb.HealthCheckAsync(),
        NFS = await nfs.HealthCheckAsync()
    });
});

app.Run();

// ============================================================================
// EXAMPLE FILE DISCOVERY HANDLER
// ============================================================================

/// <summary>
/// Example handler that processes discovered files
/// </summary>
public class ExampleFileDiscoveryHandler : IFileDiscoveryHandler
{
    private readonly FilePollingService _pollingService;
    private readonly S3FileService _s3;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ExampleFileDiscoveryHandler> _logger;

    public ExampleFileDiscoveryHandler(
        FilePollingService pollingService,
        S3FileService s3,
        IPublishEndpoint publishEndpoint,
        ILogger<ExampleFileDiscoveryHandler> logger)
    {
        _pollingService = pollingService;
        _s3 = s3;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task HandleFileDiscoveredAsync(FileDiscoveredEvent evt, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Processing discovered file: {FileName} from {Protocol}",
            evt.FileInfo.Name, evt.Protocol);

        try
        {
            // 1. Download the file locally
            var localPath = await _pollingService.DownloadFileAsync(evt, ct);

            try
            {
                // 2. Process the file (your business logic here)
                // Example: Parse CSV, validate XML, transform data, etc.
                var processedData = await ProcessFileAsync(localPath, ct);

                // 3. Write results to output location (S3 in this example)
                var outputKey = $"processed/{DateTime.UtcNow:yyyy/MM/dd}/{evt.FileInfo.Name}";
                await _s3.WriteFileAsync($"output/{outputKey}", processedData, ct);

                // 4. Publish event for other services
                await _publishEndpoint.Publish(new FileProcessedNotification
                {
                    OriginalFile = evt.FileInfo.FullPath,
                    ProcessedFile = $"output/{outputKey}",
                    ProcessedAt = DateTime.UtcNow
                }, ct);

                // 5. Mark as processed / move / delete
                await _pollingService.PostProcessFileAsync(evt, ct);

                _logger.LogInformation("Successfully processed file: {FileName}", evt.FileInfo.Name);
            }
            finally
            {
                // Clean up local file
                if (File.Exists(localPath))
                    File.Delete(localPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file: {FileName}", evt.FileInfo.Name);
            throw;
        }
    }

    private async Task<byte[]> ProcessFileAsync(string localPath, CancellationToken ct)
    {
        // Example: Read file, transform, return processed bytes
        var content = await File.ReadAllBytesAsync(localPath, ct);
        
        // Your transformation logic here...
        // For example: parse CSV, validate, enrich, etc.
        
        return content;
    }
}

/// <summary>
/// Notification event when a file is processed
/// </summary>
public record FileProcessedNotification
{
    public required string OriginalFile { get; init; }
    public required string ProcessedFile { get; init; }
    public DateTime ProcessedAt { get; init; }
}

// ============================================================================
// MASSTRANSIT CONSUMER FOR DISTRIBUTED FILE PROCESSING
// ============================================================================

public record ProcessFileCommand
{
    public required string Protocol { get; init; }
    public required string SourcePath { get; init; }
    public required string DestinationBucket { get; init; }
    public string? DestinationKey { get; init; }
}

public class ProcessFileCommandConsumer : IConsumer<ProcessFileCommand>
{
    private readonly IServiceProvider _services;
    private readonly S3FileService _s3Output;
    private readonly ILogger<ProcessFileCommandConsumer> _logger;

    public ProcessFileCommandConsumer(
        IServiceProvider services,
        S3FileService s3Output,
        ILogger<ProcessFileCommandConsumer> logger)
    {
        _services = services;
        _s3Output = s3Output;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessFileCommand> context)
    {
        var cmd = context.Message;
        _logger.LogInformation("Processing file command: {Protocol} {Path}", cmd.Protocol, cmd.SourcePath);

        // Get the appropriate protocol service
        IFileProtocolService sourceService = cmd.Protocol.ToLowerInvariant() switch
        {
            "ftp" => _services.GetRequiredService<FtpFileService>(),
            "sftp" => _services.GetRequiredService<SftpFileService>(),
            "s3" => _services.GetRequiredService<S3FileService>(),
            "http" => _services.GetRequiredService<HttpFileService>(),
            "smb" => _services.GetRequiredService<SmbFileService>(),
            "nfs" => _services.GetRequiredService<NfsFileService>(),
            _ => throw new ArgumentException($"Unknown protocol: {cmd.Protocol}")
        };

        // Read from source
        var content = await sourceService.ReadFileAsync(cmd.SourcePath, context.CancellationToken);

        // Write to destination
        var destKey = cmd.DestinationKey ?? Path.GetFileName(cmd.SourcePath);
        await _s3Output.WriteFileAsync($"{cmd.DestinationBucket}/{destKey}", content, context.CancellationToken);

        _logger.LogInformation("File processed: {Source} -> {Dest}/{Key}",
            cmd.SourcePath, cmd.DestinationBucket, destKey);
    }
}

// ============================================================================
// HEALTH CHECK FOR ALL FILE SERVICES
// ============================================================================

public class FileServicesHealthCheck : IHealthCheck
{
    private readonly FtpFileService _ftp;
    private readonly SftpFileService _sftp;
    private readonly S3FileService _s3;
    private readonly HttpFileService _http;
    private readonly SmbFileService _smb;
    private readonly NfsFileService _nfs;

    public FileServicesHealthCheck(
        FtpFileService ftp,
        SftpFileService sftp,
        S3FileService s3,
        HttpFileService http,
        SmbFileService smb,
        NfsFileService nfs)
    {
        _ftp = ftp;
        _sftp = sftp;
        _s3 = s3;
        _http = http;
        _smb = smb;
        _nfs = nfs;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, object>();
        var allHealthy = true;

        // Check each service
        var checks = new (string name, Func<Task<bool>> check)[]
        {
            ("FTP", () => _ftp.HealthCheckAsync(cancellationToken)),
            ("SFTP", () => _sftp.HealthCheckAsync(cancellationToken)),
            ("S3", () => _s3.HealthCheckAsync(cancellationToken)),
            ("HTTP", () => _http.HealthCheckAsync(cancellationToken)),
            ("SMB", () => _smb.HealthCheckAsync(cancellationToken)),
            ("NFS", () => _nfs.HealthCheckAsync(cancellationToken))
        };

        foreach (var (name, check) in checks)
        {
            try
            {
                var healthy = await check();
                results[name] = healthy ? "Healthy" : "Unhealthy";
                if (!healthy) allHealthy = false;
            }
            catch (Exception ex)
            {
                results[name] = $"Error: {ex.Message}";
                allHealthy = false;
            }
        }

        return allHealthy
            ? HealthCheckResult.Healthy("All file services are healthy", results)
            : HealthCheckResult.Degraded("Some file services are unhealthy", data: results);
    }
}