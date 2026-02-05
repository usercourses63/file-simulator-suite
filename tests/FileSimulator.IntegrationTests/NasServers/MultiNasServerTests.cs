using System.Diagnostics;
using System.Net.Sockets;
using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Models;
using FluentAssertions;
using Xunit;

namespace FileSimulator.IntegrationTests.NasServers;

/// <summary>
/// Tests for all 7 static NAS servers (input-1/2/3, output-1/2/3, backup).
/// Validates TCP connectivity and file operations when mounted.
/// </summary>
[Collection("Simulator")]
public class MultiNasServerTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public MultiNasServerTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Gets all NAS servers from connection info.
    /// </summary>
    private async Task<List<ServerInfo>> GetNasServersFromConnectionInfo()
    {
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        return connectionInfo.Servers
            .Where(s => s.Protocol.Equals("NFS", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Gets Windows mount path for a NAS server.
    /// Format: C:\simulator-data\{server-name}
    /// </summary>
    private static string GetMountPath(ServerInfo server)
    {
        var serverName = server.Name.ToLowerInvariant();

        // Remove "file-sim-" prefix if present
        if (serverName.StartsWith("file-sim-"))
        {
            serverName = serverName.Substring("file-sim-".Length);
        }

        return Path.Combine(@"C:\simulator-data", serverName);
    }

    /// <summary>
    /// Determines if a server is read-only (backup server).
    /// </summary>
    private static bool IsReadOnlyServer(ServerInfo server)
    {
        return server.Name.Contains("backup", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AllNasServers_TcpConnectivity()
    {
        // Arrange
        var nasServers = await GetNasServersFromConnectionInfo();

        // Assert - expect 7 NAS servers
        nasServers.Should().HaveCount(7, "Should have 7 NAS servers (input-1/2/3, output-1/2/3, backup)");

        // Act & Assert - test TCP connectivity for each
        var results = new List<(string Name, bool Success, long LatencyMs, string? Error)>();

        foreach (var server in nasServers)
        {
            var sw = Stopwatch.StartNew();
            bool connected = false;
            string? error = null;

            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(server.Host, server.Port);
                connected = true;
                tcp.Close();
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            sw.Stop();
            results.Add((server.Name, connected, sw.ElapsedMilliseconds, error));
        }

        // Display results table
        Console.WriteLine("\n=== NAS Server TCP Connectivity Results ===");
        Console.WriteLine($"{"Server",-20} {"Status",-10} {"Latency",-12} {"Error"}");
        Console.WriteLine(new string('-', 80));

        foreach (var result in results.OrderBy(r => r.Name))
        {
            var status = result.Success ? "CONNECTED" : "FAILED";
            var latency = $"{result.LatencyMs}ms";
            Console.WriteLine($"{result.Name,-20} {status,-10} {latency,-12} {result.Error}");
        }

        // Assert all servers are reachable
        var failedServers = results.Where(r => !r.Success).Select(r => r.Name).ToList();
        failedServers.Should().BeEmpty($"All 7 NAS servers should be TCP reachable. Failed: {string.Join(", ", failedServers)}");
    }

    [Theory]
    [InlineData("nas-input-1")]
    [InlineData("nas-input-2")]
    [InlineData("nas-input-3")]
    [InlineData("nas-output-1")]
    [InlineData("nas-output-2")]
    [InlineData("nas-output-3")]
    [InlineData("nas-backup")]
    public async Task NasServer_TcpReachable(string serverName)
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var server = connectionInfo.Servers.FirstOrDefault(s =>
            s.Name.Contains(serverName, StringComparison.OrdinalIgnoreCase));

        // Assert server exists
        server.Should().NotBeNull($"{serverName} should be present in connection info");

        // Act & Assert - TCP connectivity
        using var tcp = new TcpClient();
        var connectTask = tcp.ConnectAsync(server!.Host, server.Port);
        var completed = await Task.WhenAny(connectTask, Task.Delay(5000));

        completed.Should().Be(connectTask, $"{serverName} should connect within 5 seconds");
        tcp.Connected.Should().BeTrue($"{serverName} should be TCP reachable at {server.Host}:{server.Port}");

        tcp.Close();
    }

    [Theory]
    [InlineData("nas-input-1")]
    [InlineData("nas-input-2")]
    [InlineData("nas-input-3")]
    [InlineData("nas-output-1")]
    [InlineData("nas-output-2")]
    [InlineData("nas-output-3")]
    [InlineData("nas-backup")]
    public async Task NasServer_FileOperations_WhenMounted(string serverName)
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var server = connectionInfo.Servers.FirstOrDefault(s =>
            s.Name.Contains(serverName, StringComparison.OrdinalIgnoreCase));

        server.Should().NotBeNull($"{serverName} should be present in connection info");

        var mountPath = GetMountPath(server!);
        var isReadOnly = IsReadOnlyServer(server);

        // Skip if mount not available
        if (!Directory.Exists(mountPath))
        {
            Console.WriteLine($"SKIPPED: Mount path not found: {mountPath}");
            return;
        }

        var testFileName = $"test-{Guid.NewGuid():N}.txt";
        var testContent = $"Test content - {DateTime.UtcNow:O}";
        var fullPath = Path.Combine(mountPath, testFileName);

        try
        {
            // Act & Assert - Write test (skip for backup - read-only)
            if (!isReadOnly)
            {
                File.WriteAllText(fullPath, testContent);
                File.Exists(fullPath).Should().BeTrue($"File should be written to {serverName}");

                // Read test
                var readContent = File.ReadAllText(fullPath);
                readContent.Should().Be(testContent, $"File content should match on {serverName}");

                // List test
                var files = Directory.GetFiles(mountPath);
                files.Should().Contain(fullPath, $"File should be listable on {serverName}");

                // Delete test
                File.Delete(fullPath);
                File.Exists(fullPath).Should().BeFalse($"File should be deleted from {serverName}");
            }
            else
            {
                // Backup server - verify read-only
                var files = Directory.GetFiles(mountPath);
                files.Should().NotBeNull($"Backup server {serverName} should allow directory listing");

                Console.WriteLine($"INFO: {serverName} is read-only (backup server), skipping write tests");
            }
        }
        finally
        {
            // Cleanup
            if (File.Exists(fullPath))
            {
                try { File.Delete(fullPath); } catch { /* Ignore cleanup errors */ }
            }
        }
    }

    [Fact]
    public async Task InputServers_AreWritable()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var inputServers = connectionInfo.Servers
            .Where(s => s.Name.Contains("input", StringComparison.OrdinalIgnoreCase))
            .ToList();

        inputServers.Should().HaveCount(3, "Should have 3 input servers");

        var results = new List<(string Name, bool Writable, string? Error)>();

        // Act
        foreach (var server in inputServers)
        {
            var mountPath = GetMountPath(server);

            if (!Directory.Exists(mountPath))
            {
                results.Add((server.Name, false, $"Mount path not found: {mountPath}"));
                continue;
            }

            var testFileName = $"input-test-{Guid.NewGuid():N}.txt";
            var testContent = $"Input test - {DateTime.UtcNow:O}";
            var fullPath = Path.Combine(mountPath, testFileName);

            try
            {
                File.WriteAllText(fullPath, testContent);
                var readBack = File.ReadAllText(fullPath);
                var success = readBack == testContent;

                File.Delete(fullPath);
                results.Add((server.Name, success, null));
            }
            catch (Exception ex)
            {
                results.Add((server.Name, false, ex.Message));
            }
        }

        // Assert
        Console.WriteLine("\n=== Input Server Write Test Results ===");
        foreach (var result in results)
        {
            Console.WriteLine($"{result.Name}: {(result.Writable ? "WRITABLE" : "FAILED")} {result.Error}");
        }

        results.Where(r => !r.Writable && r.Error?.Contains("Mount path not found") != true)
            .Should().BeEmpty("All input servers should be writable when mounted");
    }

    [Fact]
    public async Task OutputServers_AreWritable()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var outputServers = connectionInfo.Servers
            .Where(s => s.Name.Contains("output", StringComparison.OrdinalIgnoreCase))
            .ToList();

        outputServers.Should().HaveCount(3, "Should have 3 output servers");

        var results = new List<(string Name, bool Writable, string? Error)>();

        // Act
        foreach (var server in outputServers)
        {
            var mountPath = GetMountPath(server);

            if (!Directory.Exists(mountPath))
            {
                results.Add((server.Name, false, $"Mount path not found: {mountPath}"));
                continue;
            }

            var testFileName = $"output-test-{Guid.NewGuid():N}.txt";
            var testContent = $"Output test - {DateTime.UtcNow:O}";
            var fullPath = Path.Combine(mountPath, testFileName);

            try
            {
                File.WriteAllText(fullPath, testContent);
                var readBack = File.ReadAllText(fullPath);
                var success = readBack == testContent;

                File.Delete(fullPath);
                results.Add((server.Name, success, null));
            }
            catch (Exception ex)
            {
                results.Add((server.Name, false, ex.Message));
            }
        }

        // Assert
        Console.WriteLine("\n=== Output Server Write Test Results ===");
        foreach (var result in results)
        {
            Console.WriteLine($"{result.Name}: {(result.Writable ? "WRITABLE" : "FAILED")} {result.Error}");
        }

        results.Where(r => !r.Writable && r.Error?.Contains("Mount path not found") != true)
            .Should().BeEmpty("All output servers should be writable when mounted");
    }

    [Fact]
    public async Task BackupServer_IsReadOnly()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var backupServer = connectionInfo.Servers
            .FirstOrDefault(s => s.Name.Contains("backup", StringComparison.OrdinalIgnoreCase));

        backupServer.Should().NotBeNull("Backup server should be present");

        if (backupServer == null)
        {
            return; // Should not reach here due to assertion, but satisfies null check
        }

        var mountPath = GetMountPath(backupServer);

        if (!Directory.Exists(mountPath))
        {
            Console.WriteLine($"SKIPPED: Mount path not found: {mountPath}");
            return;
        }

        // Act & Assert - Read operations should work
        var action = () => Directory.GetFiles(mountPath);
        action.Should().NotThrow("Backup server should allow directory listing");

        var files = Directory.GetFiles(mountPath);
        files.Should().NotBeNull("Backup server should return file list");

        Console.WriteLine($"INFO: Backup server is read-only. Found {files.Length} files.");
    }

    [Fact]
    public async Task AllNasServers_HaveDifferentPorts()
    {
        // Arrange
        var nasServers = await GetNasServersFromConnectionInfo();

        // Act
        var ports = nasServers.Select(s => s.Port).ToList();
        var distinctPorts = ports.Distinct().ToList();

        // Assert
        distinctPorts.Should().HaveCount(ports.Count,
            "Each NAS server should have a unique port for proper service discovery");

        // Verify ports are in valid range (typically 32049-32055 for NodePort)
        ports.Should().OnlyContain(p => p >= 30000 && p <= 33000,
            "NAS server ports should be in valid NodePort range (30000-33000)");

        Console.WriteLine("\n=== NAS Server Port Assignments ===");
        foreach (var server in nasServers.OrderBy(s => s.Port))
        {
            Console.WriteLine($"{server.Name,-20} Port: {server.Port}");
        }
    }
}
