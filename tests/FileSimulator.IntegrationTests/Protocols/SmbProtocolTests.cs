using System.Net;
using System.Net.Sockets;
using System.Text;
using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Support;
using FluentAssertions;
using SMBLibrary;
using SMBLibrary.Client;
using Xunit;
using SmbFileAttributes = SMBLibrary.FileAttributes;

namespace FileSimulator.IntegrationTests.Protocols;

/// <summary>
/// Integration tests for SMB protocol.
/// SMB requires 'minikube tunnel' running in Administrator terminal to work on Windows.
/// Tests are automatically skipped with clear instructions if tunnel is not running.
/// </summary>
[Collection("Simulator")]
public class SmbProtocolTests
{
    private readonly SimulatorCollectionFixture _fixture;
    private readonly bool _smbAccessible;
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _shareName;
    private readonly string _basePath;

    public SmbProtocolTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;

        // Get SMB server configuration from connection-info API
        var connectionInfo = _fixture.GetConnectionInfoAsync().GetAwaiter().GetResult();
        var smbServer = connectionInfo.GetServer("SMB")
            ?? throw new InvalidOperationException("SMB server not found in connection-info");

        _host = smbServer.Host;
        _port = smbServer.Port;
        _username = smbServer.Credentials.Username;
        _password = smbServer.Credentials.Password;

        // Parse connection string for share name and path
        // Expected format: \\host\share\path or smb://host/share/path
        _shareName = "simulator"; // Default from deployment
        _basePath = "output"; // Default from deployment

        // Check if SMB is accessible (requires minikube tunnel)
        _smbAccessible = PlatformHelpers.IsSmbAccessibleAsync(_host, _port).GetAwaiter().GetResult();

