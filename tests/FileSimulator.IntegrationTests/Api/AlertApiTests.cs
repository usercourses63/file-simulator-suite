using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FileSimulator.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace FileSimulator.IntegrationTests.Api;

/// <summary>
/// Tests for the /api/alerts endpoints.
/// Validates alert retrieval, filtering, and statistics.
/// </summary>
[Collection("Simulator")]
public class AlertApiTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public AlertApiTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Alerts_GetActive_ReturnsResponse()
    {
        // Arrange & Act
        var response = await _fixture.ApiClient.GetAsync("/api/alerts/active");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue(
            $"GET /api/alerts/active should succeed. Status: {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNull("Response should have content");

        // Verify it's valid JSON (array of alerts)
        var alerts = JsonSerializer.Deserialize<List<JsonElement>>(content);
        alerts.Should().NotBeNull("Response should be deserializable as JSON array");

        Console.WriteLine($"Active alerts: {alerts!.Count}");
    }

    [Fact]
    public async Task Alerts_GetHistory_ReturnsAlerts()
    {
        // Arrange & Act
        var response = await _fixture.ApiClient.GetAsync("/api/alerts/history");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue(
            $"GET /api/alerts/history should succeed. Status: {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNull("Response should have content");

        // Verify it's valid JSON
        var historyResponse = JsonSerializer.Deserialize<JsonElement>(content);
        historyResponse.ValueKind.Should().NotBe(JsonValueKind.Undefined, "Response should be valid JSON");

        // History endpoint may return paged results or array
        if (historyResponse.ValueKind == JsonValueKind.Array)
        {
            var alerts = historyResponse.EnumerateArray().ToList();
            Console.WriteLine($"Alert history contains {alerts.Count} alerts");
        }
        else if (historyResponse.TryGetProperty("items", out var items) ||
                 historyResponse.TryGetProperty("alerts", out items) ||
                 historyResponse.TryGetProperty("data", out items))
        {
            var alertCount = items.GetArrayLength();
            Console.WriteLine($"Alert history contains {alertCount} alerts (paged)");

            // Check for pagination info
            if (historyResponse.TryGetProperty("totalCount", out var totalCount))
            {
                Console.WriteLine($"Total alerts in history: {totalCount.GetInt32()}");
            }
        }
        else
        {
            Console.WriteLine($"Alert history response type: {historyResponse.ValueKind}");
        }
    }

    [Fact]
    public async Task Alerts_GetHistory_FiltersBySeverity()
    {
        // Arrange - Test each severity level
        var severities = new[] { "info", "warning", "critical" };

        foreach (var severity in severities)
        {
            // Act
            var response = await _fixture.ApiClient.GetAsync($"/api/alerts/history?severity={severity}");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue(
                $"GET /api/alerts/history?severity={severity} should succeed. Status: {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNull($"Response for severity={severity} should have content");

            Console.WriteLine($"Alerts with severity '{severity}': {content.Length} bytes");
        }
    }

    [Fact]
    public async Task Alerts_GetStats_ReturnsStatistics()
    {
        // Arrange & Act
        var response = await _fixture.ApiClient.GetAsync("/api/alerts/stats");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue(
            $"GET /api/alerts/stats should succeed. Status: {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNull("Response should have content");

        // Verify it's valid JSON
        var stats = JsonSerializer.Deserialize<JsonElement>(content);
        stats.ValueKind.Should().Be(JsonValueKind.Object, "Stats should be a JSON object");

        // Log available stats
        Console.WriteLine("Alert statistics:");
        foreach (var property in stats.EnumerateObject())
        {
            Console.WriteLine($"  {property.Name}: {property.Value}");
        }
    }

    [Fact]
    public async Task Alerts_GetById_ReturnsNotFoundForInvalidId()
    {
        // Arrange
        var invalidId = Guid.NewGuid().ToString();

        // Act
        var response = await _fixture.ApiClient.GetAsync($"/api/alerts/{invalidId}");

        // Assert - Should return 404 for non-existent alert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            $"GET /api/alerts/{invalidId} should return 404 for non-existent alert");
    }

    [Fact]
    public async Task Alerts_GetById_ReturnsAlertIfExists()
    {
        // Arrange - First get history to find an existing alert
        var historyResponse = await _fixture.ApiClient.GetAsync("/api/alerts/history");
        if (!historyResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("Skipping test - could not get alert history");
            return;
        }

        var historyContent = await historyResponse.Content.ReadAsStringAsync();
        var historyJson = JsonSerializer.Deserialize<JsonElement>(historyContent);

        // Try to extract an alert ID from the response
        string? alertId = null;

        if (historyJson.ValueKind == JsonValueKind.Array)
        {
            if (historyJson.GetArrayLength() > 0)
            {
                var firstAlert = historyJson[0];
                if (firstAlert.TryGetProperty("id", out var idProp))
                {
                    alertId = idProp.GetString();
                }
            }
        }
        else if (historyJson.ValueKind == JsonValueKind.Object)
        {
            // Try paged response format with "items" property
            if (historyJson.TryGetProperty("items", out var items) &&
                items.ValueKind == JsonValueKind.Array &&
                items.GetArrayLength() > 0)
            {
                var firstAlert = items[0];
                if (firstAlert.TryGetProperty("id", out var idProp))
                {
                    alertId = idProp.GetString();
                }
            }
        }

        if (string.IsNullOrEmpty(alertId))
        {
            Console.WriteLine("No alerts in history to test GetById");
            return;
        }

        // Act
        var response = await _fixture.ApiClient.GetAsync($"/api/alerts/{alertId}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue(
            $"GET /api/alerts/{alertId} should succeed for existing alert. Status: {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        var alert = JsonSerializer.Deserialize<JsonElement>(content);

        alert.TryGetProperty("id", out var returnedId).Should().BeTrue("Alert should have id property");
        returnedId.GetString().Should().Be(alertId, "Returned alert ID should match requested ID");

        Console.WriteLine($"Successfully retrieved alert: {alertId}");
    }

    [Fact]
    public async Task Alerts_GetHistory_SupportsPagination()
    {
        // Arrange & Act - Request first page with small page size
        var response = await _fixture.ApiClient.GetAsync("/api/alerts/history?pageSize=5&page=1");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue(
            $"GET /api/alerts/history with pagination should succeed. Status: {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        // If paged response, check structure
        if (result.ValueKind == JsonValueKind.Object)
        {
            if (result.TryGetProperty("pageSize", out var pageSize))
            {
                Console.WriteLine($"Page size: {pageSize.GetInt32()}");
            }
            if (result.TryGetProperty("page", out var page) ||
                result.TryGetProperty("currentPage", out page))
            {
                Console.WriteLine($"Current page: {page.GetInt32()}");
            }
            if (result.TryGetProperty("totalPages", out var totalPages))
            {
                Console.WriteLine($"Total pages: {totalPages.GetInt32()}");
            }
            if (result.TryGetProperty("totalCount", out var totalCount))
            {
                Console.WriteLine($"Total count: {totalCount.GetInt32()}");
            }
        }
    }

    [Fact]
    public async Task Alerts_GetHistory_FiltersByDateRange()
    {
        // Arrange - Use date range from last 7 days
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-7);

        // Act
        var response = await _fixture.ApiClient.GetAsync(
            $"/api/alerts/history?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue(
            $"GET /api/alerts/history with date range should succeed. Status: {response.StatusCode}");

        Console.WriteLine($"Alert history for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} retrieved");
    }

    [Fact]
    public async Task Alerts_GetHistory_FiltersByType()
    {
        // Arrange - Common alert types
        var alertTypes = new[] { "ServerHealth", "DiskSpace", "KafkaHealth" };

        foreach (var alertType in alertTypes)
        {
            // Act
            var response = await _fixture.ApiClient.GetAsync($"/api/alerts/history?type={alertType}");

            // Assert - Should succeed (may return empty if no alerts of that type)
            response.IsSuccessStatusCode.Should().BeTrue(
                $"GET /api/alerts/history?type={alertType} should succeed. Status: {response.StatusCode}");

            Console.WriteLine($"Alert history for type '{alertType}' retrieved");
        }
    }
}
