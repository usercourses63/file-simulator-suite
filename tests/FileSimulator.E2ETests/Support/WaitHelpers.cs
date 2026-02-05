using System.Diagnostics;
using System.Text.Json;
using FileSimulator.E2ETests.Fixtures;

namespace FileSimulator.E2ETests.Support;

public static class WaitHelpers
{
    private static readonly HttpClient HttpClient = new();

    /// <summary>
    /// Wait for a service health endpoint to return success
    /// </summary>
    public static async Task WaitForServiceHealthyAsync(string healthUrl, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var response = await HttpClient.GetAsync(healthUrl);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Service healthy at {healthUrl}");
                    return;
                }
            }
            catch
            {
                // Service not ready yet, continue polling
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Service at {healthUrl} did not become healthy within {timeout.TotalSeconds}s");
    }

    /// <summary>
    /// Wait for a URL to be accessible (return 200)
    /// </summary>
    public static async Task WaitForUrlAccessibleAsync(string url, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var response = await HttpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"URL accessible at {url}");
                    return;
                }
            }
            catch
            {
                // URL not accessible yet, continue polling
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"URL {url} did not become accessible within {timeout.TotalSeconds}s");
    }

    /// <summary>
    /// Get the repository root path by finding the .git directory
    /// </summary>
    public static string GetRepoRootPath()
    {
        var directory = Directory.GetCurrentDirectory();

        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory, ".git")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not find repository root (.git directory)");
    }

    /// <summary>
    /// Load PlaywrightSettings.json from the test project directory
    /// </summary>
    public static async Task<PlaywrightSettings> LoadPlaywrightSettingsAsync()
    {
        var settingsPath = Path.Combine(
            AppContext.BaseDirectory,
            "PlaywrightSettings.json"
        );

        if (!File.Exists(settingsPath))
        {
            throw new FileNotFoundException(
                $"PlaywrightSettings.json not found at: {settingsPath}. " +
                "Ensure the file is set to copy to output directory."
            );
        }

        var json = await File.ReadAllTextAsync(settingsPath);
        var settings = JsonSerializer.Deserialize<PlaywrightSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return settings ?? throw new InvalidOperationException("Failed to deserialize PlaywrightSettings.json");
    }
}
