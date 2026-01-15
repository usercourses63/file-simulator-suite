using System.Diagnostics;
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

public class Program
{
    public static async Task Main(string[] args)
    {
        // Get the directory where the executable is located
        var basePath = AppContext.BaseDirectory;

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // Debug: Show which config is loaded
        AnsiConsole.MarkupLine($"[grey]Config loaded from: {basePath}[/]");
        AnsiConsole.MarkupLine($"[grey]HTTP BaseUrl: {config["FileSimulator:Http:BaseUrl"]}[/]");

        AnsiConsole.Write(new FigletText("File Simulator").Color(Color.Cyan1));
        AnsiConsole.Write(new Rule("[yellow]Protocol Test Suite[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var results = new List<TestResult>();
        var testContent = $"Test file created at {DateTime.UtcNow:O}\nThis is a test file for protocol validation.";
        var testFileName = $"test-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";

        // Run all protocol tests
        await AnsiConsole.Status()
            .StartAsync("Running protocol tests...", async ctx =>
            {
                ctx.Status("Testing FTP...");
                results.Add(await TestFtpAsync(config, testContent, testFileName));

                ctx.Status("Testing SFTP...");
                results.Add(await TestSftpAsync(config, testContent, testFileName));

                ctx.Status("Testing HTTP (Read)...");
                results.Add(await TestHttpReadAsync(config));

                ctx.Status("Testing WebDAV (Write)...");
                results.Add(await TestWebDavAsync(config, testContent, testFileName));

                ctx.Status("Testing S3/MinIO...");
                results.Add(await TestS3Async(config, testContent, testFileName));

                ctx.Status("Testing SMB...");
                results.Add(await TestSmbAsync(config, testContent, testFileName));

                ctx.Status("Testing NFS...");
                results.Add(await TestNfsAsync(config, testContent, testFileName));
            });

        // Display results table
        DisplayResults(results);

        // Display summary
        DisplaySummary(results);
    }

    static async Task<TestResult> TestFtpAsync(IConfiguration config, string content, string fileName)
    {
        var result = new TestResult { Protocol = "FTP" };
        var host = config["FileSimulator:Ftp:Host"] ?? "localhost";
        var port = int.Parse(config["FileSimulator:Ftp:Port"] ?? "30021");
        var username = config["FileSimulator:Ftp:Username"] ?? "ftpuser";
        var password = config["FileSimulator:Ftp:Password"] ?? "ftppass123";
        var basePath = config["FileSimulator:Ftp:BasePath"] ?? "/output";

        try
        {
            using var client = new AsyncFtpClient(host, username, password, port);
            client.Config.ConnectTimeout = 10000;

            // Connect
            var sw = Stopwatch.StartNew();
            await client.Connect();
            result.ConnectMs = sw.ElapsedMilliseconds;
            result.Connected = true;

            // Upload
            var remotePath = $"{basePath}/{fileName}";
            var bytes = Encoding.UTF8.GetBytes(content);
            sw.Restart();
            await using (var stream = new MemoryStream(bytes))
            {
                await client.UploadStream(stream, remotePath, FtpRemoteExists.Overwrite, true);
            }
            result.UploadMs = sw.ElapsedMilliseconds;
            result.UploadSuccess = true;

            // List/Discover
            sw.Restart();
            var items = await client.GetListing(basePath);
            result.ListMs = sw.ElapsedMilliseconds;
            result.ListSuccess = items.Any(i => i.Name == fileName);

            // Download/Read
            sw.Restart();
            await using (var stream = new MemoryStream())
            {
                await client.DownloadStream(stream, remotePath);
                stream.Position = 0;
                var downloaded = Encoding.UTF8.GetString(stream.ToArray());
                result.ReadSuccess = downloaded == content;
            }
            result.ReadMs = sw.ElapsedMilliseconds;

            // Delete
            sw.Restart();
            await client.DeleteFile(remotePath);
            result.DeleteMs = sw.ElapsedMilliseconds;
            result.DeleteSuccess = true;

            await client.Disconnect();
        }
        catch (Exception ex)
        {
            result.Error = ex.InnerException?.Message ?? ex.Message;
        }

        return result;
    }

    static async Task<TestResult> TestSftpAsync(IConfiguration config, string content, string fileName)
    {
        var result = new TestResult { Protocol = "SFTP" };
        var host = config["FileSimulator:Sftp:Host"] ?? "localhost";
        var port = int.Parse(config["FileSimulator:Sftp:Port"] ?? "30022");
        var username = config["FileSimulator:Sftp:Username"] ?? "sftpuser";
        var password = config["FileSimulator:Sftp:Password"] ?? "sftppass123";
        var basePath = config["FileSimulator:Sftp:BasePath"] ?? "/data/output";

        try
        {
            await Task.Run(() =>
            {
                using var client = new SftpClient(host, port, username, password);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);

                // Connect
                var sw = Stopwatch.StartNew();
                client.Connect();
                result.ConnectMs = sw.ElapsedMilliseconds;
                result.Connected = true;

                // Upload
                var remotePath = $"{basePath}/{fileName}";
                var bytes = Encoding.UTF8.GetBytes(content);
                sw.Restart();
                using (var stream = new MemoryStream(bytes))
                {
                    client.UploadFile(stream, remotePath, true);
                }
                result.UploadMs = sw.ElapsedMilliseconds;
                result.UploadSuccess = true;

                // List/Discover
                sw.Restart();
                var items = client.ListDirectory(basePath);
                result.ListMs = sw.ElapsedMilliseconds;
                result.ListSuccess = items.Any(i => i.Name == fileName);

                // Download/Read
                sw.Restart();
                using (var stream = new MemoryStream())
                {
                    client.DownloadFile(remotePath, stream);
                    stream.Position = 0;
                    var downloaded = Encoding.UTF8.GetString(stream.ToArray());
                    result.ReadSuccess = downloaded == content;
                }
                result.ReadMs = sw.ElapsedMilliseconds;

                // Delete
                sw.Restart();
                client.DeleteFile(remotePath);
                result.DeleteMs = sw.ElapsedMilliseconds;
                result.DeleteSuccess = true;

                client.Disconnect();
            });
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    static async Task<TestResult> TestHttpReadAsync(IConfiguration config)
    {
        var result = new TestResult { Protocol = "HTTP" };
        var baseUrl = config["FileSimulator:Http:BaseUrl"] ?? "http://localhost:30088";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            // Connect (health check)
            var sw = Stopwatch.StartNew();
            var healthResponse = await client.GetAsync($"{baseUrl}/health");
            result.ConnectMs = sw.ElapsedMilliseconds;
            result.Connected = healthResponse.IsSuccessStatusCode;

            // List (directory listing via JSON API - include trailing slash to avoid redirect)
            sw.Restart();
            var listResponse = await client.GetAsync($"{baseUrl}/api/files/");
            result.ListMs = sw.ElapsedMilliseconds;
            result.ListSuccess = listResponse.IsSuccessStatusCode;

            // Read (try to read any file in output)
            sw.Restart();
            var readResponse = await client.GetAsync($"{baseUrl}/output/");
            result.ReadMs = sw.ElapsedMilliseconds;
            result.ReadSuccess = readResponse.IsSuccessStatusCode;

            // HTTP is read-only, mark upload/delete as N/A
            result.UploadSuccess = null;
            result.DeleteSuccess = null;
        }
        catch (Exception ex)
        {
            result.Error = $"{ex.Message} (URL: {baseUrl})";
        }

        return result;
    }

    static async Task<TestResult> TestWebDavAsync(IConfiguration config, string content, string fileName)
    {
        var result = new TestResult { Protocol = "WebDAV" };
        var baseUrl = config["FileSimulator:WebDav:BaseUrl"] ?? "http://localhost:30089";
        var username = config["FileSimulator:WebDav:Username"] ?? "httpuser";
        var password = config["FileSimulator:WebDav:Password"] ?? "httppass123";
        var basePath = config["FileSimulator:WebDav:BasePath"] ?? "/output";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            // Connect (try to list root)
            var sw = Stopwatch.StartNew();
            var connectResponse = await client.GetAsync($"{baseUrl}/");
            result.ConnectMs = sw.ElapsedMilliseconds;
            result.Connected = connectResponse.IsSuccessStatusCode;

            // Upload (PUT)
            var remotePath = $"{baseUrl}{basePath}/{fileName}";
            sw.Restart();
            var uploadContent = new StringContent(content, Encoding.UTF8);
            var uploadResponse = await client.PutAsync(remotePath, uploadContent);
            result.UploadMs = sw.ElapsedMilliseconds;
            result.UploadSuccess = uploadResponse.IsSuccessStatusCode;

            // List
            sw.Restart();
            var listResponse = await client.GetAsync($"{baseUrl}{basePath}/");
            var listContent = await listResponse.Content.ReadAsStringAsync();
            result.ListMs = sw.ElapsedMilliseconds;
            result.ListSuccess = listContent.Contains(fileName);

            // Read
            sw.Restart();
            var readResponse = await client.GetAsync(remotePath);
            var readContent = await readResponse.Content.ReadAsStringAsync();
            result.ReadMs = sw.ElapsedMilliseconds;
            result.ReadSuccess = readContent == content;

            // Delete
            sw.Restart();
            var deleteResponse = await client.DeleteAsync(remotePath);
            result.DeleteMs = sw.ElapsedMilliseconds;
            result.DeleteSuccess = deleteResponse.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    static async Task<TestResult> TestS3Async(IConfiguration config, string content, string fileName)
    {
        var result = new TestResult { Protocol = "S3/MinIO" };
        var serviceUrl = config["FileSimulator:S3:ServiceUrl"] ?? "http://localhost:30900";
        var accessKey = config["FileSimulator:S3:AccessKey"] ?? "minioadmin";
        var secretKey = config["FileSimulator:S3:SecretKey"] ?? "minioadmin123";
        var bucketName = config["FileSimulator:S3:BucketName"] ?? "simulator";
        var basePath = config["FileSimulator:S3:BasePath"] ?? "output";

        try
        {
            var s3Config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true,
                Timeout = TimeSpan.FromSeconds(10)
            };

            using var client = new AmazonS3Client(accessKey, secretKey, s3Config);

            // Connect (list buckets)
            var sw = Stopwatch.StartNew();
            var buckets = await client.ListBucketsAsync();
            result.ConnectMs = sw.ElapsedMilliseconds;
            result.Connected = buckets.Buckets.Any(b => b.BucketName == bucketName);

            // Upload
            var key = $"{basePath}/{fileName}";
            sw.Restart();
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                ContentBody = content
            });
            result.UploadMs = sw.ElapsedMilliseconds;
            result.UploadSuccess = true;

            // List
            sw.Restart();
            var listResponse = await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = basePath
            });
            result.ListMs = sw.ElapsedMilliseconds;
            result.ListSuccess = listResponse.S3Objects.Any(o => o.Key == key);

