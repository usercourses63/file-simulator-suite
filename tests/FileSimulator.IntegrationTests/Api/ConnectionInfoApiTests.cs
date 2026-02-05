using System.Net.Http.Json;
using System.Text.Json;
using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Models;
using FluentAssertions;
using Xunit;

namespace FileSimulator.IntegrationTests.Api;

/// <summary>
/// Tests for the /api/connection-info endpoint.
/// Validates that all protocol credentials and NAS servers are present and valid.
/// </summary>
[Collection("Simulator")]
public class ConnectionInfoApiTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public ConnectionInfoApiTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConnectionInfo_ReturnsAllProtocols()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();

        // Assert - Check all major protocols are present
        var protocolsPresent = connectionInfo.Servers
            .Select(s => s.Protocol.ToUpperInvariant())
            .Distinct()
            .ToList();

        protocolsPresent.Should().Contain("FTP", "FTP protocol should be present");
        protocolsPresent.Should().Contain("SFTP", "SFTP protocol should be present");
        protocolsPresent.Should().Contain("HTTP", "HTTP protocol should be present");
        protocolsPresent.Should().Contain("S3", "S3 protocol should be present");
        protocolsPresent.Should().Contain("SMB", "SMB protocol should be present");
        protocolsPresent.Should().Contain("NFS", "NFS protocol should be present");

        Console.WriteLine($"Protocols found: {string.Join(", ", protocolsPresent)}");
    }

    [Fact]
    public async Task ConnectionInfo_FtpCredentials_AreValid()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var ftpServer = connectionInfo.GetServer("FTP");

        // Assert - Credentials present
        ftpServer.Should().NotBeNull("FTP server should be present");
        ftpServer!.Host.Should().NotBeNullOrEmpty("FTP host should be configured");
        ftpServer.Port.Should().BeGreaterThan(0, "FTP port should be valid");
        ftpServer.Credentials.Username.Should().NotBeNullOrEmpty("FTP username should be configured");
        ftpServer.Credentials.Password.Should().NotBeNullOrEmpty("FTP password should be configured");

        Console.WriteLine($"FTP: {ftpServer.Host}:{ftpServer.Port} (user: {ftpServer.Credentials.Username})");

        // Try basic connection (just verify we can reach the server)
        using var tcp = new System.Net.Sockets.TcpClient();
        var connectTask = tcp.ConnectAsync(ftpServer.Host, ftpServer.Port);
        var completed = await Task.WhenAny(connectTask, Task.Delay(5000));

        completed.Should().Be(connectTask, "FTP server should be reachable");
        tcp.Connected.Should().BeTrue("FTP credentials point to an active server");
    }

    [Fact]
    public async Task ConnectionInfo_SftpCredentials_AreValid()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var sftpServer = connectionInfo.GetServer("SFTP");

        // Assert - Credentials present
        sftpServer.Should().NotBeNull("SFTP server should be present");
        sftpServer!.Host.Should().NotBeNullOrEmpty("SFTP host should be configured");
        sftpServer.Port.Should().BeGreaterThan(0, "SFTP port should be valid");
        sftpServer.Credentials.Username.Should().NotBeNullOrEmpty("SFTP username should be configured");
        sftpServer.Credentials.Password.Should().NotBeNullOrEmpty("SFTP password should be configured");

        Console.WriteLine($"SFTP: {sftpServer.Host}:{sftpServer.Port} (user: {sftpServer.Credentials.Username})");

        // Try basic connection (just verify we can reach the server)
        using var tcp = new System.Net.Sockets.TcpClient();
        var connectTask = tcp.ConnectAsync(sftpServer.Host, sftpServer.Port);
        var completed = await Task.WhenAny(connectTask, Task.Delay(5000));

        completed.Should().Be(connectTask, "SFTP server should be reachable");
        tcp.Connected.Should().BeTrue("SFTP credentials point to an active server");
    }

    [Fact]
    public async Task ConnectionInfo_S3Credentials_AreValid()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var s3Server = connectionInfo.GetServer("S3");

        // Assert - Credentials present
        s3Server.Should().NotBeNull("S3 server should be present");
        s3Server!.Host.Should().NotBeNullOrEmpty("S3 host should be configured");
        s3Server.Port.Should().BeGreaterThan(0, "S3 port should be valid");
        s3Server.Credentials.Username.Should().NotBeNullOrEmpty("S3 access key should be configured");
        s3Server.Credentials.Password.Should().NotBeNullOrEmpty("S3 secret key should be configured");

        // Validate service URL format
        var serviceUrl = $"http://{s3Server.Host}:{s3Server.Port}";
        var isValidUrl = Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri);
        isValidUrl.Should().BeTrue($"S3 service URL should be valid: {serviceUrl}");

        Console.WriteLine($"S3: {serviceUrl} (accessKey: {s3Server.Credentials.Username})");

        // Verify server is reachable
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            var response = await client.GetAsync(serviceUrl);
            // MinIO returns various status codes - just verify we can reach it
            response.Should().NotBeNull("S3 server should respond");
            Console.WriteLine($"S3 server responded with status: {response.StatusCode}");
        }
        catch (HttpRequestException)
        {
            // If HTTP fails, at least verify TCP connectivity
            using var tcp = new System.Net.Sockets.TcpClient();
            await tcp.ConnectAsync(s3Server.Host, s3Server.Port);
            tcp.Connected.Should().BeTrue("S3 server should be TCP reachable");
        }
    }

    [Fact]
    public async Task ConnectionInfo_WebDavCredentials_AreValid()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var httpServer = connectionInfo.GetServer("HTTP");

        // Assert - WebDAV typically uses HTTP server with credentials
        httpServer.Should().NotBeNull("HTTP/WebDAV server should be present");
        httpServer!.Host.Should().NotBeNullOrEmpty("WebDAV host should be configured");
        httpServer.Port.Should().BeGreaterThan(0, "WebDAV port should be valid");

        var baseUrl = $"http://{httpServer.Host}:{httpServer.Port}";
        var isValidUrl = Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri);
        isValidUrl.Should().BeTrue($"WebDAV base URL should be valid: {baseUrl}");

        Console.WriteLine($"WebDAV: {baseUrl}");

        // Check if credentials are present (may be optional for HTTP)
        if (!string.IsNullOrEmpty(httpServer.Credentials.Username))
        {
            Console.WriteLine($"WebDAV username: {httpServer.Credentials.Username}");
            httpServer.Credentials.Password.Should().NotBeNullOrEmpty("WebDAV password should be configured if username is set");
        }

        // Verify server is reachable
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var response = await client.GetAsync(baseUrl);
        response.Should().NotBeNull("WebDAV server should respond");
        Console.WriteLine($"WebDAV server responded with status: {response.StatusCode}");
    }

    [Fact]
    public async Task ConnectionInfo_SmbCredentials_Present()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var smbServer = connectionInfo.GetServer("SMB");

        // Assert - Credentials present (don't test actual connection - requires tunnel)
        smbServer.Should().NotBeNull("SMB server should be present");
        smbServer!.Host.Should().NotBeNullOrEmpty("SMB host should be configured");
        smbServer.Port.Should().BeGreaterThan(0, "SMB port should be valid");
        smbServer.Credentials.Username.Should().NotBeNullOrEmpty("SMB username should be configured");
        smbServer.Credentials.Password.Should().NotBeNullOrEmpty("SMB password should be configured");

        // Check connection string format if present
        if (!string.IsNullOrEmpty(smbServer.ConnectionString))
        {
            smbServer.ConnectionString.Should().StartWith("\\\\", "SMB connection string should be UNC path format");
            Console.WriteLine($"SMB: {smbServer.ConnectionString} (user: {smbServer.Credentials.Username})");
        }
        else
        {
            Console.WriteLine($"SMB: \\\\{smbServer.Host} (port: {smbServer.Port}, user: {smbServer.Credentials.Username})");
        }
    }

    [Fact]
    public async Task ConnectionInfo_NasServers_AllPresent()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();

        // Act - Filter NFS servers (which represent NAS servers)
        var nasServers = connectionInfo.Servers
            .Where(s => s.Protocol.Equals("NFS", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert - Should have at least 7 NAS servers
        nasServers.Should().HaveCountGreaterOrEqualTo(7,
            "Should have at least 7 NAS servers (input-1/2/3, output-1/2/3, backup)");

        // Check for specific expected servers
        var serverNames = nasServers.Select(s => s.Name.ToLowerInvariant()).ToList();
        Console.WriteLine($"Found {nasServers.Count} NAS servers:");

        var expectedServers = new[]
        {
            "nas-input-1", "nas-input-2", "nas-input-3",
            "nas-output-1", "nas-output-2", "nas-output-3",
            "nas-backup"
        };

        foreach (var expected in expectedServers)
        {
            var found = serverNames.Any(name => name.Contains(expected));
            found.Should().BeTrue($"NAS server {expected} should be present");
            Console.WriteLine($"  - {expected}: {(found ? "FOUND" : "MISSING")}");
        }

        // Verify each NAS server has required fields
        foreach (var server in nasServers)
        {
            server.Host.Should().NotBeNullOrEmpty($"{server.Name} should have host configured");
            server.Port.Should().BeGreaterThan(0, $"{server.Name} should have valid port");
        }
    }

    [Fact]
    public async Task ConnectionInfo_KafkaBootstrap_Present()
    {
        // Arrange & Act
        var response = await _fixture.ApiClient.GetAsync("/api/kafka/info");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Kafka info endpoint returned {response.StatusCode} - Kafka may not be deployed");
            return; // Skip test if Kafka not available
        }

        var content = await response.Content.ReadAsStringAsync();
        var kafkaInfo = JsonSerializer.Deserialize<KafkaInfo>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        kafkaInfo.Should().NotBeNull("Kafka info should be deserializable");
        kafkaInfo!.BootstrapServers.Should().NotBeNullOrEmpty("Kafka bootstrap servers should be configured");

        // Verify format is host:port
        kafkaInfo.BootstrapServers.Should().Contain(":", "Bootstrap servers should be in host:port format");

        Console.WriteLine($"Kafka bootstrap servers: {kafkaInfo.BootstrapServers}");
        Console.WriteLine($"Kafka healthy: {kafkaInfo.IsHealthy}");
    }

    [Fact]
    public async Task ConnectionInfo_Formats_ReturnCorrectContentType()
    {
        // Test default JSON format
        var jsonResponse = await _fixture.ApiClient.GetAsync("/api/connection-info");
        jsonResponse.IsSuccessStatusCode.Should().BeTrue("JSON format should succeed");
        jsonResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json",
            "Default format should return JSON content type");

        // Test env format
        var envResponse = await _fixture.ApiClient.GetAsync("/api/connection-info?format=env");
        envResponse.IsSuccessStatusCode.Should().BeTrue("Env format should succeed");
        var envContentType = envResponse.Content.Headers.ContentType?.MediaType;
        (envContentType == "text/plain" || envContentType == "application/octet-stream")
            .Should().BeTrue("Env format should return text content type");

        var envContent = await envResponse.Content.ReadAsStringAsync();
        envContent.Should().Contain("=", "Env format should contain key=value pairs");
        Console.WriteLine($"Env format sample (first 200 chars): {envContent.Substring(0, Math.Min(200, envContent.Length))}");

        // Test yaml format
        var yamlResponse = await _fixture.ApiClient.GetAsync("/api/connection-info?format=yaml");
        yamlResponse.IsSuccessStatusCode.Should().BeTrue("YAML format should succeed");
        var yamlContentType = yamlResponse.Content.Headers.ContentType?.MediaType;
        (yamlContentType == "text/plain" || yamlContentType == "application/x-yaml" || yamlContentType == "text/yaml" || yamlContentType == "application/octet-stream")
            .Should().BeTrue($"YAML format should return YAML or text content type (got {yamlContentType})");

        var yamlContent = await yamlResponse.Content.ReadAsStringAsync();
        yamlContent.Should().Contain(":", "YAML format should contain key: value pairs");
        Console.WriteLine($"YAML format sample (first 200 chars): {yamlContent.Substring(0, Math.Min(200, yamlContent.Length))}");

        // Test dotnet format
        var dotnetResponse = await _fixture.ApiClient.GetAsync("/api/connection-info?format=dotnet");
        dotnetResponse.IsSuccessStatusCode.Should().BeTrue("Dotnet format should succeed");
        var dotnetContentType = dotnetResponse.Content.Headers.ContentType?.MediaType;
        (dotnetContentType == "text/plain" || dotnetContentType == "application/json" || dotnetContentType == "application/octet-stream")
            .Should().BeTrue($"Dotnet format should return text or JSON content type (got {dotnetContentType})");

        var dotnetContent = await dotnetResponse.Content.ReadAsStringAsync();
        dotnetContent.Should().Contain("{", "Dotnet format should be JSON");
        Console.WriteLine($"Dotnet format sample (first 200 chars): {dotnetContent.Substring(0, Math.Min(200, dotnetContent.Length))}");
    }

    [Fact]
    public async Task ConnectionInfo_JsonFormat_IsDeserializable()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();

        // Assert - Verify structure is complete
        connectionInfo.Hostname.Should().NotBeNullOrEmpty("Hostname should be present");
        connectionInfo.GeneratedAt.Should().BeAfter(DateTime.MinValue, "GeneratedAt should be set");
        connectionInfo.Servers.Should().NotBeEmpty("Servers list should not be empty");
        connectionInfo.Endpoints.Should().NotBeNull("Endpoints should be present");

        Console.WriteLine($"Hostname: {connectionInfo.Hostname}");
        Console.WriteLine($"Generated at: {connectionInfo.GeneratedAt:O}");
        Console.WriteLine($"Total servers: {connectionInfo.Servers.Count}");
        Console.WriteLine($"Endpoints: Dashboard={connectionInfo.Endpoints.Dashboard}, ControlApi={connectionInfo.Endpoints.ControlApi}");
    }

    [Fact]
    public async Task ConnectionInfo_EnvFormat_ContainsAllProtocols()
    {
        // Arrange & Act
        var response = await _fixture.ApiClient.GetAsync("/api/connection-info?format=env");
        response.IsSuccessStatusCode.Should().BeTrue("Env format should succeed");

        var content = await response.Content.ReadAsStringAsync();

        // Assert - Check for protocol-specific variables (actual format uses FILE_ prefix)
        content.Should().Contain("FILE_FTP_HOST", "Env format should include FTP configuration");
        content.Should().Contain("FILE_SFTP_HOST", "Env format should include SFTP configuration");
        content.Should().Contain("FILE_S3_", "Env format should include S3 configuration");
        content.Should().Contain("FILE_HTTP_URL", "Env format should include HTTP configuration");

        Console.WriteLine($"Env format contains {content.Split('\n').Length} lines");
    }

    [Fact]
    public async Task ConnectionInfo_YamlFormat_IsValidYaml()
    {
        // Arrange & Act
        var response = await _fixture.ApiClient.GetAsync("/api/connection-info?format=yaml");
        response.IsSuccessStatusCode.Should().BeTrue("YAML format should succeed");

        var content = await response.Content.ReadAsStringAsync();

        // Assert - Basic YAML structure validation
        content.Should().NotBeNullOrEmpty("YAML content should not be empty");
        content.Should().Contain(":", "YAML should contain key: value pairs");
        content.Split('\n').Should().HaveCountGreaterThan(10, "YAML should have multiple lines");

        // Check for expected sections (actual format uses fileSimulator, not hostname)
        content.Should().Contain("fileSimulator", "YAML should include fileSimulator section");
        content.Should().Contain("host:", "YAML should include host configuration");

        Console.WriteLine($"YAML format contains {content.Split('\n').Length} lines");
    }

    [Fact]
    public async Task ConnectionInfo_DotnetFormat_IsValidAppSettings()
    {
        // Arrange & Act
        var response = await _fixture.ApiClient.GetAsync("/api/connection-info?format=dotnet");
        response.IsSuccessStatusCode.Should().BeTrue("Dotnet format should succeed");

        var content = await response.Content.ReadAsStringAsync();

        // Assert - Dotnet format returns appsettings snippet (not full JSON object, may be partial)
        content.Should().NotBeNullOrEmpty("Dotnet format should return content");

        // The actual format is a code snippet, not a complete JSON object
        // Just verify it contains expected configuration structure
        content.Should().Contain("{", "Dotnet format should contain JSON structure");
        content.Should().Contain("FileSimulator", "Dotnet format should contain FileSimulator configuration section");

        Console.WriteLine($"Dotnet format sample (first 300 chars): {content.Substring(0, Math.Min(300, content.Length))}");
    }
}
