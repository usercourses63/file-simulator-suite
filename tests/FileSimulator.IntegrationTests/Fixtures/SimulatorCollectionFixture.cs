using System.Net.Http.Json;
using System.Text.Json;
using FileSimulator.IntegrationTests.Models;
using Microsoft.Extensions.Configuration;

namespace FileSimulator.IntegrationTests.Fixtures;

/// <summary>
/// Shared test fixture for simulator integration tests.
/// Validates API connectivity on construction and provides shared HTTP client.
/// </summary>
public class SimulatorCollectionFixture : IDisposable
{
    private ConnectionInfoResponse? _cachedConnectionInfo;
    private readonly object _cacheLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// HTTP client configured for the Control API.
    /// </summary>
    public HttpClient ApiClient { get; }

    /// <summary>
    /// Base URL of the Control API.
    /// </summary>
    public string ApiUrl { get; }

    /// <summary>
    /// Test configuration loaded from appsettings.test.json.
    /// </summary>
    public IConfiguration Configuration { get; }

    public SimulatorCollectionFixture()
    {
        // Load configuration
        Configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.test.json", optional: false)
            .Build();

        ApiUrl = Configuration["ControlApi:BaseUrl"]
            ?? throw new InvalidOperationException("ControlApi:BaseUrl not configured in appsettings.test.json");

        var httpTimeout = Configuration.GetValue<int>("Timeouts:HttpRequest", 30000);

        // Create HTTP client
        ApiClient = new HttpClient
        {
            BaseAddress = new Uri(ApiUrl),
            Timeout = TimeSpan.FromMilliseconds(httpTimeout)
        };

        // Validate API connectivity
        ValidateApiConnectivity();

        Console.WriteLine($"[Fixture] SimulatorCollectionFixture initialized. API: {ApiUrl}");
    }

    private void ValidateApiConnectivity()
    {
        try
        {
            var response = ApiClient.GetAsync("/health").GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Control API health check failed with status {response.StatusCode}. " +
                    $"Ensure the simulator is running and accessible at {ApiUrl}");
            }

            Console.WriteLine("[Fixture] Control API health check passed");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Cannot connect to Control API at {ApiUrl}. " +
                $"Ensure the simulator is running. Error: {ex.Message}",
                ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException(
                $"Control API connection timed out at {ApiUrl}. " +
                $"Ensure the simulator is running and responding. Error: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Gets connection info from the Control API, caching the result.
    /// </summary>
    /// <returns>Connection info for all protocols</returns>
    public async Task<ConnectionInfoResponse> GetConnectionInfoAsync()
    {
        if (_cachedConnectionInfo != null)
        {
            return _cachedConnectionInfo;
        }

        lock (_cacheLock)
        {
            if (_cachedConnectionInfo != null)
            {
                return _cachedConnectionInfo;
            }

            var response = ApiClient.GetAsync("/api/connection-info").GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            _cachedConnectionInfo = JsonSerializer.Deserialize<ConnectionInfoResponse>(content, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize connection info");

            Console.WriteLine("[Fixture] Connection info retrieved and cached");
            return _cachedConnectionInfo;
        }
    }

    /// <summary>
    /// Clears the cached connection info, forcing a refresh on next access.
    /// </summary>
    public void ClearConnectionInfoCache()
    {
        lock (_cacheLock)
        {
            _cachedConnectionInfo = null;
        }
    }

    public void Dispose()
    {
        ApiClient.Dispose();
        Console.WriteLine("[Fixture] SimulatorCollectionFixture disposed");
    }
}