            // Read
            sw.Restart();
            var getResponse = await client.GetObjectAsync(bucketName, key);
            using var reader = new StreamReader(getResponse.ResponseStream);
            var downloaded = await reader.ReadToEndAsync();
            result.ReadMs = sw.ElapsedMilliseconds;
            result.ReadSuccess = downloaded == content;

            // Delete
            sw.Restart();
            await client.DeleteObjectAsync(bucketName, key);
            result.DeleteMs = sw.ElapsedMilliseconds;
            result.DeleteSuccess = true;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    static async Task<TestResult> TestSmbAsync(IConfiguration config, string content, string fileName)
    {
        var result = new TestResult { Protocol = "SMB" };
        var host = config["FileSimulator:Smb:Host"] ?? "localhost";
        var port = int.Parse(config["FileSimulator:Smb:Port"] ?? "30445");
        var shareName = config["FileSimulator:Smb:ShareName"] ?? "simulator";
        var username = config["FileSimulator:Smb:Username"] ?? "smbuser";
        var password = config["FileSimulator:Smb:Password"] ?? "smbpass123";
        var basePath = config["FileSimulator:Smb:BasePath"] ?? "output";

        try
        {
            await Task.Run(() =>
            {
                // Resolve host to IP address
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

                var sw = Stopwatch.StartNew();

                // NOTE: SMB NTLM auth fails through TCP proxies (port-forward, minikube service).
                // Works with: direct NodePort access (Hyper-V/VirtualBox driver), in-cluster service DNS.
                // Docker driver on Windows cannot provide direct network access to Minikube.
                AnsiConsole.MarkupLine($"[grey]  Connecting to SMB server {targetAddress}:{port}...[/]");
                bool connected;
                SMB2Client? client = null;
                try
                {
                    // Try SMB2 first
                    client = new SMB2Client();
                    connected = client.Connect(targetAddress, SMBTransportType.DirectTCPTransport, port);
                    AnsiConsole.MarkupLine($"[grey]  SMB2 Connect returned: {connected}[/]");

                    if (!connected)
                    {
                        // Debug: try raw TCP connection to verify network
                        try
                        {
                            using var tcp = new System.Net.Sockets.TcpClient();
                            tcp.Connect(targetAddress, port);
                            AnsiConsole.MarkupLine($"[grey]  Raw TCP connection succeeded to {targetAddress}:{port}[/]");
                            tcp.Close();
                        }
                        catch (Exception tcpEx)
                        {
                            AnsiConsole.MarkupLine($"[red]  Raw TCP connection failed: {tcpEx.Message}[/]");
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Error = $"SMB connect exception: {ex.Message}";
                    AnsiConsole.MarkupLine($"[red]  Connect exception: {ex.Message}[/]");
                    return;
                }
                if (!connected)
                {
                    result.Error = $"Failed to connect to SMB server (no exception)";
                    return;
                }
                AnsiConsole.MarkupLine($"[grey]  Connected, attempting login...[/]");

                // Login with empty domain (standalone server mode)
                var status = client.Login(string.Empty, username, password, SMBLibrary.Client.AuthenticationMethod.NTLMv2);
                AnsiConsole.MarkupLine($"[grey]  Login result: {status}[/]");

                if (status != NTStatus.STATUS_SUCCESS)
                {
                    result.Error = $"SMB login failed: {status}";
                    client.Disconnect();
                    return;
                }

                result.ConnectMs = sw.ElapsedMilliseconds;
                result.Connected = true;

                // Access share
                var fileStore = client.TreeConnect(shareName, out status);
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    result.Error = $"Failed to connect to share: {status}";
                    client.Disconnect();
                    return;
                }

                var remotePath = $"{basePath}\\{fileName}";

                // Upload (Create and Write)
                sw.Restart();
                status = fileStore.CreateFile(
                    out var fileHandle,
                    out _,
                    remotePath,
                    AccessMask.GENERIC_WRITE | AccessMask.GENERIC_READ | AccessMask.DELETE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.None,
                    CreateDisposition.FILE_OVERWRITE_IF,
                    CreateOptions.FILE_NON_DIRECTORY_FILE,
                    null);

                if (status == NTStatus.STATUS_SUCCESS)
                {
                    var bytes = Encoding.UTF8.GetBytes(content);
                    fileStore.WriteFile(out _, fileHandle, 0, bytes);
                    fileStore.CloseFile(fileHandle);
                    result.UploadMs = sw.ElapsedMilliseconds;
                    result.UploadSuccess = true;
                }
                else
                {
                    result.Error = $"Upload failed: {status}";
                }

                // List
                sw.Restart();
                status = fileStore.CreateFile(
                    out var dirHandle,
                    out _,
                    basePath,
                    AccessMask.GENERIC_READ,
                    SMBLibrary.FileAttributes.Directory,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_DIRECTORY_FILE,
                    null);

                if (status == NTStatus.STATUS_SUCCESS)
                {
                    fileStore.QueryDirectory(out var files, dirHandle, "*", FileInformationClass.FileDirectoryInformation);
                    fileStore.CloseFile(dirHandle);
                    result.ListMs = sw.ElapsedMilliseconds;
                    result.ListSuccess = files?.Cast<FileDirectoryInformation>().Any(f => f.FileName == fileName) ?? false;
                }

                // Read
                sw.Restart();
                status = fileStore.CreateFile(
                    out fileHandle,
                    out _,
                    remotePath,
                    AccessMask.GENERIC_READ,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE,
                    null);

                if (status == NTStatus.STATUS_SUCCESS)
                {
                    fileStore.ReadFile(out var data, fileHandle, 0, 65536);
                    fileStore.CloseFile(fileHandle);
                    var downloaded = Encoding.UTF8.GetString(data);
                    result.ReadMs = sw.ElapsedMilliseconds;
                    result.ReadSuccess = downloaded == content;
                }

                // Delete
                sw.Restart();
                status = fileStore.CreateFile(
                    out fileHandle,
                    out _,
                    remotePath,
                    AccessMask.DELETE,
                    SMBLibrary.FileAttributes.Normal,
                    ShareAccess.Delete,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_DELETE_ON_CLOSE,
                    null);

                if (status == NTStatus.STATUS_SUCCESS)
                {
                    fileStore.CloseFile(fileHandle);
                    result.DeleteMs = sw.ElapsedMilliseconds;
                    result.DeleteSuccess = true;
                }

                fileStore.Disconnect();
                client.Disconnect();
            });
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    static async Task<TestResult> TestNfsAsync(IConfiguration config, string content, string fileName)
    {
        var result = new TestResult { Protocol = "NFS" };
        var host = config["FileSimulator:Nfs:Host"] ?? "localhost";
        var port = int.Parse(config["FileSimulator:Nfs:Port"] ?? "32149");
        var mountPath = config["FileSimulator:Nfs:MountPath"] ?? "/mnt/nfs";
        var basePath = config["FileSimulator:Nfs:BasePath"] ?? "output";

        try
        {
            await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();

                // Check if mount path exists (NFS requires pre-mounted filesystem)
                var fullBasePath = Path.Combine(mountPath, basePath);

                // For Windows, check if the mount path is available
                // NFS mount on Windows typically requires WSL or third-party NFS client
                if (!Directory.Exists(mountPath))
                {
                    // Try TCP connection to verify NFS server is accessible
                    try
                    {
                        using var tcp = new System.Net.Sockets.TcpClient();
                        tcp.Connect(host, port);
                        result.ConnectMs = sw.ElapsedMilliseconds;
                        result.Connected = true;
                        tcp.Close();

                        AnsiConsole.MarkupLine($"[grey]  NFS server {host}:{port} is reachable[/]");
                        AnsiConsole.MarkupLine($"[yellow]  NFS mount path '{mountPath}' not found[/]");
                        AnsiConsole.MarkupLine($"[yellow]  To test NFS operations, mount the NFS share:[/]");
                        AnsiConsole.MarkupLine($"[grey]    Linux: sudo mount -t nfs {host}:/data {mountPath}[/]");
                        AnsiConsole.MarkupLine($"[grey]    Windows WSL: sudo mount -t nfs {host}:/data {mountPath}[/]");

                        // Mark as N/A since we can't test file operations without mount
                        result.UploadSuccess = null;
                        result.ListSuccess = null;
                        result.ReadSuccess = null;
                        result.DeleteSuccess = null;
                    }
                    catch (Exception tcpEx)
                    {
                        result.Error = $"NFS server not reachable: {tcpEx.Message}";
                    }
                    return;
                }

                result.ConnectMs = sw.ElapsedMilliseconds;
                result.Connected = true;

                // Ensure base directory exists
                if (!Directory.Exists(fullBasePath))
                {
                    Directory.CreateDirectory(fullBasePath);
                }

                var remotePath = Path.Combine(fullBasePath, fileName);

                // Upload (Write file)
                sw.Restart();
                File.WriteAllText(remotePath, content);
                result.UploadMs = sw.ElapsedMilliseconds;
                result.UploadSuccess = true;

                // List
                sw.Restart();
                var files = Directory.GetFiles(fullBasePath);
                result.ListMs = sw.ElapsedMilliseconds;
                result.ListSuccess = files.Any(f => Path.GetFileName(f) == fileName);

                // Read
                sw.Restart();
                var downloaded = File.ReadAllText(remotePath);
                result.ReadMs = sw.ElapsedMilliseconds;
                result.ReadSuccess = downloaded == content;

                // Delete
                sw.Restart();
                File.Delete(remotePath);
                result.DeleteMs = sw.ElapsedMilliseconds;
                result.DeleteSuccess = !File.Exists(remotePath);
            });
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    static void DisplayResults(List<TestResult> results)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow]Test Results[/]").RuleStyle("grey"));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Protocol[/]")
            .AddColumn("[bold]Connect[/]")
            .AddColumn("[bold]Upload[/]")
            .AddColumn("[bold]List[/]")
            .AddColumn("[bold]Read[/]")
            .AddColumn("[bold]Delete[/]")
            .AddColumn("[bold]Total (ms)[/]");

        foreach (var r in results)
        {
            var connectStatus = r.Connected ? $"[green]{r.ConnectMs}ms[/]" : "[red]FAIL[/]";
            var uploadStatus = r.UploadSuccess switch
            {
                true => $"[green]{r.UploadMs}ms[/]",
                false => "[red]FAIL[/]",
                null => "[grey]N/A[/]"
            };
            var listStatus = r.ListSuccess switch
            {
                true => $"[green]{r.ListMs}ms[/]",
                false => "[red]FAIL[/]",
                null => "[grey]N/A[/]"
            };
            var readStatus = r.ReadSuccess switch
            {
                true => $"[green]{r.ReadMs}ms[/]",
                false => "[red]FAIL[/]",
                null => "[grey]N/A[/]"
            };
            var deleteStatus = r.DeleteSuccess switch
            {
                true => $"[green]{r.DeleteMs}ms[/]",
                false => "[red]FAIL[/]",
                null => "[grey]N/A[/]"
            };

            var total = r.ConnectMs + (r.UploadMs ?? 0) + (r.ListMs ?? 0) + (r.ReadMs ?? 0) + (r.DeleteMs ?? 0);

            table.AddRow(
                r.Protocol,
                connectStatus,
                uploadStatus,
                listStatus,
                readStatus,
                deleteStatus,
                $"[cyan]{total}[/]"
            );

            if (!string.IsNullOrEmpty(r.Error))
            {
                table.AddRow($"[red]  Error: {r.Error.Truncate(60)}[/]", "", "", "", "", "", "");
            }
        }

        AnsiConsole.Write(table);
    }

