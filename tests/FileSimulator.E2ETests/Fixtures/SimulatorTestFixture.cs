using System.Diagnostics;
using System.Text.Json;
using Microsoft.Playwright;
using FileSimulator.E2ETests.Support;
using Xunit;

namespace FileSimulator.E2ETests.Fixtures;

public class SimulatorTestFixture : IAsyncLifetime
{
    private Process? _simulatorProcess;
    private IPlaywright? _playwright;
    private PlaywrightSettings? _settings;

    public string DashboardUrl { get; private set; } = string.Empty;
    public string ApiUrl { get; private set; } = string.Empty;
    public IBrowser Browser { get; private set; } = null!;
    public IBrowserContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Load settings
        _settings = await WaitHelpers.LoadPlaywrightSettingsAsync();
        DashboardUrl = _settings.Dashboard.BaseUrl;
        ApiUrl = _settings.Dashboard.ApiUrl;

        // Check if we should use an existing simulator instance
        var useExisting = Environment.GetEnvironmentVariable("USE_EXISTING_SIMULATOR");
        var shouldStartSimulator = string.IsNullOrEmpty(useExisting) && !_settings.Simulator.UseExistingInstance;

        if (shouldStartSimulator)
        {
            Console.WriteLine("Starting simulator via Start-Simulator.ps1...");
            await StartSimulatorAsync();
        }
        else
        {
            Console.WriteLine("Using existing simulator instance...");
        }

        // Wait for services to be ready
        Console.WriteLine($"Waiting for API at {ApiUrl}...");
        await WaitHelpers.WaitForServiceHealthyAsync(
            $"{ApiUrl}/api/health",
            TimeSpan.FromMilliseconds(_settings.Simulator.StartupTimeout)
        );

        Console.WriteLine($"Waiting for Dashboard at {DashboardUrl}...");
        await WaitHelpers.WaitForUrlAccessibleAsync(
            DashboardUrl,
            TimeSpan.FromMilliseconds(_settings.Simulator.StartupTimeout)
        );

        // Initialize Playwright
        Console.WriteLine("Initializing Playwright browser...");
        _playwright = await Playwright.CreateAsync();

        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _settings.Playwright.Headless,
            SlowMo = _settings.Playwright.SlowMo
        });

        Context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            BaseURL = DashboardUrl
        });

        Context.SetDefaultTimeout(_settings.Playwright.Timeout);

        Console.WriteLine("Simulator test fixture initialized successfully.");
    }

    private async Task StartSimulatorAsync()
    {
        if (_settings == null)
            throw new InvalidOperationException("Settings not loaded");

        var repoRoot = WaitHelpers.GetRepoRootPath();
        var scriptPath = Path.Combine(repoRoot, _settings.Simulator.StartScript);

        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"Start-Simulator.ps1 not found at: {scriptPath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = $"-NoProfile -File \"{scriptPath}\" -Wait",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _simulatorProcess = new Process { StartInfo = startInfo };

        // Log output for debugging
        _simulatorProcess.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine($"[Simulator] {e.Data}");
        };

        _simulatorProcess.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine($"[Simulator ERROR] {e.Data}");
        };

        _simulatorProcess.Start();
        _simulatorProcess.BeginOutputReadLine();
        _simulatorProcess.BeginErrorReadLine();

        Console.WriteLine($"Simulator process started (PID: {_simulatorProcess.Id})");
    }

    public async Task DisposeAsync()
    {
        Console.WriteLine("Disposing Simulator test fixture...");

        // Close browser
        if (Context != null)
        {
            await Context.CloseAsync();
        }

        if (Browser != null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();

        // Kill simulator process if we started it
        if (_simulatorProcess != null && !_simulatorProcess.HasExited)
        {
            Console.WriteLine("Stopping simulator process...");

            try
            {
                // Kill the process tree (pwsh + any child processes)
                KillProcessTree(_simulatorProcess.Id);

                if (!_simulatorProcess.WaitForExit(5000))
                {
                    _simulatorProcess.Kill(true);
                }

                Console.WriteLine("Simulator process stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping simulator: {ex.Message}");
            }
            finally
            {
                _simulatorProcess.Dispose();
            }
        }

        Console.WriteLine("Cleanup complete.");
    }

    private static void KillProcessTree(int processId)
    {
        try
        {
            var killProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {processId} /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            killProcess.Start();
            killProcess.WaitForExit(3000);
        }
        catch
        {
            // Best effort - ignore errors
        }
    }
}

public class PlaywrightSettings
{
    public DashboardSettings Dashboard { get; set; } = new();
    public PlaywrightOptions Playwright { get; set; } = new();
    public SimulatorOptions Simulator { get; set; } = new();
}

public class DashboardSettings
{
    public string BaseUrl { get; set; } = "http://localhost:3000";
    public string ApiUrl { get; set; } = "http://localhost:5000";
}

public class PlaywrightOptions
{
    public bool Headless { get; set; } = true;
    public int SlowMo { get; set; } = 0;
    public int Timeout { get; set; } = 30000;
    public string BrowserType { get; set; } = "chromium";
}

public class SimulatorOptions
{
    public string StartScript { get; set; } = "../../scripts/Start-Simulator.ps1";
    public int StartupTimeout { get; set; } = 300000;
    public bool UseExistingInstance { get; set; } = false;
}
