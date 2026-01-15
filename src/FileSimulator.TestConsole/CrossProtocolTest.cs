using System.Net;
using System.Net.Sockets;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using FluentFTP;
using Microsoft.Extensions.Configuration;
using Renci.SshNet;
using SMBLibrary;
using SMBLibrary.Client;
using Spectre.Console;

namespace FileSimulator.TestConsole;

public static class CrossProtocolTest
{
    public static async Task RunAsync(IConfiguration config)
    {
        AnsiConsole.Write(new Rule("[yellow]Cross-Protocol File Sharing Test[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var testId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var results = new List<(string Source, string Target, bool Success, string Message)>();

        // =====================================================
        // FILE-BASED PROTOCOLS (FTP, SFTP, HTTP share storage)
        // =====================================================
        AnsiConsole.MarkupLine("[yellow]FILE-BASED PROTOCOLS (shared filesystem)[/]");
        AnsiConsole.WriteLine();

        // Check NFS availability once
        var nfsAvailable = IsNfsAvailable(config);
        if (!nfsAvailable)
        {
            AnsiConsole.WriteLine();
        }

        // Test 1: Upload via FTP, read via SFTP, HTTP, SMB, NFS
        AnsiConsole.MarkupLine($"[cyan]Test 1: FTP → SFTP/HTTP/SMB{(nfsAvailable ? "/NFS" : "")}[/]");
        var ftpContent = $"Cross-protocol test from FTP at {DateTime.UtcNow:O}";
        var ftpFileName = $"cross-test-ftp-{testId}.txt";

        try
        {
            // Upload via FTP
            await UploadViaFtpAsync(config, ftpContent, ftpFileName);
            AnsiConsole.MarkupLine($"  [green]✓[/] Uploaded via FTP: {ftpFileName}");

            // Wait for sync
            await Task.Delay(500);

            // Read via SFTP
            var sftpContent = await ReadViaSftpAsync(config, ftpFileName);
            var sftpMatch = sftpContent == ftpContent;
            results.Add(("FTP", "SFTP", sftpMatch, sftpMatch ? "Content matches" : $"Content mismatch"));
            AnsiConsole.MarkupLine($"  {(sftpMatch ? "[green]✓[/]" : "[red]✗[/]")} Read via SFTP: {(sftpMatch ? "matches" : "MISMATCH")}");

            // Read via HTTP
            var httpContent = await ReadViaHttpAsync(config, ftpFileName);
            var httpMatch = httpContent == ftpContent;
            results.Add(("FTP", "HTTP", httpMatch, httpMatch ? "Content matches" : $"Content mismatch"));
            AnsiConsole.MarkupLine($"  {(httpMatch ? "[green]✓[/]" : "[red]✗[/]")} Read via HTTP: {(httpMatch ? "matches" : "MISMATCH")}");

            // Read via SMB
            var smbContent = await ReadViaSmbAsync(config, ftpFileName);
            var smbMatch = smbContent == ftpContent;
            results.Add(("FTP", "SMB", smbMatch, smbMatch ? "Content matches" : $"Content mismatch"));
            AnsiConsole.MarkupLine($"  {(smbMatch ? "[green]✓[/]" : "[red]✗[/]")} Read via SMB: {(smbMatch ? "matches" : "MISMATCH")}");

            // Read via NFS (if available)
            if (nfsAvailable)
            {
                var nfsContent = await ReadViaNfsAsync(config, ftpFileName);
                var nfsMatch = nfsContent == ftpContent;
                results.Add(("FTP", "NFS", nfsMatch, nfsMatch ? "Content matches" : $"Content mismatch"));
                AnsiConsole.MarkupLine($"  {(nfsMatch ? "[green]✓[/]" : "[red]✗[/]")} Read via NFS: {(nfsMatch ? "matches" : "MISMATCH")}");
            }

            // Cleanup
            await DeleteViaFtpAsync(config, ftpFileName);
            AnsiConsole.MarkupLine($"  [grey]Cleaned up {ftpFileName}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]✗[/] Error: {ex.Message}");
            results.Add(("FTP", "ALL", false, ex.Message));
        }

        AnsiConsole.WriteLine();

        // Test 2: Upload via SFTP, read via FTP, HTTP, SMB, NFS
        AnsiConsole.MarkupLine($"[cyan]Test 2: SFTP → FTP/HTTP/SMB{(nfsAvailable ? "/NFS" : "")}[/]");
        var sftpUploadContent = $"Cross-protocol test from SFTP at {DateTime.UtcNow:O}";
        var sftpFileName = $"cross-test-sftp-{testId}.txt";

        try
        {
            // Upload via SFTP
            await UploadViaSftpAsync(config, sftpUploadContent, sftpFileName);
            AnsiConsole.MarkupLine($"  [green]✓[/] Uploaded via SFTP: {sftpFileName}");

            // Wait for sync
            await Task.Delay(500);

            // Read via FTP
            var ftpReadContent = await ReadViaFtpAsync(config, sftpFileName);
            var ftpMatch = ftpReadContent == sftpUploadContent;
            results.Add(("SFTP", "FTP", ftpMatch, ftpMatch ? "Content matches" : $"Content mismatch"));
            AnsiConsole.MarkupLine($"  {(ftpMatch ? "[green]✓[/]" : "[red]✗[/]")} Read via FTP: {(ftpMatch ? "matches" : "MISMATCH")}");

            // Read via HTTP
            var httpReadContent = await ReadViaHttpAsync(config, sftpFileName);
            var httpMatch = httpReadContent == sftpUploadContent;
            results.Add(("SFTP", "HTTP", httpMatch, httpMatch ? "Content matches" : $"Content mismatch"));
            AnsiConsole.MarkupLine($"  {(httpMatch ? "[green]✓[/]" : "[red]✗[/]")} Read via HTTP: {(httpMatch ? "matches" : "MISMATCH")}");

            // Read via SMB
            var smbReadContent = await ReadViaSmbAsync(config, sftpFileName);
            var smbMatch = smbReadContent == sftpUploadContent;
            results.Add(("SFTP", "SMB", smbMatch, smbMatch ? "Content matches" : $"Content mismatch"));
            AnsiConsole.MarkupLine($"  {(smbMatch ? "[green]✓[/]" : "[red]✗[/]")} Read via SMB: {(smbMatch ? "matches" : "MISMATCH")}");

            // Read via NFS (if available)
            if (nfsAvailable)
            {
                var nfsReadContent = await ReadViaNfsAsync(config, sftpFileName);
                var nfsMatch = nfsReadContent == sftpUploadContent;
                results.Add(("SFTP", "NFS", nfsMatch, nfsMatch ? "Content matches" : $"Content mismatch"));
                AnsiConsole.MarkupLine($"  {(nfsMatch ? "[green]✓[/]" : "[red]✗[/]")} Read via NFS: {(nfsMatch ? "matches" : "MISMATCH")}");
            }

            // Cleanup
            await DeleteViaSftpAsync(config, sftpFileName);
            AnsiConsole.MarkupLine($"  [grey]Cleaned up {sftpFileName}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]✗[/] Error: {ex.Message}");
            results.Add(("SFTP", "ALL", false, ex.Message));
        }

        AnsiConsole.WriteLine();

        // Test 3: Upload via SMB, read via FTP, SFTP, HTTP, NFS
        AnsiConsole.MarkupLine($"[cyan]Test 3: SMB → FTP/SFTP/HTTP{(nfsAvailable ? "/NFS" : "")}[/]");
        var smbUploadContent = $"Cross-protocol test from SMB at {DateTime.UtcNow:O}";
        var smbFileName = $"cross-test-smb-{testId}.txt";

        try
        {
            // Upload via SMB
            await UploadViaSmbAsync(config, smbUploadContent, smbFileName);
            AnsiConsole.MarkupLine($"  [green]✓[/] Uploaded via SMB: {smbFileName}");

            // Wait for sync
            await Task.Delay(500);

            // Read via FTP
            var ftpReadContent2 = await ReadViaFtpAsync(config, smbFileName);
            var ftpMatch2 = ftpReadContent2 == smbUploadContent;
            results.Add(("SMB", "FTP", ftpMatch2, ftpMatch2 ? "Content matches" : $"Content mismatch"));
            AnsiConsole.MarkupLine($"  {(ftpMatch2 ? "[green]✓[/]" : "[red]✗[/]")} Read via FTP: {(ftpMatch2 ? "matches" : "MISMATCH")}");

            // Read via SFTP
            var sftpReadContent2 = await ReadViaSftpAsync(config, smbFileName);
            var sftpMatch2 = sftpReadContent2 == smbUploadContent;
            results.Add(("SMB", "SFTP", sftpMatch2, sftpMatch2 ? "Content matches" : $"Content mismatch"));
            AnsiConsole.MarkupLine($"  {(sftpMatch2 ? "[green]✓[/]" : "[red]✗[/]")} Read via SFTP: {(sftpMatch2 ? "matches" : "MISMATCH")}");

            // Read via HTTP
            var httpReadContent2 = await ReadViaHttpAsync(config, smbFileName);
            var httpMatch2 = httpReadContent2 == smbUploadContent;
            results.Add(("SMB", "HTTP", httpMatch2, httpMatch2 ? "Content matches" : $"Content mismatch"));
            AnsiConsole.MarkupLine($"  {(httpMatch2 ? "[green]✓[/]" : "[red]✗[/]")} Read via HTTP: {(httpMatch2 ? "matches" : "MISMATCH")}");

            // Read via NFS (if available)
            if (nfsAvailable)
            {
                var nfsReadContent2 = await ReadViaNfsAsync(config, smbFileName);
                var nfsMatch2 = nfsReadContent2 == smbUploadContent;
                results.Add(("SMB", "NFS", nfsMatch2, nfsMatch2 ? "Content matches" : $"Content mismatch"));
                AnsiConsole.MarkupLine($"  {(nfsMatch2 ? "[green]✓[/]" : "[red]✗[/]")} Read via NFS: {(nfsMatch2 ? "matches" : "MISMATCH")}");
            }

            // Cleanup
            await DeleteViaSmbAsync(config, smbFileName);
            AnsiConsole.MarkupLine($"  [grey]Cleaned up {smbFileName}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]✗[/] Error: {ex.Message}");
            results.Add(("SMB", "ALL", false, ex.Message));
        }

        AnsiConsole.WriteLine();

        // Test 4: Upload via NFS, read via FTP, SFTP, HTTP, SMB (only if NFS available)
        if (nfsAvailable)
        {
            AnsiConsole.MarkupLine("[cyan]Test 4: NFS → FTP/SFTP/HTTP/SMB[/]");
            var nfsUploadContent = $"Cross-protocol test from NFS at {DateTime.UtcNow:O}";
            var nfsFileName = $"cross-test-nfs-{testId}.txt";

            try
            {
                // Upload via NFS
                await UploadViaNfsAsync(config, nfsUploadContent, nfsFileName);
                AnsiConsole.MarkupLine($"  [green]✓[/] Uploaded via NFS: {nfsFileName}");

                // Wait for sync
                await Task.Delay(500);

                // Read via FTP
                var ftpReadContent3 = await ReadViaFtpAsync(config, nfsFileName);
                var ftpMatch3 = ftpReadContent3 == nfsUploadContent;
                results.Add(("NFS", "FTP", ftpMatch3, ftpMatch3 ? "Content matches" : $"Content mismatch"));
                AnsiConsole.MarkupLine($"  {(ftpMatch3 ? "[green]✓[/]" : "[red]✗[/]")} Read via FTP: {(ftpMatch3 ? "matches" : "MISMATCH")}");

                // Read via SFTP
                var sftpReadContent3 = await ReadViaSftpAsync(config, nfsFileName);
                var sftpMatch3 = sftpReadContent3 == nfsUploadContent;
                results.Add(("NFS", "SFTP", sftpMatch3, sftpMatch3 ? "Content matches" : $"Content mismatch"));
                AnsiConsole.MarkupLine($"  {(sftpMatch3 ? "[green]✓[/]" : "[red]✗[/]")} Read via SFTP: {(sftpMatch3 ? "matches" : "MISMATCH")}");

                // Read via HTTP
                var httpReadContent3 = await ReadViaHttpAsync(config, nfsFileName);
                var httpMatch3 = httpReadContent3 == nfsUploadContent;
                results.Add(("NFS", "HTTP", httpMatch3, httpMatch3 ? "Content matches" : $"Content mismatch"));
                AnsiConsole.MarkupLine($"  {(httpMatch3 ? "[green]✓[/]" : "[red]✗[/]")} Read via HTTP: {(httpMatch3 ? "matches" : "MISMATCH")}");

                // Read via SMB
                var smbReadContent3 = await ReadViaSmbAsync(config, nfsFileName);
                var smbMatch3 = smbReadContent3 == nfsUploadContent;
                results.Add(("NFS", "SMB", smbMatch3, smbMatch3 ? "Content matches" : $"Content mismatch"));
                AnsiConsole.MarkupLine($"  {(smbMatch3 ? "[green]✓[/]" : "[red]✗[/]")} Read via SMB: {(smbMatch3 ? "matches" : "MISMATCH")}");

                // Cleanup
                await DeleteViaNfsAsync(config, nfsFileName);
                AnsiConsole.MarkupLine($"  [grey]Cleaned up {nfsFileName}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"  [red]✗[/] Error: {ex.Message}");
                results.Add(("NFS", "ALL", false, ex.Message));
            }

            AnsiConsole.WriteLine();
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]Test 4: NFS → ... (skipped - NFS not mounted)[/]");
            AnsiConsole.WriteLine();
        }

        // =====================================================
        // S3 OBJECT STORAGE (separate from file-based protocols)
        // =====================================================
        AnsiConsole.MarkupLine("[yellow]S3 OBJECT STORAGE (MinIO - isolated)[/]");
        AnsiConsole.MarkupLine("[grey]Note: S3/MinIO stores objects in a proprietary format that is[/]");
        AnsiConsole.MarkupLine("[grey]incompatible with file-based protocols. This is by design.[/]");
        AnsiConsole.WriteLine();

        // Test 5: S3 standalone test
        AnsiConsole.MarkupLine("[cyan]Test 5: S3 round-trip[/]");
        var s3Content = $"S3 test at {DateTime.UtcNow:O}";
        var s3FileName = $"s3-test-{testId}.txt";

        try
        {
            await UploadViaS3Async(config, s3Content, s3FileName);
            AnsiConsole.MarkupLine($"  [green]✓[/] Uploaded via S3: {s3FileName}");

            var s3ReadContent = await ReadViaS3Async(config, s3FileName);
            var s3Match = s3ReadContent == s3Content;
            results.Add(("S3", "S3", s3Match, s3Match ? "Round-trip successful" : "Content mismatch"));
            AnsiConsole.MarkupLine($"  {(s3Match ? "[green]✓[/]" : "[red]✗[/]")} Read via S3: {(s3Match ? "matches" : "MISMATCH")}");

            await DeleteViaS3Async(config, s3FileName);
            AnsiConsole.MarkupLine($"  [grey]Cleaned up {s3FileName}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]✗[/] Error: {ex.Message}");
            results.Add(("S3", "S3", false, ex.Message));
        }

        AnsiConsole.WriteLine();

        // Display summary
        DisplayCrossProtocolSummary(results);
    }

    #region FTP Operations

    private static async Task UploadViaFtpAsync(IConfiguration config, string content, string fileName)
    {
        var host = config["FileSimulator:Ftp:Host"] ?? "localhost";
        var port = int.Parse(config["FileSimulator:Ftp:Port"] ?? "30021");
        var username = config["FileSimulator:Ftp:Username"] ?? "ftpuser";
        var password = config["FileSimulator:Ftp:Password"] ?? "ftppass123";
        var basePath = config["FileSimulator:Ftp:BasePath"] ?? "/output";

        using var client = new AsyncFtpClient(host, username, password, port);
        await client.Connect();
        var bytes = Encoding.UTF8.GetBytes(content);
        await using var stream = new MemoryStream(bytes);
        await client.UploadStream(stream, $"{basePath}/{fileName}", FtpRemoteExists.Overwrite, true);
        await client.Disconnect();
    }

    private static async Task<string?> ReadViaFtpAsync(IConfiguration config, string fileName)
    {
        var host = config["FileSimulator:Ftp:Host"] ?? "localhost";
        var port = int.Parse(config["FileSimulator:Ftp:Port"] ?? "30021");
        var username = config["FileSimulator:Ftp:Username"] ?? "ftpuser";
        var password = config["FileSimulator:Ftp:Password"] ?? "ftppass123";
        var basePath = config["FileSimulator:Ftp:BasePath"] ?? "/output";

        using var client = new AsyncFtpClient(host, username, password, port);
        await client.Connect();
        await using var stream = new MemoryStream();
        await client.DownloadStream(stream, $"{basePath}/{fileName}");
        stream.Position = 0;
        var content = Encoding.UTF8.GetString(stream.ToArray());
        await client.Disconnect();
        return content;
    }

    private static async Task DeleteViaFtpAsync(IConfiguration config, string fileName)
    {
        var host = config["FileSimulator:Ftp:Host"] ?? "localhost";
        var port = int.Parse(config["FileSimulator:Ftp:Port"] ?? "30021");
        var username = config["FileSimulator:Ftp:Username"] ?? "ftpuser";
        var password = config["FileSimulator:Ftp:Password"] ?? "ftppass123";
        var basePath = config["FileSimulator:Ftp:BasePath"] ?? "/output";

        using var client = new AsyncFtpClient(host, username, password, port);
        await client.Connect();
        await client.DeleteFile($"{basePath}/{fileName}");
        await client.Disconnect();
    }

    #endregion

    #region SFTP Operations

    private static async Task UploadViaSftpAsync(IConfiguration config, string content, string fileName)
    {
        var host = config["FileSimulator:Sftp:Host"] ?? "localhost";
        var port = int.Parse(config["FileSimulator:Sftp:Port"] ?? "30022");
        var username = config["FileSimulator:Sftp:Username"] ?? "sftpuser";
        var password = config["FileSimulator:Sftp:Password"] ?? "sftppass123";
        var basePath = config["FileSimulator:Sftp:BasePath"] ?? "/data/output";

        await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);
            client.Connect();
            var bytes = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes);
            client.UploadFile(stream, $"{basePath}/{fileName}", true);
            client.Disconnect();
        });
    }

    private static async Task<string?> ReadViaSftpAsync(IConfiguration config, string fileName)
    {
        var host = config["FileSimulator:Sftp:Host"] ?? "localhost";
        var port = int.Parse(config["FileSimulator:Sftp:Port"] ?? "30022");
        var username = config["FileSimulator:Sftp:Username"] ?? "sftpuser";
        var password = config["FileSimulator:Sftp:Password"] ?? "sftppass123";
        var basePath = config["FileSimulator:Sftp:BasePath"] ?? "/data/output";

        return await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);
            client.Connect();
            using var stream = new MemoryStream();
            client.DownloadFile($"{basePath}/{fileName}", stream);
            stream.Position = 0;
            var content = Encoding.UTF8.GetString(stream.ToArray());
            client.Disconnect();
            return content;
        });
    }

    private static async Task DeleteViaSftpAsync(IConfiguration config, string fileName)
    {
        var host = config["FileSimulator:Sftp:Host"] ?? "localhost";
        var port = int.Parse(config["FileSimulator:Sftp:Port"] ?? "30022");
        var username = config["FileSimulator:Sftp:Username"] ?? "sftpuser";
        var password = config["FileSimulator:Sftp:Password"] ?? "sftppass123";
        var basePath = config["FileSimulator:Sftp:BasePath"] ?? "/data/output";

        await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);
            client.Connect();
            client.DeleteFile($"{basePath}/{fileName}");
            client.Disconnect();
        });
    }

    #endregion

    #region S3 Operations

    private static async Task UploadViaS3Async(IConfiguration config, string content, string fileName)
    {
        var serviceUrl = config["FileSimulator:S3:ServiceUrl"] ?? "http://localhost:30900";
        var accessKey = config["FileSimulator:S3:AccessKey"] ?? "minioadmin";
        var secretKey = config["FileSimulator:S3:SecretKey"] ?? "minioadmin123";
        var bucketName = config["FileSimulator:S3:BucketName"] ?? "output";
        var basePath = config["FileSimulator:S3:BasePath"] ?? "";

        var s3Config = new AmazonS3Config { ServiceURL = serviceUrl, ForcePathStyle = true };
        using var client = new AmazonS3Client(accessKey, secretKey, s3Config);

        var key = string.IsNullOrEmpty(basePath) ? fileName : $"{basePath}/{fileName}";
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            ContentBody = content
        });
    }

    private static async Task<string?> ReadViaS3Async(IConfiguration config, string fileName)
    {
        var serviceUrl = config["FileSimulator:S3:ServiceUrl"] ?? "http://localhost:30900";
        var accessKey = config["FileSimulator:S3:AccessKey"] ?? "minioadmin";
        var secretKey = config["FileSimulator:S3:SecretKey"] ?? "minioadmin123";
        var bucketName = config["FileSimulator:S3:BucketName"] ?? "output";
        var basePath = config["FileSimulator:S3:BasePath"] ?? "";

        var s3Config = new AmazonS3Config { ServiceURL = serviceUrl, ForcePathStyle = true };
        using var client = new AmazonS3Client(accessKey, secretKey, s3Config);

        var key = string.IsNullOrEmpty(basePath) ? fileName : $"{basePath}/{fileName}";
        var response = await client.GetObjectAsync(bucketName, key);
        using var reader = new StreamReader(response.ResponseStream);
        return await reader.ReadToEndAsync();
    }

    private static async Task DeleteViaS3Async(IConfiguration config, string fileName)
    {
        var serviceUrl = config["FileSimulator:S3:ServiceUrl"] ?? "http://localhost:30900";
        var accessKey = config["FileSimulator:S3:AccessKey"] ?? "minioadmin";
        var secretKey = config["FileSimulator:S3:SecretKey"] ?? "minioadmin123";
        var bucketName = config["FileSimulator:S3:BucketName"] ?? "output";
        var basePath = config["FileSimulator:S3:BasePath"] ?? "";

        var s3Config = new AmazonS3Config { ServiceURL = serviceUrl, ForcePathStyle = true };
        using var client = new AmazonS3Client(accessKey, secretKey, s3Config);

        var key = string.IsNullOrEmpty(basePath) ? fileName : $"{basePath}/{fileName}";
        await client.DeleteObjectAsync(bucketName, key);
    }

    #endregion

    #region HTTP Operations

    private static async Task<string?> ReadViaHttpAsync(IConfiguration config, string fileName)
    {
        var baseUrl = config["FileSimulator:Http:BaseUrl"] ?? "http://localhost:30088";
        var basePath = config["FileSimulator:Http:BasePath"] ?? "/output";

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var response = await client.GetAsync($"{baseUrl}{basePath}/{fileName}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    #endregion

    #region SMB Operations

    private static (SMB2Client client, ISMBFileStore fileStore) ConnectSmb(IConfiguration config)
    {
        var host = config["FileSimulator:Smb:Host"] ?? "localhost";
        var port = int.Parse(config["FileSimulator:Smb:Port"] ?? "445");
        var shareName = config["FileSimulator:Smb:ShareName"] ?? "simulator";
        var username = config["FileSimulator:Smb:Username"] ?? "smbuser";
        var password = config["FileSimulator:Smb:Password"] ?? "smbpass123";

        IPAddress targetAddress;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            targetAddress = IPAddress.Loopback;
        }
        else if (!IPAddress.TryParse(host, out targetAddress!))
        {
            var addresses = Dns.GetHostAddresses(host);
            targetAddress = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? throw new Exception($"Could not resolve host: {host}");
        }

        var client = new SMB2Client();
        var connected = client.Connect(targetAddress, SMBTransportType.DirectTCPTransport, port);
        if (!connected)
            throw new Exception($"Failed to connect to SMB server {host}:{port}");

        var status = client.Login(string.Empty, username, password, SMBLibrary.Client.AuthenticationMethod.NTLMv2);
        if (status != NTStatus.STATUS_SUCCESS)
        {
            client.Disconnect();
            throw new Exception($"SMB login failed: {status}");
        }

        var fileStore = client.TreeConnect(shareName, out status);
        if (status != NTStatus.STATUS_SUCCESS)
        {
            client.Disconnect();
            throw new Exception($"Failed to connect to share: {status}");
        }

        return (client, fileStore);
    }

    private static async Task UploadViaSmbAsync(IConfiguration config, string content, string fileName)
    {
        var basePath = config["FileSimulator:Smb:BasePath"] ?? "output";

        await Task.Run(() =>
        {
            var (client, fileStore) = ConnectSmb(config);
            try
            {
                var remotePath = $"{basePath}\\{fileName}";
                var status = fileStore.CreateFile(
                    out var fileHandle,
                    out _,
                    remotePath,
                    AccessMask.GENERIC_WRITE | AccessMask.GENERIC_READ,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.None,
                    CreateDisposition.FILE_OVERWRITE_IF,
                    CreateOptions.FILE_NON_DIRECTORY_FILE,
                    null);

                if (status != NTStatus.STATUS_SUCCESS)
                    throw new Exception($"SMB create file failed: {status}");

                var bytes = Encoding.UTF8.GetBytes(content);
                fileStore.WriteFile(out _, fileHandle, 0, bytes);
                fileStore.CloseFile(fileHandle);
            }
            finally
            {
                fileStore.Disconnect();
                client.Disconnect();
            }
        });
    }

    private static async Task<string?> ReadViaSmbAsync(IConfiguration config, string fileName)
    {
        var basePath = config["FileSimulator:Smb:BasePath"] ?? "output";

        return await Task.Run(() =>
        {
            var (client, fileStore) = ConnectSmb(config);
            try
            {
                var remotePath = $"{basePath}\\{fileName}";
                var status = fileStore.CreateFile(
                    out var fileHandle,
                    out _,
                    remotePath,
                    AccessMask.GENERIC_READ,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE,
                    null);

                if (status != NTStatus.STATUS_SUCCESS)
                    throw new Exception($"SMB open file failed: {status}");

                fileStore.ReadFile(out var data, fileHandle, 0, 65536);
                fileStore.CloseFile(fileHandle);
                return Encoding.UTF8.GetString(data);
            }
            finally
            {
                fileStore.Disconnect();
                client.Disconnect();
            }
        });
    }

    private static async Task DeleteViaSmbAsync(IConfiguration config, string fileName)
    {
        var basePath = config["FileSimulator:Smb:BasePath"] ?? "output";

        await Task.Run(() =>
        {
            var (client, fileStore) = ConnectSmb(config);
            try
            {
                var remotePath = $"{basePath}\\{fileName}";
                var status = fileStore.CreateFile(
                    out var fileHandle,
                    out _,
                    remotePath,
                    AccessMask.DELETE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.Delete,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_DELETE_ON_CLOSE,
                    null);

                if (status != NTStatus.STATUS_SUCCESS)
                    throw new Exception($"SMB delete file failed: {status}");

                fileStore.CloseFile(fileHandle);
            }
            finally
            {
                fileStore.Disconnect();
                client.Disconnect();
            }
        });
    }

    #endregion

    #region NFS Operations

    private static bool _nfsAvailabilityChecked;
    private static bool _nfsAvailable;

    private static bool IsNfsAvailable(IConfiguration config)
    {
        if (_nfsAvailabilityChecked)
            return _nfsAvailable;

        var mountPath = config["FileSimulator:Nfs:MountPath"] ?? "/mnt/nfs";
        var basePath = config["FileSimulator:Nfs:BasePath"] ?? "output";
        var fullPath = Path.Combine(mountPath, basePath);
        var host = config["FileSimulator:Nfs:Host"] ?? "localhost";

        // On Windows, Unix-style paths like /mnt/nfs get converted to C:\mnt\nfs
        // which is not a real NFS mount. Only consider NFS available if:
        // 1. We're on Linux/Mac with a real mount, OR
        // 2. On Windows, the path is a UNC path (\\server\share) or starts with a drive letter
        //    that represents a mapped network drive
        var isUnixStylePath = mountPath.StartsWith("/") && !mountPath.StartsWith("//");
        var isWindows = OperatingSystem.IsWindows();

        if (isWindows && isUnixStylePath)
        {
            // Unix-style paths on Windows are not real NFS mounts
            _nfsAvailable = false;
            AnsiConsole.MarkupLine($"  [yellow]NFS: Unix-style path '{mountPath}' not valid on Windows[/]");
            AnsiConsole.MarkupLine($"  [grey]To enable NFS testing on Windows:[/]");
            AnsiConsole.MarkupLine($"  [grey]  1. Mount NFS share: mount -o anon \\\\{host}\\data Z:[/]");
            AnsiConsole.MarkupLine($"  [grey]  2. Update appsettings.json: \"MountPath\": \"Z:\\\\\"[/]");
        }
        else
        {
            // Check if the directory exists (for real mounts or Windows UNC/drive paths)
            _nfsAvailable = Directory.Exists(fullPath);

            if (!_nfsAvailable)
            {
                AnsiConsole.MarkupLine($"  [yellow]NFS mount not available at '{mountPath}'[/]");
                AnsiConsole.MarkupLine($"  [grey]To enable NFS testing, mount the NFS share:[/]");
                AnsiConsole.MarkupLine($"  [grey]  Linux/WSL: sudo mount -t nfs {host}:/data {mountPath}[/]");
            }
        }

        _nfsAvailabilityChecked = true;
        return _nfsAvailable;
    }

    private static string GetNfsFilePath(IConfiguration config, string fileName)
    {
        var mountPath = config["FileSimulator:Nfs:MountPath"] ?? "/mnt/nfs";
        var basePath = config["FileSimulator:Nfs:BasePath"] ?? "output";
        return Path.Combine(mountPath, basePath, fileName);
    }

    private static async Task UploadViaNfsAsync(IConfiguration config, string content, string fileName)
    {
        if (!IsNfsAvailable(config))
            throw new InvalidOperationException("NFS mount not available");

        var filePath = GetNfsFilePath(config, fileName);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(filePath, content);
    }

    private static async Task<string?> ReadViaNfsAsync(IConfiguration config, string fileName)
    {
        if (!IsNfsAvailable(config))
            throw new InvalidOperationException("NFS mount not available");

        var filePath = GetNfsFilePath(config, fileName);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"NFS file not found: {filePath}");
        return await File.ReadAllTextAsync(filePath);
    }

    private static Task DeleteViaNfsAsync(IConfiguration config, string fileName)
    {
        if (!IsNfsAvailable(config))
            return Task.CompletedTask;

        var filePath = GetNfsFilePath(config, fileName);
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    #endregion

    private static void DisplayCrossProtocolSummary(List<(string Source, string Target, bool Success, string Message)> results)
    {
        AnsiConsole.Write(new Rule("[yellow]Cross-Protocol Summary[/]").RuleStyle("grey"));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Source[/]")
            .AddColumn("[bold]Target[/]")
            .AddColumn("[bold]Result[/]")
            .AddColumn("[bold]Message[/]");

        foreach (var (source, target, success, message) in results)
        {
            table.AddRow(
                source,
                target,
                success ? "[green]PASS[/]" : "[red]FAIL[/]",
                message.Length > 40 ? message[..40] + "..." : message
            );
        }

        AnsiConsole.Write(table);

        var passed = results.Count(r => r.Success);
        var failed = results.Count - passed;

        AnsiConsole.WriteLine();
        var panel = new Panel(
            new Markup($"[bold green]Passed:[/] {passed}  [bold red]Failed:[/] {failed}  [bold]Total:[/] {results.Count}"))
            .Header("[bold]Summary[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);

        if (passed == results.Count)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓ All protocols share the same storage (PVC)![/]");
            AnsiConsole.MarkupLine("[grey]  Files uploaded via any protocol are visible via all other protocols.[/]");
        }
    }
}
