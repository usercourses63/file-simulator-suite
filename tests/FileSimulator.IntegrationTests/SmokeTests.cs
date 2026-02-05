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
    public async Task ConnectionInfo_ReturnsValidFtpConfig()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();

        // Assert
        connectionInfo.Ftp.Should().NotBeNull("FTP configuration should be present");
        connectionInfo.Ftp.Host.Should().NotBeNullOrEmpty("FTP host should be configured");
        connectionInfo.Ftp.Port.Should().BeGreaterThan(0, "FTP port should be valid");
    }

    [Fact]
    public async Task ConnectionInfo_ReturnsValidSftpConfig()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();

        // Assert
        connectionInfo.Sftp.Should().NotBeNull("SFTP configuration should be present");
        connectionInfo.Sftp.Host.Should().NotBeNullOrEmpty("SFTP host should be configured");
        connectionInfo.Sftp.Port.Should().BeGreaterThan(0, "SFTP port should be valid");
    }

    [Fact]
    public async Task ConnectionInfo_ReturnsValidHttpConfig()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();

        // Assert
        connectionInfo.Http.Should().NotBeNull("HTTP configuration should be present");
        connectionInfo.Http.Host.Should().NotBeNullOrEmpty("HTTP host should be configured");
        connectionInfo.Http.Port.Should().BeGreaterThan(0, "HTTP port should be valid");
    }

    [Fact]
    public async Task ConnectionInfo_ReturnsValidS3Config()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();

        // Assert
        connectionInfo.S3.Should().NotBeNull("S3 configuration should be present");
        connectionInfo.S3.Host.Should().NotBeNullOrEmpty("S3 host should be configured");
        connectionInfo.S3.Port.Should().BeGreaterThan(0, "S3 port should be valid");
    }

    [Fact]
    public async Task ConnectionInfo_ReturnsValidSmbConfig()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();

        // Assert
        connectionInfo.Smb.Should().NotBeNull("SMB configuration should be present");
        connectionInfo.Smb.Host.Should().NotBeNullOrEmpty("SMB host should be configured");
        connectionInfo.Smb.Port.Should().BeGreaterThan(0, "SMB port should be valid");
    }

    [Fact]
    public async Task ConnectionInfo_ReturnsValidNfsConfig()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();

        // Assert
        connectionInfo.Nfs.Should().NotBeNull("NFS configuration should be present");
        connectionInfo.Nfs.Host.Should().NotBeNullOrEmpty("NFS host should be configured");
        connectionInfo.Nfs.Port.Should().BeGreaterThan(0, "NFS port should be valid");
    }

    [Fact]
    public async Task ConnectionInfo_ReturnsValidKafkaConfig()
    {
        // Arrange & Act
        var connectionInfo = await _fixture.GetConnectionInfoAsync();

        // Assert
        connectionInfo.Kafka.Should().NotBeNull("Kafka configuration should be present");
        connectionInfo.Kafka.Host.Should().NotBeNullOrEmpty("Kafka host should be configured");
        connectionInfo.Kafka.Port.Should().BeGreaterThan(0, "Kafka port should be valid");
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
