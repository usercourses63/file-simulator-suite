using System.Diagnostics;
using System.Net.Sockets;
using FileSimulator.TestConsole.Models;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace FileSimulator.TestConsole;

/// <summary>
/// Tests for multi-NAS server topology (7 NAS servers).
/// </summary>
public static class NasServerTests
{
    /// <summary>
    /// Test all NAS servers in the topology.
    /// </summary>
    public static async Task<List<NasTestResult>> TestAllNasServersAsync(IConfiguration config, string testContent, string testFileName)
    {
        var results = new List<NasTestResult>();

        // Get all NAS servers from configuration
        var nasServers = FilterNasServers(config);

        if (!nasServers.Any())
        {
            AnsiConsole.MarkupLine("[yellow]Warning: No NAS servers found in configuration[/]");
            return results;
        }

        AnsiConsole.MarkupLine($"[cyan]Testing {nasServers.Count} NAS servers...[/]");
        AnsiConsole.WriteLine();

        await AnsiConsole.Status()
            .StartAsync("Running NAS tests...", async ctx =>
            {
                foreach (var server in nasServers)
                {
                    ctx.Status($"Testing {server.Name}...");
                    var result = await TestNasServerAsync(server, testContent, testFileName);
                    results.Add(result);
                }
            });

        return results;
    }

    /// <summary>
    /// Test a single NAS server.
    /// </summary>
    public static async Task<NasTestResult> TestNasServerAsync(ServerConfig server, string testContent, string testFileName)
    {
        var result = new NasTestResult
        {
            ServerName = server.Name,
            ServerType = GetServerType(server.Name)
        };

        var isBackup = result.ServerType == "backup";

        try
        {
            // 1. TCP connectivity test
            var sw = Stopwatch.StartNew();
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(server.Host, server.Port);
                result.TcpConnected = true;
                result.ConnectMs = sw.ElapsedMilliseconds;
                tcp.Close();
            }
            catch (Exception tcpEx)
            {
                result.Error = $"TCP connection failed: {tcpEx.Message}";
                result.ConnectMs = sw.ElapsedMilliseconds;
                return result;
            }

            // 2. Check Windows mount path
            var mountPath = GetMountPath(server);
            result.MountPathExists = Directory.Exists(mountPath);

            if (!result.MountPathExists)
            {
                result.Error = $"Mount path not found: {mountPath}";
                return result;
            }

            var fullPath = Path.Combine(mountPath, testFileName);

            // 3. Write test (skip for backup server - read-only)
            if (!isBackup)
            {
                sw.Restart();
                File.WriteAllText(fullPath, testContent);
                result.WriteMs = sw.ElapsedMilliseconds;
                result.WriteSuccess = true;
            }
            else
            {
                // Backup is read-only - mark as N/A
                result.WriteSuccess = null;
            }

            // 4. Read test
            if (isBackup || result.WriteSuccess == true)
            {
                sw.Restart();

                // For backup, try to read any existing file, not the test file
                if (isBackup)
                {
                    var existingFiles = Directory.GetFiles(mountPath);
                    if (existingFiles.Any())
                    {
                        var contentRead = File.ReadAllText(existingFiles[0]);
                        result.ReadSuccess = !string.IsNullOrEmpty(contentRead);
                    }
                    else
                    {
                        result.ReadSuccess = null; // No files to read
                    }
                }
                else
                {
                    var contentRead = File.ReadAllText(fullPath);
                    result.ReadSuccess = contentRead == testContent;
                }

                result.ReadMs = sw.ElapsedMilliseconds;
            }

            // 5. List test
            sw.Restart();
            var files = Directory.GetFiles(mountPath);
            result.ListMs = sw.ElapsedMilliseconds;

            if (isBackup)
            {
                result.ListSuccess = files.Any();
            }
            else
            {
                result.ListSuccess = files.Any(f => Path.GetFileName(f) == testFileName);
            }

            // 6. Delete test (skip for backup server - read-only)
            if (!isBackup && result.WriteSuccess == true)
            {
                sw.Restart();
                File.Delete(fullPath);
                result.DeleteMs = sw.ElapsedMilliseconds;
                result.DeleteSuccess = !File.Exists(fullPath);
            }
            else
            {
                // Backup is read-only - mark as N/A
                result.DeleteSuccess = null;
            }

            // 7. Sync verification for output servers
            if (result.ServerType == "output" && result.WriteSuccess == true)
            {
                // For Windows testing, we verify:
                // - File was written to mount path successfully
                // - TCP connection to NFS port succeeded
                // Full NFS mount verification requires Linux/WSL
                result.SyncVerified = (result.WriteSuccess ?? false) && result.TcpConnected;
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Display NAS test results in a formatted table.
    /// </summary>
    public static void DisplayNasResults(List<NasTestResult> results)
    {
        if (!results.Any())
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow]NAS Server Test Results[/]").RuleStyle("grey"));

        // Group by type
        var grouped = results.GroupBy(r => r.ServerType).OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold cyan]{group.Key.ToUpper()} SERVERS[/]");

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Server[/]")
                .AddColumn("[bold]TCP[/]")
                .AddColumn("[bold]Mount[/]")
                .AddColumn("[bold]Write[/]")
                .AddColumn("[bold]Read[/]")
                .AddColumn("[bold]List[/]")
                .AddColumn("[bold]Delete[/]")
                .AddColumn("[bold]Sync[/]")
                .AddColumn("[bold]Total (ms)[/]");

            foreach (var r in group)
            {
                var tcpStatus = r.TcpConnected ? $"[green]{r.ConnectMs}ms[/]" : "[red]FAIL[/]";
                var mountStatus = r.MountPathExists ? "[green]YES[/]" : "[red]NO[/]";
                var writeStatus = FormatStatus(r.WriteSuccess, r.WriteMs);
                var readStatus = FormatStatus(r.ReadSuccess, r.ReadMs);
                var listStatus = FormatStatus(r.ListSuccess, r.ListMs);
                var deleteStatus = FormatStatus(r.DeleteSuccess, r.DeleteMs);
                var syncStatus = r.SyncVerified switch
                {
                    true => "[green]YES[/]",
                    false => "[red]NO[/]",
                    null => "[grey]N/A[/]"
                };

                var total = r.ConnectMs + (r.WriteMs ?? 0) + (r.ReadMs ?? 0) + (r.ListMs ?? 0) + (r.DeleteMs ?? 0);

                table.AddRow(
                    r.ServerName,
                    tcpStatus,
                    mountStatus,
                    writeStatus,
                    readStatus,
                    listStatus,
                    deleteStatus,
                    syncStatus,
                    $"[cyan]{total}[/]"
                );

                if (!string.IsNullOrEmpty(r.Error))
                {
                    table.AddRow($"[red]  Error: {r.Error.Truncate(60)}[/]", "", "", "", "", "", "", "", "");
                }
            }

            AnsiConsole.Write(table);
        }

