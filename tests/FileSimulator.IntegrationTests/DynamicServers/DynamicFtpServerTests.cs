using System.Text;
using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Support;
using FluentAssertions;
using FluentFTP;
using Xunit;

namespace FileSimulator.IntegrationTests.DynamicServers;

/// <summary>
/// Integration tests for dynamic FTP server lifecycle via Control API.
/// Tests: Create -> Wait for Ready -> Connect -> File Operations -> Delete.
/// </summary>
[Collection("Simulator")]
public class DynamicFtpServerTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public DynamicFtpServerTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FtpServer_Create_ReturnsServerInfo()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("ftp");

        try
        {
            // Act
            var response = await DynamicServerHelpers.CreateFtpServerAsync(_fixture.ApiClient, serverName);

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
    public async Task FtpServer_BecomesReady_WithinTimeout()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("ftp");

        try
        {
            // Act
            await DynamicServerHelpers.CreateFtpServerAsync(_fixture.ApiClient, serverName);
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
    public async Task FtpServer_AcceptsConnections_WhenReady()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("ftp");

        try
        {
            // Act - Create and wait for ready
            await DynamicServerHelpers.CreateFtpServerAsync(_fixture.ApiClient, serverName);
            var status = await DynamicServerHelpers.WaitForServerReadyAsync(
                _fixture.ApiClient,
                serverName,
                TimeSpan.FromSeconds(60));

            // Act - Connect to FTP server
            using var ftpClient = new AsyncFtpClient(
                status.Host,
                status.Username,
                status.Password,
                status.NodePort ?? status.Port);
            ftpClient.Config.ConnectTimeout = 10000;
            ftpClient.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;
            ftpClient.Config.DataConnectionConnectTimeout = 10000;

            await ftpClient.Connect();

            // Assert
            ftpClient.IsConnected.Should().BeTrue("FTP client should connect successfully");

            await ftpClient.Disconnect();
        }
        finally
        {
            // Cleanup
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
        }
    }

    [Fact(Skip = "Dynamic FTP servers don't have passive mode ports exposed via NodePort. File operations require passive mode data connections which are only configured for the static FTP server.")]
    public async Task FtpServer_FileOperations_WorkCorrectly()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("ftp");
        var testFileName = TestHelpers.GenerateUniqueFileName("ftp-test");
        var testContent = TestHelpers.CreateTestContent("FTP dynamic server test");
        var remotePath = $"/data/{testFileName}";

        try
        {
            // Act - Create server and wait for ready
            await DynamicServerHelpers.CreateFtpServerAsync(_fixture.ApiClient, serverName);
            var status = await DynamicServerHelpers.WaitForServerReadyAsync(
                _fixture.ApiClient,
                serverName,
                TimeSpan.FromSeconds(60));

            // Act - Connect
            using var ftpClient = new AsyncFtpClient(
                status.Host,
                status.Username,
                status.Password,
                status.NodePort ?? status.Port);
            ftpClient.Config.ConnectTimeout = 10000;
            ftpClient.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;
            ftpClient.Config.DataConnectionConnectTimeout = 10000;
            await ftpClient.Connect();

            // Act - Upload file
            var uploadBytes = Encoding.UTF8.GetBytes(testContent);
            await using (var uploadStream = new MemoryStream(uploadBytes))
            {
                var uploadResult = await ftpClient.UploadStream(
                    uploadStream,
                    remotePath,
                    FtpRemoteExists.Overwrite,
                    createRemoteDir: true);

                (uploadResult == FtpStatus.Success).Should().BeTrue("File upload should succeed");
            }

            // Act - List directory to verify file exists
            var files = await ftpClient.GetListing("/data");
            var uploadedFile = files.FirstOrDefault(f => f.Name == testFileName);

            // Assert - File exists
            uploadedFile.Should().NotBeNull($"File {testFileName} should exist after upload");
            uploadedFile!.Type.Should().Be(FtpObjectType.File, "Uploaded item should be a file");

            // Act - Download file
            await using (var downloadStream = new MemoryStream())
            {
                var downloadResult = await ftpClient.DownloadStream(downloadStream, remotePath);
                downloadResult.Should().BeTrue("File download should succeed");

                downloadStream.Position = 0;
                var downloadedContent = Encoding.UTF8.GetString(downloadStream.ToArray());

                // Assert - Content matches
                TestHelpers.AssertFileContent(
                    testContent,
                    downloadedContent,
                    "Downloaded content should match uploaded content");
            }

            // Act - Delete file
            await ftpClient.DeleteFile(remotePath);

            // Assert - File is deleted
            var filesAfterDelete = await ftpClient.GetListing("/data");
            var deletedFile = filesAfterDelete.FirstOrDefault(f => f.Name == testFileName);
            deletedFile.Should().BeNull($"File {testFileName} should be deleted");

            await ftpClient.Disconnect();
        }
        finally
        {
            // Cleanup
            await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
        }
    }

    [Fact]
    public async Task FtpServer_Delete_CleansUpResources()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("ftp");

        // Act - Create and wait for ready
        await DynamicServerHelpers.CreateFtpServerAsync(_fixture.ApiClient, serverName);
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

    [Fact(Skip = "Dynamic FTP servers don't have passive mode ports exposed via NodePort. File operations require passive mode data connections which are only configured for the static FTP server.")]
    public async Task FtpServer_CompleteLifecycle()
    {
        // Arrange
        var serverName = TestHelpers.GenerateUniqueServerName("ftp");
        var testFileName = TestHelpers.GenerateUniqueFileName("lifecycle");
        var testContent = "FTP lifecycle test content";
        var remotePath = $"/data/{testFileName}";

        try
        {
            // Step 1: Create server
            var createResponse = await DynamicServerHelpers.CreateFtpServerAsync(_fixture.ApiClient, serverName);
            createResponse.Should().NotBeNull("Server creation should succeed");

            // Step 2: Wait for ready
            var status = await DynamicServerHelpers.WaitForServerReadyAsync(
                _fixture.ApiClient,
                serverName,
                TimeSpan.FromSeconds(60));
            status.PodReady.Should().BeTrue("Server should become ready");

            // Step 3: Connect
            using var ftpClient = new AsyncFtpClient(
                status.Host,
                status.Username,
                status.Password,
                status.NodePort ?? status.Port);
            ftpClient.Config.ConnectTimeout = 10000;
            ftpClient.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;
            ftpClient.Config.DataConnectionConnectTimeout = 10000;
            await ftpClient.Connect();
            ftpClient.IsConnected.Should().BeTrue("Should connect to FTP server");

            // Step 4: Upload file
            var uploadBytes = Encoding.UTF8.GetBytes(testContent);
            await using (var uploadStream = new MemoryStream(uploadBytes))
            {
                var uploadResult = await ftpClient.UploadStream(
                    uploadStream,
                    remotePath,
                    FtpRemoteExists.Overwrite,
                    createRemoteDir: true);
                (uploadResult == FtpStatus.Success).Should().BeTrue("Upload should succeed");
            }

            // Step 5: Download file
            await using (var downloadStream = new MemoryStream())
            {
                var downloadResult = await ftpClient.DownloadStream(downloadStream, remotePath);
                downloadResult.Should().BeTrue("Download should succeed");

                downloadStream.Position = 0;
                var downloadedContent = Encoding.UTF8.GetString(downloadStream.ToArray());
                downloadedContent.Should().Be(testContent, "Downloaded content should match");
            }

            // Step 6: Delete file
            await ftpClient.DeleteFile(remotePath);
            var filesAfterDelete = await ftpClient.GetListing("/data");
            filesAfterDelete.Should().NotContain(f => f.Name == testFileName, "File should be deleted");

            await ftpClient.Disconnect();

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
