using MassTransit;
using Microsoft.Extensions.Logging;

namespace FileSimulator.Client.Examples;

// ============================================
// Message Contracts
// ============================================

/// <summary>
/// Command to process a file from an external source
/// </summary>
public record ProcessFileCommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public required string SourceProtocol { get; init; }  // "ftp", "sftp", "s3", "http"
    public required string SourcePath { get; init; }
    public required string DestinationBucket { get; init; }
    public string? DestinationKey { get; init; }
}

/// <summary>
/// Event published when file processing completes
/// </summary>
public record FileProcessedEvent
{
    public Guid CorrelationId { get; init; }
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event published when file processing fails
/// </summary>
public record FileProcessingFailedEvent
{
    public Guid CorrelationId { get; init; }
    public required string SourcePath { get; init; }
    public required string Error { get; init; }
    public DateTime FailedAt { get; init; } = DateTime.UtcNow;
}

// ============================================
// MassTransit Consumer
// ============================================

/// <summary>
/// Example MassTransit consumer that processes files from various protocols
/// </summary>
public class ProcessFileConsumer : IConsumer<ProcessFileCommand>
{
    private readonly FileSimulatorClient _fileClient;
    private readonly ILogger<ProcessFileConsumer> _logger;

    public ProcessFileConsumer(
        FileSimulatorClient fileClient,
        ILogger<ProcessFileConsumer> logger)
    {
        _fileClient = fileClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessFileCommand> context)
    {
        var command = context.Message;
        var tempFile = Path.GetTempFileName();

        try
        {
            _logger.LogInformation(
                "Processing file {SourcePath} via {Protocol}",
                command.SourcePath,
                command.SourceProtocol);

            // 1. Download from source protocol
            var protocol = Enum.Parse<FileProtocol>(command.SourceProtocol, ignoreCase: true);
            await _fileClient.DownloadAsync(command.SourcePath, tempFile, protocol);

            var fileInfo = new FileInfo(tempFile);
            _logger.LogInformation("Downloaded {Size} bytes", fileInfo.Length);

            // 2. Process the file (your business logic here)
            // Example: validate, transform, etc.

            // 3. Upload to S3 output bucket
            var destinationKey = command.DestinationKey 
                ?? $"{DateTime.UtcNow:yyyy/MM/dd}/{Path.GetFileName(command.SourcePath)}";
            
            await _fileClient.UploadViaS3Async(
                tempFile,
                command.DestinationBucket,
                destinationKey);

            // 4. Publish success event
            await context.Publish(new FileProcessedEvent
            {
                CorrelationId = command.CorrelationId,
                SourcePath = command.SourcePath,
                DestinationPath = $"s3://{command.DestinationBucket}/{destinationKey}",
                FileSizeBytes = fileInfo.Length
            });

            _logger.LogInformation(
                "File processed successfully: {Destination}",
                destinationKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file {SourcePath}", command.SourcePath);

            await context.Publish(new FileProcessingFailedEvent
            {
                CorrelationId = command.CorrelationId,
                SourcePath = command.SourcePath,
                Error = ex.Message
            });

            throw; // Let MassTransit handle retry/error queue
        }
        finally
        {
            // Cleanup temp file
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}

// ============================================
// MassTransit Configuration Example
// ============================================

/// <summary>
/// Example Program.cs setup for MassTransit with File Simulator
/// </summary>
public static class MassTransitSetupExample
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // Add File Simulator client
        services.AddFileSimulatorForCluster();

        // Configure MassTransit with RabbitMQ
        services.AddMassTransit(x =>
        {
            // Register consumers
            x.AddConsumer<ProcessFileConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(config["RabbitMQ:Host"], "/", h =>
                {
                    h.Username(config["RabbitMQ:Username"] ?? "guest");
                    h.Password(config["RabbitMQ:Password"] ?? "guest");
                });

                // Configure endpoint with retry
                cfg.ReceiveEndpoint("file-processing", e =>
                {
                    e.ConfigureConsumer<ProcessFileConsumer>(context);
                    
                    // Retry policy for transient failures
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(15)));
                });

                cfg.ConfigureEndpoints(context);
            });
        });
    }
}

// ============================================
// Usage from Controller/Service
// ============================================

/// <summary>
/// Example controller showing how to trigger file processing
/// </summary>
public class FileProcessingController
{
    private readonly IPublishEndpoint _publishEndpoint;

    public FileProcessingController(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task TriggerFtpFileProcessing(string ftpPath)
    {
        await _publishEndpoint.Publish(new ProcessFileCommand
        {
            SourceProtocol = "ftp",
            SourcePath = ftpPath,
            DestinationBucket = "output"
        });
    }

    public async Task TriggerSftpFileProcessing(string sftpPath)
    {
        await _publishEndpoint.Publish(new ProcessFileCommand
        {
            SourceProtocol = "sftp",
            SourcePath = sftpPath,
            DestinationBucket = "output"
        });
    }

    public async Task TriggerS3FileProcessing(string bucket, string key)
    {
        await _publishEndpoint.Publish(new ProcessFileCommand
        {
            SourceProtocol = "s3",
            SourcePath = $"{bucket}/{key}",
            DestinationBucket = "processed"
        });
    }
}