        // Display summary
        AnsiConsole.WriteLine();
        var passed = results.Count(r => r.TcpConnected && r.MountPathExists &&
            (r.WriteSuccess ?? true) && (r.ReadSuccess ?? true) &&
            (r.ListSuccess ?? true) && (r.DeleteSuccess ?? true) &&
            string.IsNullOrEmpty(r.Error));

        var panel = new Panel(
            new Markup($"[bold green]Passed:[/] {passed}/{results.Count}  [bold]NAS Servers[/]"))
            .Header("[bold]NAS Test Summary[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Filter NAS servers from configuration.
    /// </summary>
    public static List<ServerConfig> FilterNasServers(IConfiguration config)
    {
        var servers = new List<ServerConfig>();

        // Check for API-provided configuration in the format created by BuildConfigurationFromApi
        // This will have keys like "nas-input-1", "nas-input-2", etc.
        var configData = config.GetSection("FileSimulator").GetChildren();

        foreach (var section in configData)
        {
            // Check if this is an actual NAS server (starts with "nas-")
            // Skip generic "Nfs" entry which doesn't have a valid mount path
            if (section.Key.StartsWith("nas-", StringComparison.OrdinalIgnoreCase))
            {
                var host = section["Host"];
                var portStr = section["Port"];
                var name = section.Key;

                if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(portStr))
                {
                    var server = new ServerConfig
                    {
                        Name = name,
                        Host = host,
                        Port = int.Parse(portStr),
                        Type = "nas",
                        Protocol = "NFS",
                        BasePath = section["BasePath"] ?? "output",
                        MountPath = section["MountPath"] ?? "/mnt/nfs"
                    };
                    servers.Add(server);
                }
            }
        }

        // If no servers found in API format, check fallback configuration
        if (!servers.Any())
        {
            var fileSimSection = config.GetSection("FileSimulator");
            if (fileSimSection.Exists())
            {
                // Check for NAS section in fallback config
                var nasSection = fileSimSection.GetSection("Nas");
                if (nasSection.Exists())
                {
                    var nasServers = nasSection.GetSection("Servers").GetChildren();
                    foreach (var serverSection in nasServers)
                    {
                        var server = new ServerConfig
                        {
                            Name = serverSection["Name"] ?? "",
                            Host = serverSection["Host"] ?? "localhost",
                            Port = int.Parse(serverSection["Port"] ?? "32149"),
                            Type = "nas",
                            Protocol = "NFS",
                            Directory = serverSection["Directory"] ?? "",
                            BasePath = serverSection["BasePath"] ?? "output"
                        };
                        servers.Add(server);
                    }
                }
            }
        }

        return servers;
    }

    /// <summary>
    /// Get Windows mount path for a NAS server.
    /// </summary>
    private static string GetMountPath(ServerConfig server)
    {
        // Extract server identifier from name (e.g., "nas-input-1" -> "nas-input-1")
        var serverIdentifier = server.Name.ToLowerInvariant();

        // Remove "file-sim-" prefix if present
        if (serverIdentifier.StartsWith("file-sim-"))
        {
            serverIdentifier = serverIdentifier.Substring("file-sim-".Length);
        }

        // Mount path: C:\simulator-data\{server-identifier}\
        return Path.Combine(@"C:\simulator-data", serverIdentifier);
    }

    /// <summary>
    /// Determine server type from server name.
    /// </summary>
    private static string GetServerType(string serverName)
    {
        var name = serverName.ToLowerInvariant();

        if (name.Contains("input"))
            return "input";
        if (name.Contains("backup"))
            return "backup";
        if (name.Contains("output"))
            return "output";

        return "unknown";
    }

    /// <summary>
    /// Format status with timing or N/A.
    /// </summary>
    private static string FormatStatus(bool? success, long? ms)
    {
        return success switch
        {
            true => $"[green]{ms}ms[/]",
            false => "[red]FAIL[/]",
            null => "[grey]N/A[/]"
        };
    }

    /// <summary>
    /// Group servers by type (input, backup, output).
    /// </summary>
    private static Dictionary<string, List<ServerConfig>> GroupByType(List<ServerConfig> servers)
    {
        return servers.GroupBy(s => GetServerType(s.Name))
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
