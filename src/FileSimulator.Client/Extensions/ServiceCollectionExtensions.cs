using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using Polly.Extensions.Http;

namespace FileSimulator.Client.Extensions;

/// <summary>
/// Extension methods for registering File Simulator services in ASP.NET Core
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FileSimulatorClient with configuration from appsettings.json or environment variables
    /// </summary>
    public static IServiceCollection AddFileSimulator(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        var options = new FileSimulatorOptions();
        configuration.GetSection("FileSimulator").Bind(options);
        
        // Override with environment variables if present
        options = OverrideFromEnvironment(options);
        
        services.AddSingleton(options);
        services.AddSingleton<FileSimulatorClient>();
        
        return services;
    }

    /// <summary>
    /// Adds FileSimulatorClient configured for Minikube development
    /// </summary>
    public static IServiceCollection AddFileSimulatorForMinikube(
        this IServiceCollection services,
        string minikubeIp)
    {
        var options = FileSimulatorOptions.ForMinikube(minikubeIp);
        services.AddSingleton(options);
        services.AddSingleton<FileSimulatorClient>();
        return services;
    }

    /// <summary>
    /// Adds FileSimulatorClient configured for in-cluster access
    /// </summary>
    public static IServiceCollection AddFileSimulatorForCluster(
        this IServiceCollection services,
        string @namespace = "file-simulator",
        string releaseName = "file-sim")
    {
        var options = FileSimulatorOptions.ForCluster(@namespace, releaseName);
        services.AddSingleton(options);
        services.AddSingleton<FileSimulatorClient>();
        return services;
    }

    /// <summary>
    /// Adds health checks for all file simulator protocols
    /// </summary>
    public static IHealthChecksBuilder AddFileSimulatorHealthChecks(
        this IHealthChecksBuilder builder,
        FileSimulatorOptions options)
    {
        // FTP Health Check
        builder.AddTcpHealthCheck(
            setup => setup.AddHost(options.FtpHost, options.FtpPort),
            name: "file-simulator-ftp",
            tags: new[] { "file-simulator", "ftp" });

        // SFTP Health Check
        builder.AddTcpHealthCheck(
            setup => setup.AddHost(options.SftpHost, options.SftpPort),
            name: "file-simulator-sftp",
            tags: new[] { "file-simulator", "sftp" });

        // S3/MinIO Health Check
        builder.AddUrlGroup(
            new Uri($"{options.S3Endpoint}/minio/health/live"),
            name: "file-simulator-s3",
            tags: new[] { "file-simulator", "s3" });

        // HTTP Health Check
        builder.AddUrlGroup(
            new Uri($"{options.HttpBaseUrl}/health"),
            name: "file-simulator-http",
            tags: new[] { "file-simulator", "http" });

        // NFS Health Check (TCP to NFS port)
        builder.AddTcpHealthCheck(
            setup => setup.AddHost(options.NfsHost, options.NfsPort),
            name: "file-simulator-nfs",
            tags: new[] { "file-simulator", "nfs" });

        return builder;
    }

    /// <summary>
    /// Adds an HttpClient with retry policies for file operations
    /// </summary>
    public static IServiceCollection AddFileSimulatorHttpClient(
        this IServiceCollection services,
        FileSimulatorOptions options)
    {
        services.AddHttpClient("FileSimulator", client =>
        {
            client.BaseAddress = new Uri(options.HttpBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(5); // Large file uploads
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
    }

    private static FileSimulatorOptions OverrideFromEnvironment(FileSimulatorOptions options)
    {
        // FTP
        if (Environment.GetEnvironmentVariable("FILE_FTP_HOST") is { } ftpHost)
            options.FtpHost = ftpHost;
        if (int.TryParse(Environment.GetEnvironmentVariable("FILE_FTP_PORT"), out var ftpPort))
            options.FtpPort = ftpPort;
        if (Environment.GetEnvironmentVariable("FILE_FTP_USERNAME") is { } ftpUser)
            options.FtpUsername = ftpUser;
        if (Environment.GetEnvironmentVariable("FILE_FTP_PASSWORD") is { } ftpPass)
            options.FtpPassword = ftpPass;

        // SFTP
        if (Environment.GetEnvironmentVariable("FILE_SFTP_HOST") is { } sftpHost)
            options.SftpHost = sftpHost;
        if (int.TryParse(Environment.GetEnvironmentVariable("FILE_SFTP_PORT"), out var sftpPort))
            options.SftpPort = sftpPort;
        if (Environment.GetEnvironmentVariable("FILE_SFTP_USERNAME") is { } sftpUser)
            options.SftpUsername = sftpUser;
        if (Environment.GetEnvironmentVariable("FILE_SFTP_PASSWORD") is { } sftpPass)
            options.SftpPassword = sftpPass;

        // S3
        if (Environment.GetEnvironmentVariable("FILE_S3_ENDPOINT") is { } s3Endpoint)
            options.S3Endpoint = s3Endpoint;
        if (Environment.GetEnvironmentVariable("FILE_S3_ACCESS_KEY") is { } s3Access)
            options.S3AccessKey = s3Access;
        if (Environment.GetEnvironmentVariable("FILE_S3_SECRET_KEY") is { } s3Secret)
            options.S3SecretKey = s3Secret;

        // HTTP
        if (Environment.GetEnvironmentVariable("FILE_HTTP_URL") is { } httpUrl)
            options.HttpBaseUrl = httpUrl;

        // NFS
        if (Environment.GetEnvironmentVariable("FILE_NFS_MOUNT_PATH") is { } nfsMountPath)
            options.NfsMountPath = nfsMountPath;
        if (Environment.GetEnvironmentVariable("FILE_NFS_HOST") is { } nfsHost)
            options.NfsHost = nfsHost;
        if (int.TryParse(Environment.GetEnvironmentVariable("FILE_NFS_PORT"), out var nfsPort))
            options.NfsPort = nfsPort;

        return options;
    }
}
