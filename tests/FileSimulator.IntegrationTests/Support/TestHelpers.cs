using System.Net.Http.Json;
using System.Text.Json;
using FileSimulator.IntegrationTests.Models;
using FluentAssertions;

namespace FileSimulator.IntegrationTests.Support;

/// <summary>
/// Helper methods for integration tests.
/// </summary>
public static class TestHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Generates a unique filename with a GUID suffix.
    /// </summary>
    /// <param name="prefix">The filename prefix</param>
    /// <returns>A unique filename like "prefix-abc123def456.txt"</returns>
    public static string GenerateUniqueFileName(string prefix = "test")
    {
        return $"{prefix}-{Guid.NewGuid():N}.txt";
    }

    /// <summary>
    /// Generates a unique server name suitable for Kubernetes (max 20 chars).
    /// </summary>
    /// <param name="type">The server type (e.g., "ftp", "sftp")</param>
    /// <returns>A unique server name like "test-ftp-abc123"</returns>
    public static string GenerateUniqueServerName(string type)
    {
        var guid = Guid.NewGuid().ToString("N")[..8]; // First 8 chars of GUID
        var name = $"test-{type}-{guid}";
        return name.Length > 20 ? name[..20] : name;
    }

    /// <summary>
    /// Waits for a dynamically created server to become ready.
    /// </summary>
    /// <param name="client">The HTTP client</param>
    /// <param name="serverName">The server name to check</param>
    /// <param name="timeout">Maximum time to wait</param>
    /// <returns>True if server became ready, false if timeout</returns>
    public static async Task<bool> WaitForServerReadyAsync(
        HttpClient client,
        string serverName,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync($"/api/servers/{serverName}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var serverInfo = JsonSerializer.Deserialize<ServerStatusResponse>(content, JsonOptions);

                    if (serverInfo?.PodReady == true &&
                        serverInfo.Status?.Equals("Running", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        Console.WriteLine($"[TestHelper] Server {serverName} is ready");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TestHelper] Error checking server {serverName}: {ex.Message}");
            }

            await Task.Delay(2000);
        }

        Console.WriteLine($"[TestHelper] Server {serverName} did not become ready within {timeout.TotalSeconds}s");
        return false;
    }

    /// <summary>
    /// Fetches connection info from the Control API.
    /// </summary>
    /// <param name="client">The HTTP client</param>
    /// <returns>The connection info response</returns>
    public static async Task<ConnectionInfoResponse> GetConnectionInfoAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/connection-info");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ConnectionInfoResponse>(JsonOptions);
        return result ?? throw new InvalidOperationException("Failed to deserialize connection info");
    }

    /// <summary>
    /// Asserts that file content matches expected value with a clear failure message.
    /// </summary>
    /// <param name="expected">Expected content</param>
    /// <param name="actual">Actual content</param>
    /// <param name="context">Additional context for failure message</param>
    public static void AssertFileContent(string expected, string actual, string? context = null)
    {
        actual.Should().Be(
            expected,
            context ?? "File content should match expected value");
    }

    /// <summary>
    /// Creates test content with a unique identifier.
    /// </summary>
    /// <param name="prefix">Content prefix</param>
    /// <returns>Unique test content string</returns>
    public static string CreateTestContent(string prefix = "Test content")
    {
        return $"{prefix} - {DateTime.UtcNow:O} - {Guid.NewGuid():N}";
    }
}
