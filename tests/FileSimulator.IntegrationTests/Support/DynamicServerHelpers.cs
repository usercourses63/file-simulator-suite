using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;

namespace FileSimulator.IntegrationTests.Support;

/// <summary>
/// Helper methods for managing dynamic servers via the Control API.
/// </summary>
public static class DynamicServerHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Creates a dynamic FTP server via the Control API.
    /// </summary>
    /// <param name="client">The HTTP client</param>
    /// <param name="name">The server name (must be unique, max 20 chars)</param>
    /// <param name="directory">Optional directory path (default: input/test-dynamic)</param>
    /// <returns>Server creation response with connection details</returns>
    public static async Task<ServerCreationResponse> CreateFtpServerAsync(
        HttpClient client,
        string name,
        string? directory = null)
    {
        var url = "/api/servers/ftp";
        var requestBody = new
        {
            name,
            username = "testuser",
            password = "testpass123",
            directory = directory ?? "input/test-dynamic"
        };

        Console.WriteLine($"[DynamicServerHelper] Creating FTP server: {name}");

        var policy = RetryPolicies.HttpRetryPolicy(maxAttempts: 3, baseDelaySeconds: 2);
        var response = await policy.ExecuteAsync(async () =>
            await client.PostAsJsonAsync(url, requestBody));

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to create FTP server '{name}'. Status: {response.StatusCode}, Error: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<ServerCreationResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize server creation response");

        Console.WriteLine($"[DynamicServerHelper] FTP server created: {name} at {result.Host}:{result.NodePort ?? result.Port}");
        return result;
    }

    /// <summary>
    /// Creates a dynamic SFTP server via the Control API.
    /// </summary>
    /// <param name="client">The HTTP client</param>
    /// <param name="name">The server name (must be unique, max 20 chars)</param>
    /// <param name="directory">Optional directory path (default: input/test-dynamic)</param>
    /// <returns>Server creation response with connection details</returns>
    public static async Task<ServerCreationResponse> CreateSftpServerAsync(
        HttpClient client,
        string name,
        string? directory = null)
    {
        var url = "/api/servers/sftp";
        var requestBody = new
        {
            name,
            username = "testuser",
            password = "testpass123",
            directory = directory ?? "input/test-dynamic"
        };

        Console.WriteLine($"[DynamicServerHelper] Creating SFTP server: {name}");

        var policy = RetryPolicies.HttpRetryPolicy(maxAttempts: 3, baseDelaySeconds: 2);
        var response = await policy.ExecuteAsync(async () =>
            await client.PostAsJsonAsync(url, requestBody));

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to create SFTP server '{name}'. Status: {response.StatusCode}, Error: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<ServerCreationResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize server creation response");

        Console.WriteLine($"[DynamicServerHelper] SFTP server created: {name} at {result.Host}:{result.NodePort ?? result.Port}");
        return result;
    }

    /// <summary>
    /// Creates a dynamic NAS server via the Control API.
    /// </summary>
    /// <param name="client">The HTTP client</param>
    /// <param name="name">The server name (must be unique, max 20 chars)</param>
    /// <param name="directory">Optional directory path (default: input/test-dynamic)</param>
    /// <param name="readOnly">Whether the NAS should be read-only</param>
    /// <returns>Server creation response with connection details</returns>
    public static async Task<ServerCreationResponse> CreateNasServerAsync(
        HttpClient client,
        string name,
        string? directory = null,
        bool readOnly = false)
    {
        var url = "/api/servers/nas";
        var requestBody = new
        {
            name,
            directory = directory ?? "input/test-dynamic",
            readOnly
        };

        Console.WriteLine($"[DynamicServerHelper] Creating NAS server: {name}");

        var policy = RetryPolicies.HttpRetryPolicy(maxAttempts: 3, baseDelaySeconds: 2);
        var response = await policy.ExecuteAsync(async () =>
            await client.PostAsJsonAsync(url, requestBody));

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to create NAS server '{name}'. Status: {response.StatusCode}, Error: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<ServerCreationResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize server creation response");

        Console.WriteLine($"[DynamicServerHelper] NAS server created: {name} at {result.Host}:{result.NodePort ?? result.Port}");
        return result;
    }

    /// <summary>
    /// Waits for a dynamically created server to become ready.
    /// Polls the server status endpoint until PodReady=true and Status=Running.
    /// </summary>
    /// <param name="client">The HTTP client</param>
    /// <param name="name">The server name to check</param>
    /// <param name="timeout">Maximum time to wait (default: 60 seconds)</param>
    /// <returns>Server status when ready</returns>
    /// <exception cref="TimeoutException">If server doesn't become ready within timeout</exception>
    public static async Task<ServerStatusResponse> WaitForServerReadyAsync(
        HttpClient client,
        string name,
        TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(60);
        var url = $"/api/servers/{name}";
        var deadline = DateTime.UtcNow + maxWait;
        var attempt = 0;

        Console.WriteLine($"[DynamicServerHelper] Waiting for server '{name}' to become ready (timeout: {maxWait.TotalSeconds}s)");

        while (DateTime.UtcNow < deadline)
        {
            attempt++;

            try
            {
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var status = await response.Content.ReadFromJsonAsync<ServerStatusResponse>(JsonOptions);

                    if (status != null)
                    {
                        Console.WriteLine($"[DynamicServerHelper] Attempt {attempt}: Status={status.Status}, PodReady={status.PodReady}");

                        if (status.PodReady &&
                            status.Status?.Equals("Running", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            Console.WriteLine($"[DynamicServerHelper] Server '{name}' is ready after {attempt} attempts");
                            return status;
                        }
                    }
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"[DynamicServerHelper] Attempt {attempt}: Server not found yet (404)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DynamicServerHelper] Attempt {attempt} error: {ex.Message}");
            }

            await Task.Delay(2000); // Poll every 2 seconds
        }

        throw new TimeoutException(
            $"Server '{name}' did not become ready within {maxWait.TotalSeconds} seconds after {attempt} attempts");
    }

    /// <summary>
    /// Gets the current status of a dynamic server.
    /// </summary>
    /// <param name="client">The HTTP client</param>
    /// <param name="name">The server name</param>
    /// <returns>Server status, or null if not found (404)</returns>
    public static async Task<ServerStatusResponse?> GetServerStatusAsync(
        HttpClient client,
        string name)
    {
        var url = $"/api/servers/{name}";

        try
        {
            var response = await client.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var status = await response.Content.ReadFromJsonAsync<ServerStatusResponse>(JsonOptions);
            return status;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Failed to get status for server '{name}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a dynamic server via the Control API.
    /// Waits for deletion to be confirmed (server returns 404).
    /// </summary>
    /// <param name="client">The HTTP client</param>
    /// <param name="name">The server name</param>
    /// <returns>True if deleted successfully, false otherwise</returns>
    public static async Task<bool> DeleteServerAsync(HttpClient client, string name)
    {
        var url = $"/api/servers/{name}";

        Console.WriteLine($"[DynamicServerHelper] Deleting server: {name}");

        try
        {
            var response = await client.DeleteAsync(url);

            // Accept both 200 OK and 404 NotFound as success (idempotent delete)
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DynamicServerHelper] Delete request failed: {response.StatusCode} - {errorContent}");
                return false;
            }

            Console.WriteLine($"[DynamicServerHelper] Delete request accepted for '{name}'");

            // Poll to verify deletion (max 30 seconds)
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(2000);

                var checkResponse = await client.GetAsync(url);
                if (checkResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"[DynamicServerHelper] Server '{name}' deletion verified");
                    return true;
                }

                Console.WriteLine($"[DynamicServerHelper] Waiting for server '{name}' deletion...");
            }

            // Deletion request succeeded even if verification timed out
            Console.WriteLine($"[DynamicServerHelper] Deletion verification timed out, but request was accepted");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DynamicServerHelper] Delete error for '{name}': {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Response from POST /api/servers/ftp|sftp|nas endpoints (server creation).
/// Returns DiscoveredServer model from the API.
/// </summary>
public class ServerCreationResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("podName")]
    public string PodName { get; set; } = string.Empty;

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("clusterIp")]
    public string ClusterIp { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("nodePort")]
    public int? NodePort { get; set; }

    [JsonPropertyName("podStatus")]
    public string PodStatus { get; set; } = string.Empty;

    [JsonPropertyName("podReady")]
    public bool PodReady { get; set; }

    [JsonPropertyName("discoveredAt")]
    public DateTime DiscoveredAt { get; set; }

    [JsonPropertyName("isDynamic")]
    public bool IsDynamic { get; set; }

    [JsonPropertyName("managedBy")]
    public string ManagedBy { get; set; } = string.Empty;

    [JsonPropertyName("directory")]
    public string? Directory { get; set; }

    [JsonPropertyName("credentials")]
    public ServerCredentialsInfo? Credentials { get; set; }

    /// <summary>
    /// Gets the external host (file-simulator.local for Minikube).
    /// For external access, use this with NodePort.
    /// </summary>
    public string Host => "file-simulator.local";

    /// <summary>
    /// Gets the username from credentials or returns null.
    /// </summary>
    public string? Username => Credentials?.Username;

    /// <summary>
    /// Gets the password from credentials or returns null.
    /// </summary>
    public string? Password => Credentials?.Password;
}

