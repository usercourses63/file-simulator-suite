using FileSimulator.Client.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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

    #region Individual Protocol Service Registration

    /// <summary>
    /// Adds FtpFileService for direct dependency injection
    /// </summary>
    public static IServiceCollection AddFtpFileService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FtpServerOptions>(configuration.GetSection("FileSimulator:Ftp"));
        services.AddSingleton<FtpFileService>();
        services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<FtpFileService>());
        return services;
    }

    /// <summary>
    /// Adds FtpFileService with explicit options
    /// </summary>
    public static IServiceCollection AddFtpFileService(
        this IServiceCollection services,
        Action<FtpServerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<FtpFileService>();
        services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<FtpFileService>());
        return services;
    }

    /// <summary>
    /// Adds SftpFileService for direct dependency injection
    /// </summary>
    public static IServiceCollection AddSftpFileService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SftpServerOptions>(configuration.GetSection("FileSimulator:Sftp"));
        services.AddSingleton<SftpFileService>();
        services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<SftpFileService>());
        return services;
    }

    /// <summary>
    /// Adds SftpFileService with explicit options
    /// </summary>
    public static IServiceCollection AddSftpFileService(
        this IServiceCollection services,
        Action<SftpServerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<SftpFileService>();
        services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<SftpFileService>());
        return services;
    }

    /// <summary>
    /// Adds S3FileService for direct dependency injection
    /// </summary>
    public static IServiceCollection AddS3FileService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<S3ServerOptions>(configuration.GetSection("FileSimulator:S3"));
        services.AddSingleton<S3FileService>();
        services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<S3FileService>());
        return services;
    }

    /// <summary>
    /// Adds S3FileService with explicit options
    /// </summary>
    public static IServiceCollection AddS3FileService(
        this IServiceCollection services,
        Action<S3ServerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<S3FileService>();
        services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<S3FileService>());
        return services;
    }

    /// <summary>
    /// Adds HttpFileService for direct dependency injection
    /// </summary>
    public static IServiceCollection AddHttpFileService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<HttpServerOptions>(configuration.GetSection("FileSimulator:Http"));
        services.AddSingleton<HttpFileService>();
        services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<HttpFileService>());
        return services;
    }

    /// <summary>
    /// Adds HttpFileService with explicit options
    /// </summary>
    public static IServiceCollection AddHttpFileService(
        this IServiceCollection services,
        Action<HttpServerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<HttpFileService>();
        services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<HttpFileService>());
        return services;
    }

    /// <summary>
    /// Adds SmbFileService for direct dependency injection
    /// </summary>
    public static IServiceCollection AddSmbFileService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SmbServerOptions>(configuration.GetSection("FileSimulator:Smb"));
        services.AddSingleton<SmbFileService>();
        services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<SmbFileService>());
        return services;
    }

    /// <summary>
    /// Adds SmbFileService with explicit options
    /// </summary>
    public static IServiceCollection AddSmbFileService(
        this IServiceCollection services,
        Action<SmbServerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<SmbFileService>();
        services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<SmbFileService>());
        return services;
    }

    /// <summary>
    /// Adds NfsFileService for direct dependency injection.
    /// NFS uses mounted filesystem approach - ensure NFS is mounted at the configured path.
    /// </summary>
    public static IServiceCollection AddNfsFileService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<NfsServerOptions>(configuration.GetSection("FileSimulator:Nfs"));
        services.AddSingleton<NfsFileService>();
        services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<NfsFileService>());
        return services;
    }

    /// <summary>
    /// Adds NfsFileService with explicit options.
    /// NFS uses mounted filesystem approach - ensure NFS is mounted at the configured path.
    /// </summary>
    public static IServiceCollection AddNfsFileService(
        this IServiceCollection services,
        Action<NfsServerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<NfsFileService>();
        services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<NfsFileService>());
        return services;
    }

    /// <summary>
    /// Adds all file protocol services for direct dependency injection.
    /// Each service can be injected individually or via IEnumerable&lt;IFileProtocolService&gt;.
    /// </summary>
    public static IServiceCollection AddAllFileProtocolServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddFtpFileService(configuration);
        services.AddSftpFileService(configuration);
        services.AddS3FileService(configuration);
        services.AddHttpFileService(configuration);
        services.AddSmbFileService(configuration);
        services.AddNfsFileService(configuration);
        return services;
    }

    #endregion
}
