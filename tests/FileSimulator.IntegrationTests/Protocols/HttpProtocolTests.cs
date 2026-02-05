using FileSimulator.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace FileSimulator.IntegrationTests.Protocols;

/// <summary>
/// HTTP protocol integration tests (read-only operations).
/// HTTP server provides read-only access to shared files.
/// </summary>
[Collection("Simulator")]
public class HttpProtocolTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public HttpProtocolTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Gets HTTP server configuration from connection info.
    /// </summary>
    private async Task<(string baseUrl, HttpClient client)> GetHttpConfigAsync()
    {
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var httpServer = connectionInfo.GetServer("HTTP");

        httpServer.Should().NotBeNull("HTTP server must be available in connection info");

        var baseUrl = $"http://{httpServer!.Host}:{httpServer.Port}";
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

        return (baseUrl, client);
    }

    [Fact]
    public async Task HTTP_Health_ReturnsSuccess()
    {
        // Arrange
        var (_, client) = await GetHttpConfigAsync();

        try
        {
            // Act
            var response = await client.GetAsync("/health");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue("HTTP health endpoint should return success");
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, "Health check should return 200 OK");
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task HTTP_List_ReturnsDirectoryListing()
    {
        // Arrange
        var (_, client) = await GetHttpConfigAsync();

        try
        {
            // Act
            var response = await client.GetAsync("/api/files/");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue("Directory listing endpoint should return success");

            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty("Directory listing should return content");
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task HTTP_Read_OutputDirectory_ReturnsContent()
    {
        // Arrange
        var (_, client) = await GetHttpConfigAsync();

        try
        {
            // Act
            var response = await client.GetAsync("/output/");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue("Output directory should be accessible");

            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty("Output directory should return content");
        }
        finally
        {
            client.Dispose();
        }
    }
}