/// <summary>
/// Credentials information in DiscoveredServer response.
/// </summary>
public class ServerCredentialsInfo
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

/// <summary>
/// Response from GET /api/servers/{name} endpoint (server status).
/// Returns DiscoveredServer model from the API.
/// </summary>
public class ServerStatusResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("podName")]
    public string PodName { get; set; } = string.Empty;

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("clusterIp")]
    public string ClusterIp { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("nodePort")]
    public int? NodePort { get; set; }

    [JsonPropertyName("podStatus")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("podReady")]
    public bool PodReady { get; set; }

    [JsonPropertyName("discoveredAt")]
    public DateTime DiscoveredAt { get; set; }

    [JsonPropertyName("isDynamic")]
    public bool IsDynamic { get; set; }

    [JsonPropertyName("managedBy")]
    public string ManagedBy { get; set; } = string.Empty;

    [JsonPropertyName("directory")]
    public string? Directory { get; set; }

    [JsonPropertyName("credentials")]
    public ServerCredentialsInfo? Credentials { get; set; }

    /// <summary>
    /// Gets the external host for Minikube access.
    /// </summary>
    public string Host => "file-simulator.local";

    /// <summary>
    /// Gets the username from credentials. Returns "testuser" default if not available.
    /// </summary>
    public string Username => Credentials?.Username ?? "testuser";

    /// <summary>
    /// Gets the password from credentials. Returns "testpass123" default if not available.
    /// </summary>
    public string Password => Credentials?.Password ?? "testpass123";
}
