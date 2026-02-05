using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using SMBLibrary;
using SMBLibrary.Client;

namespace FileSimulator.IntegrationTests.Support;

/// <summary>
/// Platform-specific helpers for testing SMB and NFS protocols.
/// These helpers detect infrastructure availability and provide clear skip messages.
/// </summary>
public static class PlatformHelpers
{
    /// <summary>
    /// Checks if SMB server is accessible at the specified host and port.
    /// This requires minikube tunnel to be running on Windows.
    /// </summary>
    /// <param name="host">SMB server hostname or IP</param>
    /// <param name="port">SMB server port</param>
    /// <returns>True if SMB is accessible, false otherwise</returns>
    public static async Task<bool> IsSmbAccessibleAsync(string host, int port)
    {
        try
        {
            // First check TCP connectivity
            var tcpAccessible = await TryTcpConnectAsync(host, port, TimeSpan.FromSeconds(5));
            if (!tcpAccessible)
            {
                Console.WriteLine($"[PlatformHelper] SMB TCP connection failed to {host}:{port}");
                return false;
            }

            // Try to establish SMB connection
            return await Task.Run(() =>
            {
                try
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
                        targetAddress = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                        if (targetAddress == null)
                        {
                            Console.WriteLine($"[PlatformHelper] Could not resolve host: {host}");
                            return false;
                        }
                    }

                    // Try SMB2 connection
                    var client = new SMB2Client();
                    try
                    {
                        var connected = client.Connect(targetAddress, SMBTransportType.DirectTCPTransport, port);

                        if (connected)
                        {
                            Console.WriteLine($"[PlatformHelper] SMB connection successful to {host}:{port}");
                            client.Disconnect();
                        }
                        else
                        {
                            Console.WriteLine($"[PlatformHelper] SMB connection returned false for {host}:{port}");
                        }

                        return connected;
                    }
                    finally
                    {
                        try { client.Disconnect(); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PlatformHelper] SMB connection error: {ex.GetType().Name} - {ex.Message}");
                    return false;
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlatformHelper] SMB accessibility check failed: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if NFS mount path exists and is accessible.
    /// </summary>
    /// <param name="mountPath">The NFS mount path to check</param>
    /// <returns>True if mounted and accessible, false otherwise</returns>
    public static Task<bool> IsNfsMountedAsync(string mountPath)
    {
        try
        {
            // Check if mount path directory exists
            if (!Directory.Exists(mountPath))
            {
                Console.WriteLine($"[PlatformHelper] NFS mount path does not exist: {mountPath}");
                return Task.FromResult(false);
            }

            // Check if it's not just an empty mount point
            // Try to list contents to verify it's accessible
            try
            {
                var _ = Directory.GetFileSystemEntries(mountPath);
                Console.WriteLine($"[PlatformHelper] NFS mount path is accessible: {mountPath}");
                return Task.FromResult(true);
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"[PlatformHelper] NFS mount path exists but not accessible: {mountPath}");
                return Task.FromResult(false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlatformHelper] NFS mount check failed: {ex.GetType().Name} - {ex.Message}");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Gets the platform-specific NFS mount path.
    /// </summary>
    /// <returns>The expected NFS mount path for the current platform</returns>
    public static string GetNfsMountPath()
    {
        // Check for environment variable override
        var envPath = Environment.GetEnvironmentVariable("NFS_MOUNT_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            return envPath;
        }

        // Platform-specific defaults
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: Minikube mount path
            return @"C:\simulator-data";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux: Standard NFS mount point
            return "/mnt/nfs";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: Standard mount point
            return "/mnt/nfs";
        }

        // Fallback
        return "/mnt/nfs";
    }

    /// <summary>
    /// Attempts to establish a TCP connection to verify server accessibility.
    /// </summary>
    /// <param name="host">Hostname or IP address</param>
    /// <param name="port">TCP port</param>
    /// <param name="timeout">Connection timeout</param>
    /// <returns>True if connection succeeds, false otherwise</returns>
    public static async Task<bool> TryTcpConnectAsync(string host, int port, TimeSpan timeout)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(timeout);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == connectTask && !connectTask.IsFaulted)
            {
                Console.WriteLine($"[PlatformHelper] TCP connection successful to {host}:{port}");
                return true;
            }
            else if (completedTask == timeoutTask)
            {
                Console.WriteLine($"[PlatformHelper] TCP connection timed out to {host}:{port}");
                return false;
            }
            else
            {
                Console.WriteLine($"[PlatformHelper] TCP connection failed to {host}:{port}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlatformHelper] TCP connection error to {host}:{port}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generates a standardized skip message for tests that require unavailable infrastructure.
    /// </summary>
    /// <param name="protocol">The protocol being tested</param>
    /// <param name="requirement">What infrastructure is required</param>
    /// <returns>A formatted skip message with instructions</returns>
    public static string GetSkipMessage(string protocol, string requirement)
    {
        var instructions = protocol.ToUpper() switch
        {
            "SMB" => "Start 'minikube tunnel -p file-simulator' in an Administrator PowerShell terminal",
            "NFS" when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) =>
                $"Use Minikube mount: C:\\simulator-data should be mounted at /mnt/simulator-data in Minikube",
            "NFS" when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) =>
                "Mount the NFS share: sudo mount -t nfs <host>:/data /mnt/nfs",
            _ => "Ensure required infrastructure is running"
        };

        return $"{protocol} test skipped: {requirement}. To run this test: {instructions}";
    }

    /// <summary>
    /// Gets platform information for diagnostic purposes.
    /// </summary>
    /// <returns>A string describing the current platform</returns>
    public static string GetPlatformInfo()
    {
        var os = "Unknown";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            os = "Windows";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            os = "Linux";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            os = "macOS";

        return $"{os} {RuntimeInformation.OSArchitecture}";
    }
}