        Console.WriteLine($"[SmbProtocolTests] Host: {_host}:{_port}, Accessible: {_smbAccessible}");
    }

    [Fact]
    public async Task SMB_CanConnect()
    {
        // Skip if tunnel not running
        if (!_smbAccessible)
        {
            // Use xUnit 2's Skip.If pattern
            var skipMessage = PlatformHelpers.GetSkipMessage("SMB",
                "requires 'minikube tunnel' running in Administrator terminal");
            Console.WriteLine($"[SKIP] {skipMessage}");
            return;
        }

        // Arrange & Act
        SMB2Client? client = null;
        try
        {
            client = CreateSmbClient();
            var connected = ConnectToSmb(client);

            // Assert
            connected.Should().BeTrue("SMB server should accept connections");
        }
        finally
        {
            client?.Disconnect();
        }
    }

    [Fact]
    public async Task SMB_TreeConnect_AccessesShare()
    {
        if (!_smbAccessible)
        {
            // Use xUnit 2's Skip.If pattern
            var skipMessage = PlatformHelpers.GetSkipMessage("SMB",
                "requires 'minikube tunnel' running in Administrator terminal");
            Console.WriteLine($"[SKIP] {skipMessage}");
            return;
        }

        // Arrange
        SMB2Client? client = null;
        ISMBFileStore? fileStore = null;

        try
        {
            client = CreateSmbClient();
            fileStore = LoginAndConnect(client, _shareName);

            // Assert
            fileStore.Should().NotBeNull("Should be able to connect to SMB share");
        }
        finally
        {
            fileStore?.Disconnect();
            client?.Disconnect();
        }
    }

    [Fact]
    public async Task SMB_Upload_CreatesFile()
    {
        if (!_smbAccessible)
        {
            // Use xUnit 2's Skip.If pattern
            var skipMessage = PlatformHelpers.GetSkipMessage("SMB",
                "requires 'minikube tunnel' running in Administrator terminal");
            Console.WriteLine($"[SKIP] {skipMessage}");
            return;
        }

        // Arrange
        var fileName = TestHelpers.GenerateUniqueFileName("smb-upload");
        var testContent = TestHelpers.CreateTestContent("SMB upload test");
        SMB2Client? client = null;
        ISMBFileStore? fileStore = null;

        try
        {
            client = CreateSmbClient();
            fileStore = LoginAndConnect(client, _shareName);

            var filePath = $"{_basePath}\\{fileName}";

            // Act - Create and write file
            var createStatus = fileStore.CreateFile(
                out var fileHandle,
                out var fileStatus,
                filePath,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                SmbFileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_CREATE,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                null);

            createStatus.Should().Be(NTStatus.STATUS_SUCCESS, "File creation should succeed");

            var contentBytes = Encoding.UTF8.GetBytes(testContent);
            var writeStatus = fileStore.WriteFile(out var bytesWritten, fileHandle, 0, contentBytes);

            writeStatus.Should().Be(NTStatus.STATUS_SUCCESS, "File write should succeed");
            bytesWritten.Should().Be(contentBytes.Length, "All bytes should be written");

            var closeStatus = fileStore.CloseFile(fileHandle);
            closeStatus.Should().Be(NTStatus.STATUS_SUCCESS, "File close should succeed");

            // Verify file exists by listing directory
            var queryStatus = fileStore.CreateFile(
                out var dirHandle,
                out _,
                _basePath,
                AccessMask.GENERIC_READ,
                SmbFileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            queryStatus.Should().Be(NTStatus.STATUS_SUCCESS, "Should open directory");

            fileStore.QueryDirectory(
                out var fileList,
                dirHandle,
                fileName,
                FileInformationClass.FileDirectoryInformation);

            fileStore.CloseFile(dirHandle);

            // Assert
            fileList.Should().NotBeNull("Directory listing should succeed");
            var files = fileList.Cast<FileDirectoryInformation>().ToList();
            files.Should().Contain(f => f.FileName == fileName,
                "Uploaded file should appear in directory listing");
        }
        finally
        {
            // Cleanup
            try
            {
                if (fileStore != null)
                {
                    var filePath = $"{_basePath}\\{fileName}";
                    fileStore.CreateFile(
                        out var deleteHandle,
                        out _,
                        filePath,
                        AccessMask.GENERIC_WRITE | AccessMask.DELETE,
                        SmbFileAttributes.Normal,
                        ShareAccess.None,
                        CreateDisposition.FILE_OPEN,
                        CreateOptions.FILE_DELETE_ON_CLOSE,
                        null);
                    fileStore.CloseFile(deleteHandle);
                }
            }
            catch { /* Cleanup best effort */ }

            fileStore?.Disconnect();
            client?.Disconnect();
        }
    }

    [Fact]
    public async Task SMB_Download_ReturnsContent()
    {
        if (!_smbAccessible)
        {
            // Use xUnit 2's Skip.If pattern
            var skipMessage = PlatformHelpers.GetSkipMessage("SMB",
                "requires 'minikube tunnel' running in Administrator terminal");
            Console.WriteLine($"[SKIP] {skipMessage}");
            return;
        }

        // Arrange
        var fileName = TestHelpers.GenerateUniqueFileName("smb-download");
        var testContent = TestHelpers.CreateTestContent("SMB download test");
        SMB2Client? client = null;
        ISMBFileStore? fileStore = null;

        try
        {
            client = CreateSmbClient();
            fileStore = LoginAndConnect(client, _shareName);

            var filePath = $"{_basePath}\\{fileName}";

            // Upload test file
            var createStatus = fileStore.CreateFile(
                out var writeHandle,
                out _,
                filePath,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                SmbFileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_CREATE,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                null);

            createStatus.Should().Be(NTStatus.STATUS_SUCCESS);

            var contentBytes = Encoding.UTF8.GetBytes(testContent);
            fileStore.WriteFile(out _, writeHandle, 0, contentBytes);
            fileStore.CloseFile(writeHandle);

            // Act - Read file back
            var openStatus = fileStore.CreateFile(
                out var readHandle,
                out _,
                filePath,
                AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                SmbFileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                null);

            openStatus.Should().Be(NTStatus.STATUS_SUCCESS, "File should exist and be readable");

            var readStatus = fileStore.ReadFile(out var readBytes, readHandle, 0, contentBytes.Length);
            fileStore.CloseFile(readHandle);

            // Assert
            readStatus.Should().Be(NTStatus.STATUS_SUCCESS, "File read should succeed");
            var actualContent = Encoding.UTF8.GetString(readBytes);
            actualContent.Should().Be(testContent, "Downloaded content should match uploaded content");
        }
        finally
        {
            // Cleanup
            try
            {
                if (fileStore != null)
                {
                    var filePath = $"{_basePath}\\{fileName}";
                    fileStore.CreateFile(
                        out var deleteHandle,
                        out _,
                        filePath,
                        AccessMask.GENERIC_WRITE | AccessMask.DELETE,
                        SmbFileAttributes.Normal,
                        ShareAccess.None,
                        CreateDisposition.FILE_OPEN,
                        CreateOptions.FILE_DELETE_ON_CLOSE,
                        null);
                    fileStore.CloseFile(deleteHandle);
                }
            }
            catch { /* Cleanup best effort */ }

            fileStore?.Disconnect();
            client?.Disconnect();
        }
    }

    [Fact]
    public async Task SMB_List_ReturnsUploadedFile()
    {
        if (!_smbAccessible)
        {
            // Use xUnit 2's Skip.If pattern
            var skipMessage = PlatformHelpers.GetSkipMessage("SMB",
                "requires 'minikube tunnel' running in Administrator terminal");
            Console.WriteLine($"[SKIP] {skipMessage}");
            return;
        }

        // Arrange
        var fileName = TestHelpers.GenerateUniqueFileName("smb-list");
        var testContent = TestHelpers.CreateTestContent("SMB list test");
        SMB2Client? client = null;
        ISMBFileStore? fileStore = null;

        try
        {
            client = CreateSmbClient();
            fileStore = LoginAndConnect(client, _shareName);

            var filePath = $"{_basePath}\\{fileName}";

            // Upload test file
            fileStore.CreateFile(
                out var writeHandle,
                out _,
                filePath,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                SmbFileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_CREATE,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                null);

            var contentBytes = Encoding.UTF8.GetBytes(testContent);
            fileStore.WriteFile(out _, writeHandle, 0, contentBytes);
            fileStore.CloseFile(writeHandle);

            // Act - List directory
            var queryStatus = fileStore.CreateFile(
                out var dirHandle,
                out _,
                _basePath,
                AccessMask.GENERIC_READ,
                SmbFileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            queryStatus.Should().Be(NTStatus.STATUS_SUCCESS);

            fileStore.QueryDirectory(
                out var fileList,
                dirHandle,
                "*",
                FileInformationClass.FileDirectoryInformation);

            fileStore.CloseFile(dirHandle);

            // Assert
            fileList.Should().NotBeNull("Directory listing should succeed");
            var files = fileList.Cast<FileDirectoryInformation>().ToList();
            files.Should().Contain(f => f.FileName == fileName,
                "Uploaded file should appear in directory listing");
        }
        finally
        {
            // Cleanup
            try
            {
                if (fileStore != null)
                {
                    var filePath = $"{_basePath}\\{fileName}";
                    fileStore.CreateFile(
                        out var deleteHandle,
                        out _,
                        filePath,
                        AccessMask.GENERIC_WRITE | AccessMask.DELETE,
                        SmbFileAttributes.Normal,
                        ShareAccess.None,
                        CreateDisposition.FILE_OPEN,
                        CreateOptions.FILE_DELETE_ON_CLOSE,
                        null);
                    fileStore.CloseFile(deleteHandle);
                }
            }
            catch { /* Cleanup best effort */ }

            fileStore?.Disconnect();
            client?.Disconnect();
        }
    }

    [Fact]
    public async Task SMB_Delete_RemovesFile()
    {
        if (!_smbAccessible)
        {
            // Use xUnit 2's Skip.If pattern
            var skipMessage = PlatformHelpers.GetSkipMessage("SMB",
                "requires 'minikube tunnel' running in Administrator terminal");
            Console.WriteLine($"[SKIP] {skipMessage}");
            return;
        }

        // Arrange
        var fileName = TestHelpers.GenerateUniqueFileName("smb-delete");
        var testContent = TestHelpers.CreateTestContent("SMB delete test");
        SMB2Client? client = null;
        ISMBFileStore? fileStore = null;

        try
        {
            client = CreateSmbClient();
            fileStore = LoginAndConnect(client, _shareName);

            var filePath = $"{_basePath}\\{fileName}";

            // Upload test file
            fileStore.CreateFile(
                out var writeHandle,
                out _,
                filePath,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                SmbFileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_CREATE,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                null);

            var contentBytes = Encoding.UTF8.GetBytes(testContent);
            fileStore.WriteFile(out _, writeHandle, 0, contentBytes);
            fileStore.CloseFile(writeHandle);

            // Act - Delete file using DELETE_ON_CLOSE
            var deleteStatus = fileStore.CreateFile(
                out var deleteHandle,
                out _,
                filePath,
                AccessMask.GENERIC_WRITE | AccessMask.DELETE,
                SmbFileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DELETE_ON_CLOSE,
                null);

            deleteStatus.Should().Be(NTStatus.STATUS_SUCCESS, "Should be able to open file for deletion");

            fileStore.CloseFile(deleteHandle);

            // Verify file is deleted by attempting to open it
            var verifyStatus = fileStore.CreateFile(
                out var verifyHandle,
                out _,
                filePath,
                AccessMask.GENERIC_READ,
                SmbFileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);

            // Assert
            verifyStatus.Should().Be(NTStatus.STATUS_OBJECT_NAME_NOT_FOUND,
                "Deleted file should not exist");
        }
        finally
        {
            fileStore?.Disconnect();
            client?.Disconnect();
        }
    }

    [Fact]
    public async Task SMB_FullCycle_CRUD()
    {
        if (!_smbAccessible)
        {
            // Use xUnit 2's Skip.If pattern
            var skipMessage = PlatformHelpers.GetSkipMessage("SMB",
                "requires 'minikube tunnel' running in Administrator terminal");
            Console.WriteLine($"[SKIP] {skipMessage}");
            return;
        }

        // Arrange
        var fileName = TestHelpers.GenerateUniqueFileName("smb-fullcycle");
        var testContent = TestHelpers.CreateTestContent("SMB full cycle test");
        SMB2Client? client = null;
        ISMBFileStore? fileStore = null;

        try
        {
            client = CreateSmbClient();
            fileStore = LoginAndConnect(client, _shareName);

            var filePath = $"{_basePath}\\{fileName}";

            // Create
            var createStatus = fileStore.CreateFile(
                out var writeHandle,
                out _,
                filePath,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                SmbFileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_CREATE,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                null);
            createStatus.Should().Be(NTStatus.STATUS_SUCCESS, "Create should succeed");

            // Write
            var contentBytes = Encoding.UTF8.GetBytes(testContent);
            var writeStatus = fileStore.WriteFile(out var bytesWritten, writeHandle, 0, contentBytes);
            writeStatus.Should().Be(NTStatus.STATUS_SUCCESS, "Write should succeed");
            bytesWritten.Should().Be(contentBytes.Length, "All bytes should be written");
            fileStore.CloseFile(writeHandle);

            // Read
            fileStore.CreateFile(
                out var readHandle,
                out _,
                filePath,
                AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                SmbFileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                null);
            var readStatus = fileStore.ReadFile(out var readBytes, readHandle, 0, contentBytes.Length);
            readStatus.Should().Be(NTStatus.STATUS_SUCCESS, "Read should succeed");
            var actualContent = Encoding.UTF8.GetString(readBytes);
            actualContent.Should().Be(testContent, "Content should match");
            fileStore.CloseFile(readHandle);

            // Delete
            var deleteStatus = fileStore.CreateFile(
                out var deleteHandle,
                out _,
                filePath,
                AccessMask.GENERIC_WRITE | AccessMask.DELETE,
                SmbFileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DELETE_ON_CLOSE,
                null);
            deleteStatus.Should().Be(NTStatus.STATUS_SUCCESS, "Delete should succeed");
            fileStore.CloseFile(deleteHandle);

            // Verify deleted
            var verifyStatus = fileStore.CreateFile(
                out _,
                out _,
                filePath,
                AccessMask.GENERIC_READ,
                SmbFileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);
            verifyStatus.Should().Be(NTStatus.STATUS_OBJECT_NAME_NOT_FOUND,
                "File should be deleted");
        }
        finally
        {
            fileStore?.Disconnect();
            client?.Disconnect();
        }
    }

    #region Helper Methods

    private SMB2Client CreateSmbClient()
    {
        return new SMB2Client();
    }

    private bool ConnectToSmb(SMB2Client client)
    {
        // Resolve host to IP address
        IPAddress targetAddress;
        if (_host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            targetAddress = IPAddress.Loopback;
        }
        else if (!IPAddress.TryParse(_host, out targetAddress!))
        {
            var addresses = Dns.GetHostAddresses(_host);
            targetAddress = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? throw new Exception($"Could not resolve host: {_host}");
        }

        return client.Connect(targetAddress, SMBTransportType.DirectTCPTransport, _port);
    }

    private ISMBFileStore LoginAndConnect(SMB2Client client, string shareName)
    {
        var connected = ConnectToSmb(client);
        if (!connected)
        {
            throw new Exception($"Failed to connect to SMB server at {_host}:{_port}");
        }

        var loginStatus = client.Login(string.Empty, _username, _password);
        if (loginStatus != NTStatus.STATUS_SUCCESS)
        {
            throw new Exception($"SMB login failed with status: {loginStatus}");
        }

        var fileStore = client.TreeConnect(shareName, out var treeStatus);
        if (treeStatus != NTStatus.STATUS_SUCCESS)
        {
            throw new Exception($"SMB tree connect to share '{shareName}' failed with status: {treeStatus}");
        }

        return fileStore;
    }

    #endregion
}
