using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Support;
using FluentAssertions;
using Xunit;

namespace FileSimulator.IntegrationTests;

/// <summary>
/// Smoke tests to validate the test infrastructure is working correctly.
/// These tests run first to verify API connectivity and fixture setup.
/// </summary>
[Collection("Simulator")]
public class SmokeTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public SmokeTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ApiHealth_ReturnsSuccess()
    {
        // Arrange & Act
        var response = await _fixture.ApiClient.GetAsync("/health");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue(
            "Control API should be healthy and responding");
    }

    [Fact]
    public async Task ConnectionInfo_ReturnsValidResponse()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();

        // Assert
        connectionInfo.Hostname.Should().NotBeNullOrEmpty("Hostname should be configured");
        connectionInfo.Servers.Should().NotBeEmpty("At least one server should be present");
        connectionInfo.Endpoints.Should().NotBeNull("Endpoints should be present");
    }

    [Fact]
    public async Task ConnectionInfo_HasFtpServer()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var ftpServer = connectionInfo.GetServer("FTP");

        // Assert
        ftpServer.Should().NotBeNull("FTP server should be present");
        ftpServer!.Host.Should().NotBeNullOrEmpty("FTP host should be configured");
        ftpServer.Port.Should().BeGreaterThan(0, "FTP port should be valid");
        ftpServer.Credentials.Username.Should().NotBeNullOrEmpty("FTP username should be configured");
    }

    [Fact]
    public async Task ConnectionInfo_HasSftpServer()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var sftpServer = connectionInfo.GetServer("SFTP");

        // Assert
        sftpServer.Should().NotBeNull("SFTP server should be present");
        sftpServer!.Host.Should().NotBeNullOrEmpty("SFTP host should be configured");
        sftpServer.Port.Should().BeGreaterThan(0, "SFTP port should be valid");
        sftpServer.Credentials.Username.Should().NotBeNullOrEmpty("SFTP username should be configured");
    }

    [Fact]
    public async Task ConnectionInfo_HasHttpServer()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var httpServer = connectionInfo.GetServer("HTTP");

        // Assert
        httpServer.Should().NotBeNull("HTTP server should be present");
        httpServer!.Host.Should().NotBeNullOrEmpty("HTTP host should be configured");
        httpServer.Port.Should().BeGreaterThan(0, "HTTP port should be valid");
    }

    [Fact]
    public async Task ConnectionInfo_HasS3Server()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var s3Server = connectionInfo.GetServer("S3");

        // Assert
        s3Server.Should().NotBeNull("S3 server should be present");
        s3Server!.Host.Should().NotBeNullOrEmpty("S3 host should be configured");
        s3Server.Port.Should().BeGreaterThan(0, "S3 port should be valid");
        s3Server.Credentials.Username.Should().NotBeNullOrEmpty("S3 access key should be configured");
    }

    [Fact]
    public async Task ConnectionInfo_HasSmbServer()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var smbServer = connectionInfo.GetServer("SMB");

        // Assert
        smbServer.Should().NotBeNull("SMB server should be present");
        smbServer!.Host.Should().NotBeNullOrEmpty("SMB host should be configured");
        smbServer.Port.Should().BeGreaterThan(0, "SMB port should be valid");
        smbServer.Credentials.Username.Should().NotBeNullOrEmpty("SMB username should be configured");
    }

    [Fact]
    public async Task ConnectionInfo_HasNfsServers()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var nfsServers = connectionInfo.GetServers("NFS").ToList();

        // Assert
        nfsServers.Should().NotBeEmpty("At least one NFS server should be present");
        var firstNfs = nfsServers.First();
        firstNfs.Host.Should().NotBeNullOrEmpty("NFS host should be configured");
        firstNfs.Port.Should().BeGreaterThan(0, "NFS port should be valid");
    }

    [Fact]
    public async Task ConnectionInfo_HasEndpoints()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();

        // Assert
        connectionInfo.Endpoints.Dashboard.Should().NotBeNullOrEmpty("Dashboard endpoint should be configured");
        connectionInfo.Endpoints.ControlApi.Should().NotBeNullOrEmpty("Control API endpoint should be configured");
        connectionInfo.Endpoints.Ftp.Should().NotBeNullOrEmpty("FTP endpoint should be configured");
        connectionInfo.Endpoints.Sftp.Should().NotBeNullOrEmpty("SFTP endpoint should be configured");
    }

    [Fact]
    public async Task RetryPolicy_HandlesTransientFailures()
    {
        // Arrange
        var policy = RetryPolicies.HttpRetryPolicy(maxAttempts: 1, baseDelaySeconds: 1);
        var attempts = 0;

        // Act
        var response = await policy.ExecuteAsync(async () =>
        {
            attempts++;
            return await _fixture.ApiClient.GetAsync("/health");
        });

        // Assert
        attempts.Should().BeGreaterThan(0, "Policy should have executed at least once");
        response.IsSuccessStatusCode.Should().BeTrue("Health endpoint should succeed");
    }

    [Fact]
    public void TestHelpers_GenerateUniqueFileName_CreatesUniqueNames()
    {
        // Act
        var name1 = TestHelpers.GenerateUniqueFileName("test");
        var name2 = TestHelpers.GenerateUniqueFileName("test");

        // Assert
        name1.Should().NotBe(name2, "Each generated name should be unique");
        name1.Should().StartWith("test-", "Name should start with prefix");
        name1.Should().EndWith(".txt", "Name should have .txt extension");
    }

    [Fact]
    public void TestHelpers_GenerateUniqueServerName_RespectsLengthLimit()
    {
        // Act
        var name = TestHelpers.GenerateUniqueServerName("verylongtypename");

        // Assert
        name.Length.Should().BeLessOrEqualTo(20, "Server name should not exceed 20 characters");
        name.Should().StartWith("test-", "Server name should start with test-");
    }
}
