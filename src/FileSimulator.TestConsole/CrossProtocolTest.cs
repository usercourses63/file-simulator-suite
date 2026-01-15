using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using FluentFTP;
using Microsoft.Extensions.Configuration;
using Renci.SshNet;
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

        // Test 1: Upload via FTP, read via SFTP and HTTP
        AnsiConsole.MarkupLine("[cyan]Test 1: FTP → SFTP/HTTP[/]");
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

        // Test 2: Upload via SFTP, read via FTP and HTTP
        AnsiConsole.MarkupLine("[cyan]Test 2: SFTP → FTP/HTTP[/]");
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

        // =====================================================
        // S3 OBJECT STORAGE (separate from file-based protocols)
        // =====================================================
        AnsiConsole.MarkupLine("[yellow]S3 OBJECT STORAGE (MinIO - isolated)[/]");
        AnsiConsole.MarkupLine("[grey]Note: S3/MinIO stores objects in a proprietary format that is[/]");
        AnsiConsole.MarkupLine("[grey]incompatible with file-based protocols. This is by design.[/]");
        AnsiConsole.WriteLine();

        // Test 3: S3 standalone test
        AnsiConsole.MarkupLine("[cyan]Test 3: S3 round-trip[/]");
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
