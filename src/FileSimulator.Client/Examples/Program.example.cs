// Example Program.cs for a .NET 9.0 Microservice using File Simulator Suite
// This shows how to configure multiple file server connections

using FileSimulator.Client;
using FileSimulator.Client.Extensions;
using FileSimulator.Client.Examples;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Option 1: Single Instance Configuration
// (Use when you have one of each protocol type)
// ============================================

// Automatically detects environment (Minikube vs In-Cluster)
if (Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") != null)
{
    // Running inside Kubernetes cluster
    builder.Services.AddFileSimulatorForCluster(
        @namespace: "file-simulator",
        releaseName: "file-sim");
}
else
{
    // Running locally against Minikube
    var minikubeIp = Environment.GetEnvironmentVariable("MINIKUBE_IP") ?? "192.168.49.2";
    builder.Services.AddFileSimulatorForMinikube(minikubeIp);
}

// ============================================
// Option 2: Multi-Instance Configuration
// (Use when simulating multiple servers)
// ============================================

// Register multiple FTP clients
builder.Services.AddKeyedSingleton<FileSimulatorClient>("ftp-primary", (sp, key) =>
{
    var config = builder.Configuration.GetSection("FileSimulator:FtpPrimary");
    return new FileSimulatorClient(new FileSimulatorOptions
    {
        FtpHost = config["Host"] ?? "localhost",
        FtpPort = int.Parse(config["Port"] ?? "30021"),
        FtpUsername = config["Username"] ?? "ftpuser1",
        FtpPassword = config["Password"] ?? "ftppass123"
    });
});

builder.Services.AddKeyedSingleton<FileSimulatorClient>("ftp-secondary", (sp, key) =>
{
    var config = builder.Configuration.GetSection("FileSimulator:FtpSecondary");
    return new FileSimulatorClient(new FileSimulatorOptions
    {
        FtpHost = config["Host"] ?? "localhost",
        FtpPort = int.Parse(config["Port"] ?? "30022"),
        FtpUsername = config["Username"] ?? "ftpuser2",
        FtpPassword = config["Password"] ?? "ftppass456"
    });
});

// Register multiple S3 clients
builder.Services.AddKeyedSingleton<FileSimulatorClient>("s3-primary", (sp, key) =>
{
    var config = builder.Configuration.GetSection("FileSimulator:S3Primary");
    return new FileSimulatorClient(new FileSimulatorOptions
    {
        S3Endpoint = config["Endpoint"] ?? "http://localhost:30900",
        S3AccessKey = config["AccessKey"] ?? "minio-primary",
        S3SecretKey = config["SecretKey"] ?? "minio-primary-secret"
    });
});

builder.Services.AddKeyedSingleton<FileSimulatorClient>("s3-archive", (sp, key) =>
{
    var config = builder.Configuration.GetSection("FileSimulator:S3Archive");
    return new FileSimulatorClient(new FileSimulatorOptions
    {
        S3Endpoint = config["Endpoint"] ?? "http://localhost:30910",
        S3AccessKey = config["AccessKey"] ?? "minio-archive",
        S3SecretKey = config["SecretKey"] ?? "minio-archive-secret"
    });
});

// ============================================
// Health Checks
// ============================================

builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddTcpHealthCheck(
        setup => setup.AddHost(
            builder.Configuration["FileSimulator:FtpPrimary:Host"] ?? "localhost",
            int.Parse(builder.Configuration["FileSimulator:FtpPrimary:Port"] ?? "30021")),
        name: "ftp-primary",
        tags: ["file-simulator", "ftp"])
    .AddUrlGroup(
        new Uri($"{builder.Configuration["FileSimulator:S3Primary:Endpoint"]}/minio/health/live"),
        name: "s3-primary",
        tags: ["file-simulator", "s3"]);

// ============================================
// MassTransit Configuration
// ============================================

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProcessFileConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("file-processing", e =>
        {
            e.ConfigureConsumer<ProcessFileConsumer>(context);
            e.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15)));
        });

        cfg.ConfigureEndpoints(context);
    });
});

// ============================================
// Build & Run
// ============================================

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("file-simulator")
});

// Example endpoint showing multi-client usage
app.MapPost("/transfer-file", async (
    [FromKeyedServices("ftp-primary")] FileSimulatorClient ftpPrimary,
    [FromKeyedServices("s3-archive")] FileSimulatorClient s3Archive,
    string sourcePath,
    string destinationKey) =>
{
    var tempFile = Path.GetTempFileName();
    try
    {
        // Download from primary FTP
        await ftpPrimary.DownloadViaFtpAsync(sourcePath, tempFile);
        
        // Upload to archive S3
        await s3Archive.UploadViaS3Async(tempFile, "archive", destinationKey);
        
        return Results.Ok(new { Message = "File transferred successfully" });
    }
    finally
    {
        if (File.Exists(tempFile))
            File.Delete(tempFile);
    }
});

// Example: List files from multiple sources
app.MapGet("/list-files", async (
    [FromKeyedServices("ftp-primary")] FileSimulatorClient ftpPrimary,
    [FromKeyedServices("ftp-secondary")] FileSimulatorClient ftpSecondary) =>
{
    var primaryFiles = await ftpPrimary.ListFtpDirectoryAsync("/");
    var secondaryFiles = await ftpSecondary.ListFtpDirectoryAsync("/");
    
    return Results.Ok(new
    {
        PrimaryServer = primaryFiles,
        SecondaryServer = secondaryFiles
    });
});

app.Run();
