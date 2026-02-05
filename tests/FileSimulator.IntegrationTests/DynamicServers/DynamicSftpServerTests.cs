using System.Text;
using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Support;
using FluentAssertions;
using Renci.SshNet;
using Xunit;

namespace FileSimulator.IntegrationTests.DynamicServers;

/// <summary>
/// Integration tests for dynamic SFTP server lifecycle via Control API.
/// Tests: Create -> Wait for Ready -> Connect -> File Operations -> Delete.
/// </summary>
[Collection("Simulator")]
public class DynamicSftpServerTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public DynamicSftpServerTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SftpServer_Create_ReturnsServerInfo()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("sftp");

        try
        {
            // Act
            var response = await DynamicServerHelpers.CreateSftpServerAsync(_fixture.ApiClient, serverName);

            // Assert
            response.Should().NotBeNull("Server creation should return response");
            response.Name.Should().Be(serverName, "Server name should match request");
            response.ServiceName.Should().NotBeNullOrEmpty("Service name should be provided");
            response.NodePort.Should().BeGreaterThan(0, "NodePort should be assigned");
            response.IsDynamic.Should().BeTrue("Server should be marked as dynamic");
        }
        finally
        {
            // Cleanup
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
        }
    }

    [Fact]
    public async Task SftpServer_BecomesReady_WithinTimeout()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("sftp");

        try
        {
            // Act
            await DynamicServerHelpers.CreateSftpServerAsync(_fixture.ApiClient, serverName);
            var status = await DynamicServerHelpers.WaitForServerReadyAsync(
                _fixture.ApiClient,
                serverName,
                TimeSpan.FromSeconds(60));

            // Assert
            status.Should().NotBeNull("Server should become ready");
            status.PodReady.Should().BeTrue("Pod should be ready");
            status.Status.Should().Be("Running", "Status should be Running");
            status.Name.Should().Be(serverName, "Status should contain server name");
        }
        finally
        {
            // Cleanup
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
        }
    }

    [Fact]
    public async Task SftpServer_AcceptsConnections_WhenReady()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("sftp");

        try
        {
            // Act - Create and wait for ready
            await DynamicServerHelpers.CreateSftpServerAsync(_fixture.ApiClient, serverName);
            var status = await DynamicServerHelpers.WaitForServerReadyAsync(
                _fixture.ApiClient,
                serverName,
                TimeSpan.FromSeconds(60));

            // Act - Connect to SFTP server (SSH.NET is synchronous, wrap in Task.Run)
            var isConnected = await Task.Run(() =>
            {
                using var sftpClient = new SftpClient(
                    status.Host,
                    status.NodePort ?? status.Port,
                    status.Username,
                    status.Password);
                sftpClient.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);

                sftpClient.Connect();
                var connected = sftpClient.IsConnected;
                sftpClient.Disconnect();

                return connected;
            });

            // Assert
            isConnected.Should().BeTrue("SFTP client should connect successfully");
        }
        finally
        {
            // Cleanup
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
        }
    }

    [Fact]
    public async Task SftpServer_FileOperations_WorkCorrectly()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("sftp");
        var testFileName = TestHelpers.GenerateUniqueFileName("sftp-test");
        var testContent = TestHelpers.CreateTestContent("SFTP dynamic server test");
        var remotePath = $"/data/{testFileName}"; // SFTP uses /data as root

        try
        {
            // Act - Create server and wait for ready
            await DynamicServerHelpers.CreateSftpServerAsync(_fixture.ApiClient, serverName);
            var status = await DynamicServerHelpers.WaitForServerReadyAsync(
                _fixture.ApiClient,
                serverName,
                TimeSpan.FromSeconds(60));

            // Act - File operations (SSH.NET is synchronous, wrap in Task.Run)
            await Task.Run(() =>
            {
                using var sftpClient = new SftpClient(
                    status.Host,
                    status.NodePort ?? status.Port,
                    status.Username,
                    status.Password);
                sftpClient.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);

                sftpClient.Connect();

                // Upload file
                var uploadBytes = Encoding.UTF8.GetBytes(testContent);
                using (var uploadStream = new MemoryStream(uploadBytes))
                {
                    sftpClient.UploadFile(uploadStream, remotePath, canOverride: true);
                }

                // Verify file exists by listing directory
                var files = sftpClient.ListDirectory("/data");
                var uploadedFile = files.FirstOrDefault(f => f.Name == testFileName);

                // Assert - File exists
                uploadedFile.Should().NotBeNull($"File {testFileName} should exist after upload");
                uploadedFile!.IsRegularFile.Should().BeTrue("Uploaded item should be a file");

                // Download file
                using (var downloadStream = new MemoryStream())
                {
                    sftpClient.DownloadFile(remotePath, downloadStream);
                    downloadStream.Position = 0;
                    var downloadedContent = Encoding.UTF8.GetString(downloadStream.ToArray());

                    // Assert - Content matches
                    TestHelpers.AssertFileContent(
                        testContent,
                        downloadedContent,
                        "Downloaded content should match uploaded content");
                }

                // Delete file
                sftpClient.DeleteFile(remotePath);

                // Verify file is deleted
                var filesAfterDelete = sftpClient.ListDirectory("/data");
                var deletedFile = filesAfterDelete.FirstOrDefault(f => f.Name == testFileName);
                deletedFile.Should().BeNull($"File {testFileName} should be deleted");

                sftpClient.Disconnect();
            });
        }
        finally
        {
            // Cleanup
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
        }
    }

    [Fact]
    public async Task SftpServer_Delete_CleansUpResources()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("sftp");

        // Act - Create and wait for ready
        await DynamicServerHelpers.CreateSftpServerAsync(_fixture.ApiClient, serverName);
        await DynamicServerHelpers.WaitForServerReadyAsync(
            _fixture.ApiClient,
            serverName,
            TimeSpan.FromSeconds(60));

        // Act - Delete server
        var deleteResult = await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);

        // Assert - Delete succeeded
        deleteResult.Should().BeTrue("Server deletion should succeed");

        // Act - Verify server is gone by polling status
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        ServerStatusResponse? status = null;

        while (DateTime.UtcNow < deadline)
        {
            status = await DynamicServerHelpers.GetServerStatusAsync(_fixture.ApiClient, serverName);
            if (status == null)
            {
                break; // Server successfully deleted (returns 404)
            }
            await Task.Delay(2000);
        }

        // Assert - Server is truly gone
        status.Should().BeNull("Server should return 404 after deletion");
    }

    [Fact]
    public async Task SftpServer_CompleteLifecycle()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("sftp");
        var testFileName = TestHelpers.GenerateUniqueFileName("lifecycle");
        var testContent = "SFTP lifecycle test content";
        var remotePath = $"/data/{testFileName}";

        try
        {
            // Step 1: Create server
            var createResponse = await DynamicServerHelpers.CreateSftpServerAsync(_fixture.ApiClient, serverName);
            createResponse.Should().NotBeNull("Server creation should succeed");

            // Step 2: Wait for ready
            var status = await DynamicServerHelpers.WaitForServerReadyAsync(
                _fixture.ApiClient,
                serverName,
                TimeSpan.FromSeconds(60));
            status.PodReady.Should().BeTrue("Server should become ready");

            // Step 3-6: Connect, Upload, Download, Delete file (SSH.NET is synchronous)
            await Task.Run(() =>
            {
                using var sftpClient = new SftpClient(
                    status.Host,
                    status.NodePort ?? status.Port,
                    status.Username,
                    status.Password);
                sftpClient.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);

                // Step 3: Connect
                sftpClient.Connect();
                sftpClient.IsConnected.Should().BeTrue("Should connect to SFTP server");

                // Step 4: Upload file
                var uploadBytes = Encoding.UTF8.GetBytes(testContent);
                using (var uploadStream = new MemoryStream(uploadBytes))
                {
                    sftpClient.UploadFile(uploadStream, remotePath, canOverride: true);
                }

                var files = sftpClient.ListDirectory("/data");
                files.Should().Contain(f => f.Name == testFileName, "File should be uploaded");

                // Step 5: Download file
                using (var downloadStream = new MemoryStream())
                {
                    sftpClient.DownloadFile(remotePath, downloadStream);
                    downloadStream.Position = 0;
                    var downloadedContent = Encoding.UTF8.GetString(downloadStream.ToArray());
                    downloadedContent.Should().Be(testContent, "Downloaded content should match");
                }

                // Step 6: Delete file
                sftpClient.DeleteFile(remotePath);
                var filesAfterDelete = sftpClient.ListDirectory("/data");
                filesAfterDelete.Should().NotContain(f => f.Name == testFileName, "File should be deleted");

                sftpClient.Disconnect();
            });

            // Step 7: Delete server
            var deleteResult = await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
            deleteResult.Should().BeTrue("Server deletion should succeed");

            // All steps completed successfully
            Console.WriteLine($"[Test] Complete lifecycle test passed for {serverName}");
        }
        catch
        {
            // Cleanup on failure
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
            throw;
        }
    }
}
