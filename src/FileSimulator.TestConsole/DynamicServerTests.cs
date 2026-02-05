using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FluentFTP;
using Renci.SshNet;
using Spectre.Console;
using FileSimulator.TestConsole.Models;

namespace FileSimulator.TestConsole;

/// <summary>
/// Tests dynamic server creation, connectivity, and deletion via Control API.
/// </summary>
public static class DynamicServerTests
{
    /// <summary>
    /// Test dynamic server lifecycle for FTP, SFTP, and NAS.
    /// </summary>
    public static async Task TestDynamicServersAsync(string apiBaseUrl, bool includeFileOps = false)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow]Dynamic Server Tests[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var results = new List<DynamicServerTestResult>();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

        // Test FTP
        await AnsiConsole.Status()
            .StartAsync("Testing dynamic FTP server...", async ctx =>
            {
                results.Add(await TestDynamicServerAsync(client, "FTP", $"test-ftp-{timestamp}", apiBaseUrl, includeFileOps, ctx));
            });

        // Test SFTP
        await AnsiConsole.Status()
            .StartAsync("Testing dynamic SFTP server...", async ctx =>
            {
                results.Add(await TestDynamicServerAsync(client, "SFTP", $"test-sftp-{timestamp}", apiBaseUrl, includeFileOps, ctx));
            });

        // Test NAS
        await AnsiConsole.Status()
            .StartAsync("Testing dynamic NAS server...", async ctx =>
            {
                results.Add(await TestDynamicServerAsync(client, "NAS", $"test-nas-{timestamp}", apiBaseUrl, includeFileOps, ctx));
            });

        // Display results
        DisplayDynamicResults(results);
    }

    /// <summary>
    /// Test complete lifecycle of a single dynamic server.
    /// </summary>
    private static async Task<DynamicServerTestResult> TestDynamicServerAsync(
        HttpClient client,
        string protocol,
        string name,
        string apiBaseUrl,
        bool includeFileOps,
        StatusContext ctx)
    {
        var result = new DynamicServerTestResult
        {
            Protocol = protocol,
            ServerName = name
        };

        var totalSw = Stopwatch.StartNew();

        try
        {
            // Step 1: Create server
            ctx.Status($"Creating {protocol} server '{name}'...");
            var createSw = Stopwatch.StartNew();
            var serverInfo = await CreateDynamicServerAsync(client, protocol, name, apiBaseUrl);
            result.CreateMs = createSw.ElapsedMilliseconds;
            result.CreateSuccess = serverInfo != null;

            if (!result.CreateSuccess)
            {
                result.Error = "Failed to create server";
                return result;
            }

            // Step 2: Wait for ready
            ctx.Status($"Waiting for {protocol} server '{name}' to become ready...");
            var readySw = Stopwatch.StartNew();
            var readyInfo = await WaitForServerReadyAsync(client, name, apiBaseUrl, TimeSpan.FromSeconds(60), ctx);
            result.WaitForReadyMs = readySw.ElapsedMilliseconds;
            result.WaitForReadySuccess = readyInfo != null;

            if (!result.WaitForReadySuccess)
            {
                result.Error = "Server did not become ready within timeout";
                return result;
            }

            // Step 3: Test connectivity
            ctx.Status($"Testing {protocol} connectivity to '{name}'...");
            var connectSw = Stopwatch.StartNew();
            result.ConnectivitySuccess = await TestServerConnectivityAsync(
                protocol,
                readyInfo!.Host,
                readyInfo.Port,
                readyInfo.Username ?? "",
                readyInfo.Password ?? "");
            result.ConnectivityMs = connectSw.ElapsedMilliseconds;

            if (!result.ConnectivitySuccess)
            {
                result.Error = "Connectivity test failed";
                return result;
            }

            // Step 4: Optional file operations
            if (includeFileOps)
            {
                ctx.Status($"Testing {protocol} file operations on '{name}'...");
                var fileOpsSw = Stopwatch.StartNew();
                result.FileOperationSuccess = await TestFileOperationsAsync(
                    protocol,
                    readyInfo.Host,
                    readyInfo.Port,
                    readyInfo.Username ?? "",
                    readyInfo.Password ?? "");
                result.FileOperationMs = fileOpsSw.ElapsedMilliseconds;

                if (result.FileOperationSuccess == false)
                {
                    result.Error = "File operations test failed";
                }
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        finally
        {
            // Step 5: Always cleanup (delete server)
            try
            {
                ctx.Status($"Deleting {protocol} server '{name}'...");
                var deleteSw = Stopwatch.StartNew();
                result.DeleteSuccess = await DeleteDynamicServerAsync(client, name, apiBaseUrl);
                result.DeleteMs = deleteSw.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                result.Error = (result.Error ?? "") + $" | Cleanup failed: {ex.Message}";
            }

            result.TotalMs = totalSw.ElapsedMilliseconds;
        }

        return result;
    }

    /// <summary>
    /// Create a dynamic server via Control API.
    /// </summary>
    private static async Task<ServerCreationInfo?> CreateDynamicServerAsync(
        HttpClient client,
        string protocol,
        string name,
        string apiBaseUrl)
    {
        var url = $"{apiBaseUrl.TrimEnd('/')}/api/servers";

        object requestBody = protocol.ToUpperInvariant() switch
        {
            "FTP" => new
            {
                name,
                username = "testuser",
                password = "testpass123",
                directory = "input/test-dynamic"
            },
            "SFTP" => new
            {
                name,
                username = "testuser",
                password = "testpass123",
                directory = "input/test-dynamic"
            },
            "NAS" => new
            {
                name,
                directory = "input/test-dynamic",
                readOnly = false
            },
            _ => throw new ArgumentException($"Unsupported protocol: {protocol}")
        };

        var response = await client.PostAsJsonAsync(url, requestBody);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            AnsiConsole.MarkupLine($"[red]  Create failed: {response.StatusCode} - {errorContent}[/]");
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<ServerCreationResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result != null ? new ServerCreationInfo
        {
            Name = result.Name,
            Host = result.Host,
            Port = result.Port,
            Username = result.Username,
            Password = result.Password
        } : null;
    }

    /// <summary>
    /// Wait for server to become ready by polling status.
    /// </summary>
    private static async Task<ServerCreationInfo?> WaitForServerReadyAsync(
        HttpClient client,
        string name,
        string apiBaseUrl,
        TimeSpan timeout,
        StatusContext ctx)
    {
        var url = $"{apiBaseUrl.TrimEnd('/')}/api/servers/{name}";
        var maxAttempts = (int)(timeout.TotalSeconds / 2); // Poll every 2 seconds
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            attempt++;
            ctx.Status($"Waiting for server '{name}'... (attempt {attempt}/{maxAttempts})");

            try
            {
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var serverStatus = await response.Content.ReadFromJsonAsync<ServerStatusResponse>(
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (serverStatus != null &&
                        serverStatus.Status?.Equals("Running", StringComparison.OrdinalIgnoreCase) == true &&
                        serverStatus.PodReady)
                    {
                        AnsiConsole.MarkupLine($"[green]  Server '{name}' is ready[/]");
                        return new ServerCreationInfo
                        {
                            Name = serverStatus.Name,
                            Host = serverStatus.Host ?? "localhost",
                            Port = serverStatus.Port,
                            Username = serverStatus.Username,
                            Password = serverStatus.Password
                        };
                    }

                    AnsiConsole.MarkupLine($"[grey]  Status: {serverStatus?.Status}, Ready: {serverStatus?.PodReady}[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]  Poll attempt {attempt} failed: {ex.Message}[/]");
            }

            await Task.Delay(2000); // Wait 2 seconds before next poll
        }

        AnsiConsole.MarkupLine($"[red]  Timeout waiting for server '{name}'[/]");
        return null;
    }

    /// <summary>
    /// Test basic connectivity to a server.
    /// </summary>
    private static async Task<bool> TestServerConnectivityAsync(
        string protocol,
        string host,
        int port,
        string username,
        string password)
    {
        try
        {
            return protocol.ToUpperInvariant() switch
            {
                "FTP" => await TestFtpConnectivityAsync(host, port, username, password),
                "SFTP" => await TestSftpConnectivityAsync(host, port, username, password),
                "NAS" => await TestNasConnectivityAsync(host, port),
                _ => false
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]  Connectivity test failed: {ex.Message}[/]");
            return false;
        }
    }

    private static async Task<bool> TestFtpConnectivityAsync(string host, int port, string username, string password)
    {
        using var client = new AsyncFtpClient(host, username, password, port);
        client.Config.ConnectTimeout = 10000;

        await client.Connect();
        var isConnected = client.IsConnected;
        await client.Disconnect();

        AnsiConsole.MarkupLine($"[grey]  FTP connection: {(isConnected ? "SUCCESS" : "FAILED")}[/]");
        return isConnected;
    }

    private static async Task<bool> TestSftpConnectivityAsync(string host, int port, string username, string password)
    {
        return await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);
            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);

            client.Connect();
            var isConnected = client.IsConnected;
            client.Disconnect();

            AnsiConsole.MarkupLine($"[grey]  SFTP connection: {(isConnected ? "SUCCESS" : "FAILED")}[/]");
            return isConnected;
        });
    }

    private static async Task<bool> TestNasConnectivityAsync(string host, int port)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var tcp = new TcpClient();
                tcp.Connect(host, port);
                var isConnected = tcp.Connected;
                tcp.Close();

                AnsiConsole.MarkupLine($"[grey]  NAS TCP connection: {(isConnected ? "SUCCESS" : "FAILED")}[/]");
                AnsiConsole.MarkupLine($"[grey]  (NFS protocol testing requires mount - TCP-only test)[/]");
                return isConnected;
            }
            catch
            {
                AnsiConsole.MarkupLine($"[red]  NAS TCP connection: FAILED[/]");
                return false;
            }
        });
    }

    /// <summary>
    /// Test file operations (create, read, delete).
    /// </summary>
    private static async Task<bool> TestFileOperationsAsync(
        string protocol,
        string host,
        int port,
        string username,
        string password)
    {
        var testContent = $"Test file created at {DateTime.UtcNow:O}";
        var testFileName = $"test-{DateTime.UtcNow:yyyyMMddHHmmss}.txt";

        try
        {
            return protocol.ToUpperInvariant() switch
            {
                "FTP" => await TestFtpFileOperationsAsync(host, port, username, password, testContent, testFileName),
                "SFTP" => await TestSftpFileOperationsAsync(host, port, username, password, testContent, testFileName),
                "NAS" => false, // Requires mount
                _ => false
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]  File operations failed: {ex.Message}[/]");
            return false;
        }
    }

    private static async Task<bool> TestFtpFileOperationsAsync(
        string host,
        int port,
        string username,
        string password,
        string content,
        string fileName)
    {
        using var client = new AsyncFtpClient(host, username, password, port);
        client.Config.ConnectTimeout = 10000;

        await client.Connect();

        // Upload
        var remotePath = $"/data/{fileName}";
        var bytes = Encoding.UTF8.GetBytes(content);
        await using (var stream = new MemoryStream(bytes))
        {
            await client.UploadStream(stream, remotePath, FtpRemoteExists.Overwrite, true);
        }

        // Read
        await using (var stream = new MemoryStream())
        {
            await client.DownloadStream(stream, remotePath);
            stream.Position = 0;
            var downloaded = Encoding.UTF8.GetString(stream.ToArray());
            if (downloaded != content)
            {
                await client.Disconnect();
                return false;
            }
        }

        // Delete
        await client.DeleteFile(remotePath);
        await client.Disconnect();

        AnsiConsole.MarkupLine($"[grey]  FTP file operations: SUCCESS[/]");
        return true;
    }

    private static async Task<bool> TestSftpFileOperationsAsync(
        string host,
        int port,
        string username,
        string password,
        string content,
        string fileName)
    {
        return await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);
            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
            client.Connect();

            // Upload
            var remotePath = $"/data/{fileName}";
            var bytes = Encoding.UTF8.GetBytes(content);
            using (var stream = new MemoryStream(bytes))
            {
                client.UploadFile(stream, remotePath, true);
            }

            // Read
            using (var stream = new MemoryStream())
            {
                client.DownloadFile(remotePath, stream);
                stream.Position = 0;
                var downloaded = Encoding.UTF8.GetString(stream.ToArray());
                if (downloaded != content)
                {
                    client.Disconnect();
                    return false;
                }
            }

            // Delete
            client.DeleteFile(remotePath);
            client.Disconnect();

            AnsiConsole.MarkupLine($"[grey]  SFTP file operations: SUCCESS[/]");
            return true;
        });
    }

    /// <summary>
    /// Delete a dynamic server via Control API.
    /// </summary>
    private static async Task<bool> DeleteDynamicServerAsync(HttpClient client, string name, string apiBaseUrl)
    {
        var url = $"{apiBaseUrl.TrimEnd('/')}/api/servers/{name}";

        try
        {
            var response = await client.DeleteAsync(url);

            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                AnsiConsole.MarkupLine($"[green]  Deleted server '{name}'[/]");

                // Optional: Poll to verify deletion (max 30 seconds)
                for (int i = 0; i < 15; i++)
                {
                    await Task.Delay(2000);
                    var checkResponse = await client.GetAsync(url);
                    if (checkResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        AnsiConsole.MarkupLine($"[grey]  Verified deletion of '{name}'[/]");
                        return true;
                    }
                }

                return true; // Deletion request succeeded even if verification timed out
            }

            AnsiConsole.MarkupLine($"[red]  Delete failed: {response.StatusCode}[/]");
            return false;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]  Delete error: {ex.Message}[/]");
            return false;
        }
    }

    /// <summary>
    /// Display dynamic server test results.
    /// </summary>
    private static void DisplayDynamicResults(List<DynamicServerTestResult> results)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow]Dynamic Server Test Results[/]").RuleStyle("grey"));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Protocol[/]")
            .AddColumn("[bold]Name[/]")
            .AddColumn("[bold]Create[/]")
            .AddColumn("[bold]Ready[/]")
            .AddColumn("[bold]Connect[/]")
            .AddColumn("[bold]FileOps[/]")
            .AddColumn("[bold]Delete[/]")
            .AddColumn("[bold]Total (s)[/]");

        foreach (var r in results)
        {
            var createStatus = r.CreateSuccess ? $"[green]{r.CreateMs}ms[/]" : "[red]FAIL[/]";
            var readyStatus = r.WaitForReadySuccess ? $"[green]{r.WaitForReadyMs / 1000.0:F1}s[/]" : "[red]FAIL[/]";
            var connectStatus = r.ConnectivitySuccess ? $"[green]{r.ConnectivityMs}ms[/]" : "[red]FAIL[/]";
            var fileOpsStatus = r.FileOperationSuccess switch
            {
                true => $"[green]{r.FileOperationMs}ms[/]",
                false => "[red]FAIL[/]",
                null => "[grey]SKIP[/]"
            };
            var deleteStatus = r.DeleteSuccess ? $"[green]{r.DeleteMs}ms[/]" : "[red]FAIL[/]";
            var totalStatus = $"[cyan]{r.TotalMs / 1000.0:F1}s[/]";

            table.AddRow(
                r.Protocol,
                r.ServerName,
                createStatus,
                readyStatus,
                connectStatus,
                fileOpsStatus,
                deleteStatus,
                totalStatus
            );

            if (!string.IsNullOrEmpty(r.Error))
            {
                table.AddRow($"[red]  Error: {r.Error.Truncate(80)}[/]", "", "", "", "", "", "", "");
            }
        }

        AnsiConsole.Write(table);

        // Summary
        var successCount = results.Count(r => r.CreateSuccess && r.WaitForReadySuccess && r.ConnectivitySuccess && r.DeleteSuccess && string.IsNullOrEmpty(r.Error));
        var totalCount = results.Count;

        AnsiConsole.WriteLine();
        var summaryColor = successCount == totalCount ? "green" : "yellow";
        AnsiConsole.MarkupLine($"[{summaryColor}]Dynamic Server Tests: {successCount}/{totalCount} protocols tested successfully[/]");
        AnsiConsole.WriteLine();
    }

    // Helper classes for API responses

    private class ServerCreationInfo
    {
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    private class ServerCreationResponse
    {
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? ServiceName { get; set; }
        public string? DeploymentName { get; set; }
    }

    private class ServerStatusResponse
    {
        public string Name { get; set; } = "";
        public string? Protocol { get; set; }
        public string? Status { get; set; }
        public bool PodReady { get; set; }
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Directory { get; set; }
    }
}
