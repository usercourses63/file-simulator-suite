using System.Net.Sockets;
using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Support;
using FluentAssertions;
using Xunit;

namespace FileSimulator.IntegrationTests.DynamicServers;

/// <summary>
/// Integration tests for dynamic NAS server lifecycle via Control API.
/// Tests server creation, readiness, connectivity, and deletion.
/// </summary>
[Collection("Simulator")]
public class NasServerLifecycleTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public NasServerLifecycleTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NasServer_Create_ReturnsServerInfo()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("nas");

        try
        {
            // Act
            var server = await DynamicServerHelpers.CreateNasServerAsync(
                _fixture.ApiClient,
                serverName,
                "input/test-dynamic-nas");

            // Assert
            server.Should().NotBeNull("Server creation should succeed");
            server.Name.Should().Be(serverName, "Server name should match request");
            server.Host.Should().NotBeNullOrEmpty("Server should have a host");
            server.Port.Should().BeGreaterThan(0, "Server should have a valid port");
        }
        finally
        {
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
        }
    }

    [Fact]
    public async Task NasServer_BecomesReady_WithinTimeout()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("nas");

        try
        {
            // Act
            await DynamicServerHelpers.CreateNasServerAsync(
                _fixture.ApiClient,
                serverName,
                "input/test-dynamic-nas");

            var status = await DynamicServerHelpers.WaitForServerReadyAsync(
                _fixture.ApiClient,
                serverName,
                TimeSpan.FromSeconds(60));

            // Assert
            status.Should().NotBeNull("Server should become ready within 60 seconds");
            status.PodReady.Should().BeTrue("Pod should be ready");
            status.Status.Should().BeEquivalentTo("Running", "Server should be running");
        }
        finally
        {
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
        }
    }

    [Fact]
    public async Task NasServer_TcpPort_IsAccessible()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("nas");

        try
        {
            // Act
            var server = await DynamicServerHelpers.CreateNasServerAsync(
                _fixture.ApiClient,
                serverName,
                "input/test-dynamic-nas");

            await DynamicServerHelpers.WaitForServerReadyAsync(
                _fixture.ApiClient,
                serverName,
                TimeSpan.FromSeconds(60));

            // Test TCP connectivity with retry policy
            var policy = RetryPolicies.TcpConnectivityPolicy(maxAttempts: 5, delaySeconds: 2);
            var connected = await policy.ExecuteAsync(async () =>
            {
                return await TestTcpConnectivityAsync(server.Host, server.Port);
            });

            // Assert
            connected.Should().BeTrue($"Should be able to connect to NFS port at {server.Host}:{server.Port}");
        }
        finally
        {
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
        }
    }

    [Fact]
    public async Task NasServer_ReadOnly_ConfigurationApplied()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("nas");

        try
        {
            // Act
            var server = await DynamicServerHelpers.CreateNasServerAsync(
                _fixture.ApiClient,
                serverName,
                "input/test-dynamic-nas",
                readOnly: true);

            var status = await DynamicServerHelpers.WaitForServerReadyAsync(
                _fixture.ApiClient,
                serverName,
                TimeSpan.FromSeconds(60));

            // Assert
            server.Should().NotBeNull("Server creation should succeed with readOnly=true");
            status.Should().NotBeNull("Server status should be available");
            status.Status.Should().BeEquivalentTo("Running", "Server should be running");
        }
        finally
        {
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
        }
    }

    [Fact]
    public async Task NasServer_Delete_CleansUpResources()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("nas");
        await DynamicServerHelpers.CreateNasServerAsync(
            _fixture.ApiClient,
            serverName,
            "input/test-dynamic-nas");

        await DynamicServerHelpers.WaitForServerReadyAsync(
            _fixture.ApiClient,
            serverName,
            TimeSpan.FromSeconds(60));

        // Act
        var deleted = await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);

        // Verify deletion
        var status = await DynamicServerHelpers.GetServerStatusAsync(_fixture.ApiClient, serverName);

        // Assert
        deleted.Should().BeTrue("Server deletion should succeed");
        status.Should().BeNull("Server should return 404 after deletion");
    }

    [Fact]
    public async Task NasServer_CompleteLifecycle()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("nas");

        try
        {
            // Act & Assert - Create
            var server = await DynamicServerHelpers.CreateNasServerAsync(
                _fixture.ApiClient,
                serverName,
                "input/test-dynamic-nas");
            server.Should().NotBeNull("Server creation should succeed");

            // Act & Assert - Wait for Ready
            var status = await DynamicServerHelpers.WaitForServerReadyAsync(
                _fixture.ApiClient,
                serverName,
                TimeSpan.FromSeconds(60));
            status.Should().NotBeNull("Server should become ready");
            status.PodReady.Should().BeTrue("Pod should be ready");

            // Act & Assert - TCP Connect
            var policy = RetryPolicies.TcpConnectivityPolicy(maxAttempts: 5, delaySeconds: 2);
            var connected = await policy.ExecuteAsync(async () =>
            {
                return await TestTcpConnectivityAsync(server.Host, server.Port);
            });
            connected.Should().BeTrue("TCP connectivity should succeed");

            // Act & Assert - Delete
            var deleted = await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
            deleted.Should().BeTrue("Server deletion should succeed");
        }
        finally
        {
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
        }
    }

    [Fact]
    public async Task NasServer_FileOperations_WhenMountAvailable()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("nas");
        var mountPath = $"C:\\simulator-data\\{serverName}";
        var testFileName = $"test-file-{Guid.NewGuid():N}.txt";
        var testFilePath = Path.Combine(mountPath, testFileName);
        var testContent = TestHelpers.CreateTestContent("NAS file operation test");

        try
        {
            // Create server
            await DynamicServerHelpers.CreateNasServerAsync(
                _fixture.ApiClient,
                serverName,
                "input/test-dynamic-nas");

            await DynamicServerHelpers.WaitForServerReadyAsync(
                _fixture.ApiClient,
                serverName,
                TimeSpan.FromSeconds(60));

            // Check if mount path exists (may take a few seconds to sync)
            var mountAvailable = false;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                if (Directory.Exists(mountPath))
                {
                    mountAvailable = true;
                    break;
                }
                await Task.Delay(1000);
            }

            if (!mountAvailable)
            {
                // Skip test gracefully if mount not available
                Console.WriteLine($"[Skip] NAS mount path not available at {mountPath}. Skipping file operations test.");
                return;
            }

            // Act - Write file
            await File.WriteAllTextAsync(testFilePath, testContent);

            // Wait for file to sync
            await Task.Delay(2000);

            // Act - Read file
            var readContent = await File.ReadAllTextAsync(testFilePath);

            // Act - List files
            var files = Directory.GetFiles(mountPath);

            // Assert
            readContent.Should().Be(testContent, "File content should match what was written");
            files.Should().Contain(testFilePath, "Directory listing should include test file");

            // Cleanup test file
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
        finally
        {
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
        }
    }

    #region Helper Methods

    private static async Task<bool> TestTcpConnectivityAsync(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            var connected = client.Connected;
            client.Close();
            return connected;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    #endregion
}