    static void DisplaySummary(List<TestResult> results)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow]Summary[/]").RuleStyle("grey"));

        var passed = results.Count(r => r.Connected && (r.UploadSuccess ?? true) && (r.ListSuccess ?? true) && (r.ReadSuccess ?? true) && (r.DeleteSuccess ?? true) && string.IsNullOrEmpty(r.Error));
        var failed = results.Count - passed;

        var panel = new Panel(
            new Markup($"[bold green]Passed:[/] {passed}  [bold red]Failed:[/] {failed}  [bold]Total:[/] {results.Count}"))
            .Header("[bold]Test Summary[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);

        if (failed > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Failed protocols:[/]");
            foreach (var r in results.Where(r => !string.IsNullOrEmpty(r.Error) || r.Connected == false))
            {
                AnsiConsole.MarkupLine($"  [red]- {r.Protocol}[/]: {r.Error ?? "Connection failed"}");
            }
        }

        AnsiConsole.WriteLine();
    }
}

public class TestResult
{
    public string Protocol { get; set; } = "";
    public bool Connected { get; set; }
    public long ConnectMs { get; set; }
    public bool? UploadSuccess { get; set; }
    public long? UploadMs { get; set; }
    public bool? ListSuccess { get; set; }
    public long? ListMs { get; set; }
    public bool? ReadSuccess { get; set; }
    public long? ReadMs { get; set; }
    public bool? DeleteSuccess { get; set; }
    public long? DeleteMs { get; set; }
    public string? Error { get; set; }
}

public static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
