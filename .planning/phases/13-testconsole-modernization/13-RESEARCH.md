# Phase 13: TestConsole Modernization and Release - Research

**Researched:** 2026-02-05
**Domain:** .NET integration testing, E2E testing, API testing, release automation
**Confidence:** HIGH

## Summary

Phase 13 modernizes the TestConsole application to become API-driven and adds comprehensive E2E testing with Playwright. The phase involves five main domains: (1) API-driven configuration via Control API's /api/connection-info endpoint, (2) comprehensive protocol testing including 7 multi-NAS servers and dynamic server lifecycle, (3) Kafka integration testing, (4) Playwright E2E testing for the React dashboard, and (5) GitHub release automation with semantic versioning.

The standard approach uses Microsoft.Playwright 1.58.0 for E2E testing, WebApplicationFactory for API integration tests, Testcontainers for Kafka testing, and conventional commits with automated changelog generation for releases. The TestConsole already has excellent foundation with Spectre.Console for CLI UX and complete protocol implementations.

**Primary recommendation:** Use Microsoft.Playwright 1.58.0 with xUnit for E2E tests, extend TestConsole with HttpClient to fetch /api/connection-info dynamically, leverage WebApplicationFactory for Control API integration tests, and use conventional commits with GitHub Actions for automated release creation with semantic versioning.

## Standard Stack

The established libraries/tools for this domain:

### Core Testing Frameworks
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Playwright | 1.58.0 | Browser automation for E2E tests | Official Microsoft library, cross-browser support, robust auto-waiting, .NET 9 compatible |
| Microsoft.Playwright.Xunit | 1.58.0 | xUnit integration for Playwright | Seamless test framework integration, parallel execution support |
| Microsoft.AspNetCore.Mvc.Testing | 9.0.0 | In-memory API integration testing | Built-in WebApplicationFactory, no network overhead, official ASP.NET Core package |
| xUnit | 2.9.0+ | Test framework | Industry standard for .NET, parallel execution by default, excellent async support |
| Testcontainers | Latest | Docker-based integration tests | Standard for Kafka testing in .NET, lifecycle management, isolation |

### Supporting Libraries
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| FluentAssertions | 6.12+ | Readable test assertions | All test projects for better error messages |
| Bogus | Latest | Test data generation | When creating dynamic server test data |
| Moq | 4.20+ | Mocking framework | Unit tests requiring isolated dependencies |
| Respawn | Latest | Database cleanup | Integration tests needing clean state |

### Release Automation
| Tool | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| semantic-release | Latest | Automated versioning and changelog | Industry standard, conventional commits support, full automation |
| conventional-changelog | Latest | Changelog generation | Git history-based, semantic version integration |
| GitHub CLI (gh) | 2.x | Release creation | Official GitHub tool, scriptable, supports assets |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Playwright | Selenium | Selenium more mature but slower, more flaky, requires WebDriver management |
| xUnit | NUnit/MSTest | NUnit has more fixture options but xUnit better for parallel execution |
| Testcontainers | Embedded Kafka | Embedded Kafka not available for .NET, Testcontainers more realistic |
| semantic-release | manual versioning | Manual gives control but error-prone, time-consuming, inconsistent |

**Installation:**
```bash
# TestConsole project
dotnet add package Microsoft.Extensions.Http --version 9.0.0

# New E2E test project
dotnet new xunit -n FileSimulator.E2ETests
cd FileSimulator.E2ETests
dotnet add package Microsoft.Playwright.Xunit --version 1.58.0
dotnet add package FluentAssertions --version 6.12.2

# Install Playwright browsers (one-time)
pwsh bin/Debug/net9.0/playwright.ps1 install

# New Integration test project (if separate from E2E)
dotnet new xunit -n FileSimulator.IntegrationTests
dotnet add package Microsoft.AspNetCore.Mvc.Testing --version 9.0.0
dotnet add package Testcontainers --version 3.10.0
dotnet add package FluentAssertions --version 6.12.2
```

## Architecture Patterns

### Recommended Project Structure
```
src/
├── FileSimulator.TestConsole/          # CLI test application
│   ├── Program.cs                      # Main entry + protocol tests
│   ├── CrossProtocolTest.cs            # Cross-protocol validation
│   ├── ApiConfigurationProvider.cs     # NEW: Fetch /api/connection-info
│   ├── NasServerTests.cs               # NEW: 7 NAS server testing
│   ├── DynamicServerTests.cs           # NEW: Dynamic server lifecycle
│   └── KafkaTests.cs                   # NEW: Kafka integration tests
│
tests/
├── FileSimulator.E2ETests/             # NEW: Playwright E2E tests
│   ├── PageObjects/                    # Page object models
│   │   ├── DashboardPage.cs
│   │   ├── FileOperationsPage.cs
│   │   ├── ServersPage.cs
│   │   └── KafkaPage.cs
│   ├── Tests/                          # Test specifications
│   │   ├── DashboardTests.cs
│   │   ├── FileUploadTests.cs
│   │   ├── ServerManagementTests.cs
│   │   ├── KafkaTests.cs
│   │   └── AlertsTests.cs
│   ├── Fixtures/                       # Test fixtures
│   │   └── SimulatorTestFixture.cs     # Start-Simulator.ps1 integration
│   ├── Support/                        # Utilities
│   │   ├── WaitHelpers.cs
│   │   └── TestDataHelpers.cs
│   └── PlaywrightSettings.json         # Browser/timeout config
│
└── FileSimulator.IntegrationTests/     # NEW: API integration tests
    ├── ControlApiTests.cs              # API endpoint tests
    ├── KafkaIntegrationTests.cs        # Kafka with Testcontainers
    ├── Fixtures/
    │   └── ApiTestFixture.cs           # WebApplicationFactory setup
    └── appsettings.Test.json
```

### Pattern 1: API-Driven Configuration Fetching
**What:** TestConsole fetches server configuration dynamically from Control API instead of hardcoded appsettings.json
**When to use:** When Control API is available (check /api/health first)
**Example:**
```csharp
// Source: Project requirements + WebApplicationFactory pattern
public class ApiConfigurationProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public ApiConfigurationProvider(string apiBaseUrl, ILogger logger)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _logger = logger;
    }

    public async Task<ConnectionInfo?> GetConnectionInfoAsync(CancellationToken ct = default)
    {
        try
        {
            // Check if API is available
            var healthResponse = await _httpClient.GetAsync("/api/health", ct);
            if (!healthResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Control API not available at {Url}", _httpClient.BaseAddress);
                return null;
            }

            // Fetch connection info
            var response = await _httpClient.GetAsync("/api/connection-info", ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var connectionInfo = JsonSerializer.Deserialize<ConnectionInfo>(json);

            _logger.LogInformation("Fetched connection info for {Count} servers",
                connectionInfo?.Servers?.Count ?? 0);

            return connectionInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch connection info from API");
            return null;
        }
    }
}

// Usage in TestConsole
var apiUrl = config["ControlApi:BaseUrl"] ?? "http://localhost:5000";
var apiProvider = new ApiConfigurationProvider(apiUrl, logger);
var connectionInfo = await apiProvider.GetConnectionInfoAsync();

if (connectionInfo != null)
{
    // Use dynamic configuration
    await TestServersFromApi(connectionInfo);
}
else
{
    // Fallback to appsettings.json
    await TestServersFromConfig(config);
}
```

### Pattern 2: Multi-NAS Server Testing Loop
**What:** Test all 7 NAS servers (input-1/2/3, backup, output-1/2/3) with file operations
**When to use:** When multi-NAS deployment is active
**Example:**
```csharp
// Source: Project multi-NAS topology + existing NFS test pattern
public async Task TestAllNasServers(ConnectionInfo connectionInfo)
{
    var nasServers = connectionInfo.Servers
        .Where(s => s.Protocol == "NFS" && s.Type == "nas")
        .ToList();

    AnsiConsole.MarkupLine($"[yellow]Testing {nasServers.Count} NAS servers...[/]");

    var results = new List<NasTestResult>();

    foreach (var server in nasServers)
    {
        var result = new NasTestResult { ServerName = server.Name };

        try
        {
            // Test connectivity
            using var tcp = new TcpClient();
            var sw = Stopwatch.StartNew();
            await tcp.ConnectAsync(server.Host, server.Port);
            result.ConnectMs = sw.ElapsedMilliseconds;
            result.Connected = true;
            tcp.Close();

            // Test file operations via Windows mount
            var mountPath = Path.Combine(@"C:\simulator-data", server.Name);
            if (Directory.Exists(mountPath))
            {
                var testFile = Path.Combine(mountPath, $"test-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt");
                var content = $"NAS test for {server.Name}";

                // Write
                sw.Restart();
                await File.WriteAllTextAsync(testFile, content);
                result.WriteMs = sw.ElapsedMilliseconds;
                result.WriteSuccess = true;

                // Read
                sw.Restart();
                var readContent = await File.ReadAllTextAsync(testFile);
                result.ReadMs = sw.ElapsedMilliseconds;
                result.ReadSuccess = readContent == content;

                // Delete
                sw.Restart();
                File.Delete(testFile);
                result.DeleteMs = sw.ElapsedMilliseconds;
                result.DeleteSuccess = !File.Exists(testFile);
            }
            else
            {
                result.Error = $"Mount path not found: {mountPath}";
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        results.Add(result);
    }

    DisplayNasResults(results);
}
```

### Pattern 3: Dynamic Server Lifecycle Testing
**What:** Create dynamic servers, test connectivity, then delete them via API
**When to use:** When testing dynamic server management functionality
**Example:**
```csharp
// Source: Phase 11 dynamic server patterns + API testing
public async Task TestDynamicServerLifecycle(HttpClient apiClient)
{
    AnsiConsole.MarkupLine("[yellow]Testing dynamic server lifecycle...[/]");

    // 1. Create dynamic FTP server
    var createRequest = new
    {
        Name = $"test-ftp-{DateTime.UtcNow:yyyyMMddHHmmss}",
        Protocol = "FTP",
        Username = "testuser",
        Password = "testpass123",
        Directory = "input/test-dynamic"
    };

    var response = await apiClient.PostAsJsonAsync("/api/servers", createRequest);
    response.EnsureSuccessStatusCode();

    var server = await response.Content.ReadFromJsonAsync<ServerInfo>();
    AnsiConsole.MarkupLine($"[green]Created server: {server!.Name}[/]");

    // 2. Wait for server to be ready
    await WaitForServerReady(apiClient, server.Name, timeout: TimeSpan.FromSeconds(60));

    // 3. Test connectivity
    var ftpClient = new AsyncFtpClient(server.Host, server.Username, server.Password, server.Port);
    await ftpClient.Connect();
    var connected = ftpClient.IsConnected;
    await ftpClient.Disconnect();

    AnsiConsole.MarkupLine(connected
        ? "[green]Connectivity test: PASS[/]"
        : "[red]Connectivity test: FAIL[/]");

    // 4. Delete server
    var deleteResponse = await apiClient.DeleteAsync($"/api/servers/{server.Name}");
    deleteResponse.EnsureSuccessStatusCode();

    AnsiConsole.MarkupLine($"[green]Deleted server: {server.Name}[/]");
}

private async Task WaitForServerReady(HttpClient client, string serverName, TimeSpan timeout)
{
    var deadline = DateTime.UtcNow.Add(timeout);

    while (DateTime.UtcNow < deadline)
    {
        var response = await client.GetAsync($"/api/servers/{serverName}");
        if (response.IsSuccessStatusCode)
        {
            var server = await response.Content.ReadFromJsonAsync<ServerInfo>();
            if (server?.Status == "Running")
            {
                return;
            }
        }

        await Task.Delay(2000);
    }

    throw new TimeoutException($"Server {serverName} not ready within {timeout}");
}
```

### Pattern 4: Playwright Page Object Model
**What:** Encapsulate UI locators and actions in page classes
**When to use:** All E2E tests to reduce duplication and improve maintainability
**Example:**
```csharp
// Source: Playwright best practices + React dashboard structure
public class DashboardPage
{
    private readonly IPage _page;

    public DashboardPage(IPage page)
    {
        _page = page;
    }

    // Locators using role-based selectors (best practice)
    private ILocator HeaderTitle => _page.GetByRole(AriaRole.Heading, new() { Name = "File Simulator Suite" });
    private ILocator ServersTab => _page.GetByRole(AriaRole.Tab, new() { Name = "Servers" });
    private ILocator FilesTab => _page.GetByRole(AriaRole.Tab, new() { Name = "Files" });
    private ILocator KafkaTab => _page.GetByRole(AriaRole.Tab, new() { Name = "Kafka" });
    private ILocator AlertsTab => _page.GetByRole(AriaRole.Tab, new() { Name = "Alerts" });
    private ILocator HistoryTab => _page.GetByRole(AriaRole.Tab, new() { Name = "History" });

    private ILocator ServerCards => _page.Locator(".server-card");
    private ILocator HealthIndicators => _page.Locator(".health-indicator");

    // Actions
    public async Task NavigateAsync(string baseUrl)
    {
        await _page.GotoAsync(baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task WaitForDashboardLoad()
    {
        // Wait for SignalR connection and initial data
        await HeaderTitle.WaitForAsync(new() { Timeout = 10_000 });
        await Expect(ServerCards.First()).ToBeVisibleAsync();
    }

    public async Task<int> GetServerCount()
    {
        return await ServerCards.CountAsync();
    }

    public async Task<Dictionary<string, string>> GetServerHealthStatuses()
    {
        var statuses = new Dictionary<string, string>();
        var count = await ServerCards.CountAsync();

        for (int i = 0; i < count; i++)
        {
            var card = ServerCards.Nth(i);
            var name = await card.Locator(".server-card__name").TextContentAsync();
            var health = await card.Locator(".health-indicator").GetAttributeAsync("data-status");

            statuses[name ?? ""] = health ?? "unknown";
        }

        return statuses;
    }

    public async Task SwitchToTab(string tabName)
    {
        await _page.GetByRole(AriaRole.Tab, new() { Name = tabName }).ClickAsync();
        await _page.WaitForTimeoutAsync(500); // Tab transition
    }

    public async Task VerifyAlertBanner(string expectedText)
    {
        var banner = _page.Locator(".alert-banner");
        await Expect(banner).ToBeVisibleAsync();
        await Expect(banner).ToContainTextAsync(expectedText);
    }
}
```

### Pattern 5: Playwright Test with Simulator Fixture
**What:** Start-Simulator.ps1 integration as test fixture with automatic cleanup
**When to use:** E2E tests requiring full simulator environment
**Example:**
```csharp
// Source: Playwright fixtures + Deploy-Production.ps1 patterns
public class SimulatorTestFixture : IAsyncLifetime
{
    private Process? _simulatorProcess;
    public string DashboardUrl { get; private set; } = "http://localhost:3000";
    public string ApiUrl { get; private set; } = "http://localhost:5000";

    public async Task InitializeAsync()
    {
        // Start simulator using Start-Simulator.ps1 (future script)
        var scriptPath = Path.Combine(GetRepoRoot(), "scripts", "Start-Simulator.ps1");

        _simulatorProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"-File \"{scriptPath}\" -SkipBrowser",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        _simulatorProcess.Start();

        // Wait for services to be ready
        await WaitForServiceHealthy(ApiUrl, timeout: TimeSpan.FromMinutes(5));
        await WaitForServiceHealthy(DashboardUrl, timeout: TimeSpan.FromMinutes(2));
    }

    public async Task DisposeAsync()
    {
        // Cleanup: Stop simulator
        if (_simulatorProcess != null && !_simulatorProcess.HasExited)
        {
            _simulatorProcess.Kill(entireProcessTree: true);
            await _simulatorProcess.WaitForExitAsync();
            _simulatorProcess.Dispose();
        }
    }

    private async Task WaitForServiceHealthy(string url, TimeSpan timeout)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync($"{url}/api/health");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Service not ready yet
            }

            await Task.Delay(5000);
        }

        throw new TimeoutException($"Service at {url} not healthy within {timeout}");
    }

    private string GetRepoRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null && !Directory.Exists(Path.Combine(current, ".git")))
        {
            current = Directory.GetParent(current)?.FullName;
        }
        return current ?? throw new InvalidOperationException("Could not find repo root");
    }
}

// Usage in test class
public class DashboardE2ETests : IClassFixture<SimulatorTestFixture>
{
    private readonly SimulatorTestFixture _fixture;

    public DashboardE2ETests(SimulatorTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Dashboard_Loads_And_Shows_Servers()
    {
        // Playwright test setup
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();

        var dashboard = new DashboardPage(page);

        // Test
        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoad();

        var serverCount = await dashboard.GetServerCount();
        serverCount.Should().BeGreaterThan(0);

        // Cleanup
        await browser.CloseAsync();
    }
}
```

### Pattern 6: WebApplicationFactory for API Integration Tests
**What:** In-memory API testing without network overhead
**When to use:** Testing Control API endpoints with realistic request/response cycle
**Example:**
```csharp
// Source: Microsoft.AspNetCore.Mvc.Testing official docs + WebApplicationFactory patterns
public class ApiTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real database with in-memory
            var dbDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<SimulatorDbContext>));

            if (dbDescriptor != null)
            {
                services.Remove(dbDescriptor);
            }

            services.AddDbContext<SimulatorDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb");
            });

            // Replace Kubernetes client with mock
            var k8sDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IKubernetes));

            if (k8sDescriptor != null)
            {
                services.Remove(k8sDescriptor);
            }

            services.AddSingleton<IKubernetes>(new Mock<IKubernetes>().Object);
        });
    }
}

// Usage in tests
public class ConnectionInfoEndpointTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _factory;
    private readonly HttpClient _client;

    public ConnectionInfoEndpointTests(ApiTestFixture factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetConnectionInfo_Returns_AllServers()
    {
        // Act
        var response = await _client.GetAsync("/api/connection-info");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var connectionInfo = JsonSerializer.Deserialize<ConnectionInfo>(json);

        connectionInfo.Should().NotBeNull();
        connectionInfo!.Servers.Should().NotBeEmpty();
        connectionInfo.Servers.Should().Contain(s => s.Protocol == "FTP");
        connectionInfo.Servers.Should().Contain(s => s.Protocol == "NFS");
    }

    [Fact]
    public async Task GetConnectionInfo_EnvFormat_Returns_EnvironmentVariables()
    {
        // Act
        var response = await _client.GetAsync("/api/connection-info?format=env");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("FILE_FTP_HOST=");
        content.Should().Contain("FILE_FTP_PORT=");
    }
}
```

### Pattern 7: Testcontainers for Kafka Integration Tests
**What:** Docker-based Kafka instance for realistic integration testing
**When to use:** Testing Kafka produce/consume without external dependencies
**Example:**
```csharp
// Source: Testcontainers docs + Confluent.Kafka testing patterns
public class KafkaIntegrationTests : IAsyncLifetime
{
    private KafkaContainer? _kafkaContainer;
    private string? _bootstrapServers;

    public async Task InitializeAsync()
    {
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.6.0")
            .Build();

        await _kafkaContainer.StartAsync();
        _bootstrapServers = _kafkaContainer.GetBootstrapAddress();
    }

    public async Task DisposeAsync()
    {
        if (_kafkaContainer != null)
        {
            await _kafkaContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task TestConsole_CanProduceAndConsumeMessages()
    {
        // Arrange
        var topicName = "test-topic";
        var testMessage = new { Key = "test-key", Value = "test-value" };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _bootstrapServers
        };

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = "test-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        // Act - Produce
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
        var produceResult = await producer.ProduceAsync(topicName,
            new Message<string, string>
            {
                Key = testMessage.Key,
                Value = testMessage.Value
            });

        // Act - Consume
        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(topicName);

        var consumeResult = consumer.Consume(TimeSpan.FromSeconds(10));

        // Assert
        produceResult.Status.Should().Be(PersistenceStatus.Persisted);
        consumeResult.Should().NotBeNull();
        consumeResult.Message.Key.Should().Be(testMessage.Key);
        consumeResult.Message.Value.Should().Be(testMessage.Value);
    }
}
```

### Anti-Patterns to Avoid
- **Hardcoded sleep/waits in tests:** Use Playwright's auto-waiting and WebApplicationFactory's built-in readiness checks instead
- **Testing implementation details:** Test user behavior and API contracts, not internal component state
- **Brittle CSS selectors:** Use role-based selectors (getByRole, getByLabel) that survive UI changes
- **Shared test state:** Each test should be isolated with its own database state/Kafka topics
- **Manual changelog updates:** Use conventional commits and automated tools to prevent inconsistencies
- **Testing multiple concerns in one test:** Keep tests focused on single user journey or API operation

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Browser automation | Custom Selenium wrapper | Microsoft.Playwright 1.58.0 | Auto-waiting, cross-browser, better debugging, official Microsoft support |
| In-memory API testing | Custom test server | WebApplicationFactory | Built-in with ASP.NET Core, handles DI/configuration, no network overhead |
| Test data cleanup | Manual database reset | Respawn library | Handles foreign keys, identity reseeding, performance optimized |
| Wait conditions | Thread.Sleep() loops | Playwright's expect() assertions | Automatic retry, configurable timeout, descriptive failures |
| Changelog generation | Manual markdown editing | conventional-changelog | Parses git history, semantic versioning, consistent format |
| Release versioning | Manual git tagging | semantic-release | Analyzes commits, determines version bump, creates tag/release |
| Kafka test setup | Manual Docker commands | Testcontainers | Lifecycle management, port discovery, automatic cleanup |
| Page object boilerplate | Copy-paste classes | Playwright code generation | `playwright codegen` generates selectors and interactions |

**Key insight:** Testing infrastructure is complex with edge cases around timing, cleanup, and isolation. Playwright handles browser quirks automatically, WebApplicationFactory manages service lifecycle correctly, and Testcontainers ensures proper Docker cleanup. Manual implementations miss these edge cases and create flaky tests.

## Common Pitfalls

### Pitfall 1: Flaky Tests Due to Timing Issues
**What goes wrong:** Tests pass locally but fail in CI due to race conditions, network delays, or slow browser rendering
**Why it happens:** Using fixed waits (Task.Delay) instead of condition-based waiting
**How to avoid:**
- Use Playwright's `Expect()` assertions which retry automatically (default 5s timeout)
- Use `WaitForLoadStateAsync(LoadState.NetworkIdle)` after navigation
- Use `WaitForAsync()` on locators before interaction
- For API tests, use `WebApplicationFactory` which waits for server readiness
**Warning signs:** Tests fail intermittently, adding more delays "fixes" tests, tests fail only in CI

### Pitfall 2: Selector Brittleness in E2E Tests
**What goes wrong:** Tests break when UI changes even though functionality is identical
**Why it happens:** Using CSS class selectors (.btn-primary) or XPath that couple tests to implementation
**How to avoid:**
- Use role-based selectors: `GetByRole(AriaRole.Button, new() { Name = "Submit" })`
- Use label-based selectors: `GetByLabel("Email address")`
- Add `data-testid` attributes only when role/label selectors aren't available
- Avoid CSS classes that are styling-focused
**Warning signs:** Every UI refactor breaks tests, selectors have multiple classes, XPath uses indices

### Pitfall 3: Incomplete Test Isolation
**What goes wrong:** Tests pass when run individually but fail when run in parallel or in different order
**Why it happens:** Shared state between tests (database, Kafka topics, file system)
**How to avoid:**
- Use `IAsyncLifetime` for per-test setup/cleanup
- Use Respawn to reset database state between tests
- Create unique Kafka topics per test: `$"test-topic-{Guid.NewGuid()}"`
- Clean up files created during tests in `DisposeAsync()`
- Use WebApplicationFactory per test class (IClassFixture)
**Warning signs:** Tests fail when run in parallel, test order affects results, cleanup happens manually

### Pitfall 4: Over-Mocking in Integration Tests
**What goes wrong:** Integration tests pass but real system fails because critical components were mocked
**Why it happens:** Mocking databases, external APIs, or infrastructure that should be tested together
**How to avoid:**
- Use Testcontainers for real Kafka instead of mocked producer/consumer
- Use in-memory SQLite database instead of mocked DbContext
- Only mock external dependencies you don't control (external APIs, hardware)
- WebApplicationFactory should use real services except external dependencies
**Warning signs:** Integration tests pass but deployment fails, mocked services configured differently than production

### Pitfall 5: Changelog Inconsistency
**What goes wrong:** Changelog entries have different formats, missing features, or incorrect categorization
**Why it happens:** Manual changelog updates by different developers with different standards
**How to avoid:**
- Use conventional commits format: `feat:`, `fix:`, `docs:`, `chore:`, `breaking:`
- Set up commit message linting (commitlint) to enforce format
- Use conventional-changelog to auto-generate from commits
- Review generated changelog before release to add context
**Warning signs:** Changelog has inconsistent formatting, features missing from changelog, unclear version increments

### Pitfall 6: Playwright Browser Installation Issues
**What goes wrong:** Tests fail with "browser executable not found" or version mismatch errors
**Why it happens:** Playwright browsers not installed or version mismatch between package and browsers
**How to avoid:**
- Run `pwsh bin/Debug/net9.0/playwright.ps1 install` after adding Playwright package
- Install browsers in CI: `- run: pwsh ./tests/*/bin/Debug/net9.0/playwright.ps1 install`
- Pin Playwright version in csproj to ensure consistency
- Use Playwright Docker image for CI: `mcr.microsoft.com/playwright/dotnet:v1.58.0`
**Warning signs:** Tests work locally but fail in CI, "browser not found" errors, version mismatch warnings

### Pitfall 7: TestConsole Configuration Fallback Logic
**What goes wrong:** TestConsole silently falls back to hardcoded config when API is unavailable, hiding issues
**Why it happens:** Insufficient error handling or logging when API fetch fails
**How to avoid:**
- Log clear warnings when API is unavailable: "Control API not reachable, using local config"
- Add command-line flag `--require-api` to fail fast if API is required
- Test both code paths (API-driven and config-driven) separately
- Display configuration source in test output
**Warning signs:** Tests pass but use wrong configuration, silent failures, tests can't reproduce production issues

## Code Examples

Verified patterns from official sources:

### Example 1: Playwright Test with Page Object
```csharp
// Source: https://playwright.dev/dotnet/docs/best-practices
// Purpose: E2E test of server creation workflow
[TestClass]
public class ServerManagementTests : PageTest
{
    private DashboardPage _dashboard = null!;
    private ServersPage _serversPage = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _dashboard = new DashboardPage(Page);
        _serversPage = new ServersPage(Page);

        await _dashboard.NavigateAsync("http://localhost:3000");
        await _dashboard.WaitForDashboardLoad();
        await _dashboard.SwitchToTab("Servers");
    }

    [TestMethod]
    public async Task CreateDynamicServer_ShowsInServerList()
    {
        // Arrange
        var serverName = $"test-ftp-{DateTime.UtcNow:yyyyMMddHHmmss}";

        // Act
        await _serversPage.OpenCreateServerDialog();
        await _serversPage.FillServerDetails(new()
        {
            Name = serverName,
            Protocol = "FTP",
            Username = "testuser",
            Password = "testpass123"
        });
        await _serversPage.SubmitServerCreation();

        // Wait for creation to complete
        await _serversPage.WaitForServerInList(serverName, timeout: 60_000);

        // Assert
        var servers = await _serversPage.GetAllServerNames();
        servers.Should().Contain(serverName);

        var status = await _serversPage.GetServerStatus(serverName);
        status.Should().Be("Running");
    }

    [TestMethod]
    public async Task FileUpload_AppearsInFileList()
    {
        // Arrange
        await _dashboard.SwitchToTab("Files");
        var filesPage = new FileOperationsPage(Page);

        var testFilePath = Path.Combine(Path.GetTempPath(), "test-upload.txt");
        await File.WriteAllTextAsync(testFilePath, "Test content");

        // Act
        await filesPage.UploadFile(testFilePath, targetDirectory: "/input");

        // Assert
        await filesPage.WaitForFileInList("test-upload.txt", timeout: 10_000);
        var files = await filesPage.GetFilesInDirectory("/input");
        files.Should().Contain("test-upload.txt");

        // Cleanup
        File.Delete(testFilePath);
    }
}
```

### Example 2: API Integration Test with WebApplicationFactory
```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
// Purpose: Test Control API connection-info endpoint
public class ConnectionInfoApiTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;

    public ConnectionInfoApiTests(ApiTestFixture factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetConnectionInfo_ReturnsJsonFormat_ByDefault()
    {
        // Act
        var response = await _client.GetAsync("/api/connection-info");

        // Assert
        response.Should().BeSuccessful();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync();
        var connectionInfo = JsonSerializer.Deserialize<ConnectionInfo>(json);

        connectionInfo.Should().NotBeNull();
        connectionInfo!.Servers.Should().NotBeEmpty();
        connectionInfo.Servers.Should().AllSatisfy(s =>
        {
            s.Name.Should().NotBeNullOrEmpty();
            s.Protocol.Should().NotBeNullOrEmpty();
            s.Host.Should().NotBeNullOrEmpty();
            s.Port.Should().BeGreaterThan(0);
        });
    }

    [Theory]
    [InlineData("env", "FILE_FTP_HOST=")]
    [InlineData("yaml", "servers:")]
    [InlineData("dotnet", "\"FileSimulator\":")]
    public async Task GetConnectionInfo_ReturnsCorrectFormat(string format, string expectedContent)
    {
        // Act
        var response = await _client.GetAsync($"/api/connection-info?format={format}");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(expectedContent);
    }
}
```

### Example 3: Kafka Integration Test with Testcontainers
```csharp
// Source: https://docs.confluent.io/cloud/current/client-apps/testing.html
// Purpose: Test Kafka produce/consume cycle
public class KafkaIntegrationTests : IAsyncLifetime
{
    private KafkaContainer _kafkaContainer = null!;
    private string _bootstrapServers = null!;

    public async Task InitializeAsync()
    {
        _kafkaContainer = new KafkaBuilder().Build();
        await _kafkaContainer.StartAsync();
        _bootstrapServers = _kafkaContainer.GetBootstrapAddress();
    }

    public async Task DisposeAsync()
    {
        await _kafkaContainer.DisposeAsync();
    }

    [Fact]
    public async Task ProduceAndConsume_WorksCorrectly()
    {
        // Arrange
        var topicName = $"test-topic-{Guid.NewGuid()}";
        var testKey = "test-key";
        var testValue = "test-value";

        // Produce
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _bootstrapServers,
            EnableIdempotence = true
        };

        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
        var produceResult = await producer.ProduceAsync(topicName,
            new Message<string, string> { Key = testKey, Value = testValue });

        // Assert produce
        produceResult.Status.Should().Be(PersistenceStatus.Persisted);

        // Consume
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = $"test-group-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(topicName);

        var consumeResult = consumer.Consume(TimeSpan.FromSeconds(10));

        // Assert consume
        consumeResult.Should().NotBeNull();
        consumeResult.Message.Key.Should().Be(testKey);
        consumeResult.Message.Value.Should().Be(testValue);
        consumeResult.Topic.Should().Be(topicName);
    }
}
```

### Example 4: TestConsole API Configuration Provider
```csharp
// Source: Project requirements + existing TestConsole patterns
// Purpose: Fetch configuration from Control API dynamically
public class ApiConfigurationProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _fallbackConfig;
    private readonly ILogger _logger;

    public ApiConfigurationProvider(
        string apiBaseUrl,
        IConfiguration fallbackConfig,
        ILogger logger)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
        _fallbackConfig = fallbackConfig;
        _logger = logger;
    }

    public async Task<TestConfiguration> GetConfigurationAsync(CancellationToken ct = default)
    {
        try
        {
            // Try to fetch from API
            var connectionInfo = await FetchFromApiAsync(ct);
            if (connectionInfo != null)
            {
                _logger.LogInformation(
                    "Using API-driven configuration with {Count} servers",
                    connectionInfo.Servers.Count);
                return MapToTestConfiguration(connectionInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch configuration from API, using fallback");
        }

        // Fallback to appsettings.json
        _logger.LogInformation("Using fallback configuration from appsettings.json");
        return MapFromAppSettings(_fallbackConfig);
    }

    private async Task<ConnectionInfo?> FetchFromApiAsync(CancellationToken ct)
    {
        // Health check first
        var healthResponse = await _httpClient.GetAsync("/api/health", ct);
        if (!healthResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Control API health check failed");
            return null;
        }

        // Fetch connection info
        var response = await _httpClient.GetAsync("/api/connection-info", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<ConnectionInfo>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private TestConfiguration MapToTestConfiguration(ConnectionInfo connectionInfo)
    {
        var config = new TestConfiguration
        {
            Source = ConfigurationSource.Api,
            Servers = new Dictionary<string, ServerConfig>()
        };

        foreach (var server in connectionInfo.Servers)
        {
            config.Servers[server.Protocol] = new ServerConfig
            {
                Name = server.Name,
                Host = server.Host,
                Port = server.Port,
                Username = server.Username,
                Password = server.Password,
                Type = server.Type,
                Status = server.Status
            };
        }

        return config;
    }

    private TestConfiguration MapFromAppSettings(IConfiguration config)
    {
        var testConfig = new TestConfiguration
        {
            Source = ConfigurationSource.AppSettings,
            Servers = new Dictionary<string, ServerConfig>()
        };

        // Map existing appsettings.json structure
        var ftpConfig = config.GetSection("FileSimulator:Ftp");
        if (ftpConfig.Exists())
        {
            testConfig.Servers["FTP"] = new ServerConfig
            {
                Host = ftpConfig["Host"] ?? "localhost",
                Port = int.Parse(ftpConfig["Port"] ?? "30021"),
                Username = ftpConfig["Username"] ?? "ftpuser",
                Password = ftpConfig["Password"] ?? "ftppass123"
            };
        }

        // ... repeat for other protocols

        return testConfig;
    }
}

public enum ConfigurationSource
{
    Api,
    AppSettings
}

public class TestConfiguration
{
    public ConfigurationSource Source { get; set; }
    public Dictionary<string, ServerConfig> Servers { get; set; } = new();
}

public class ServerConfig
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hardcoded IPs in appsettings | Dynamic fetch from /api/connection-info | Phase 13 | TestConsole adapts to any deployment without config changes |
| Testing 1 NAS server | Testing 7 NAS servers (input-1/2/3, backup, output-1/2/3) | v1.0 (Jan 2026) | Production-identical topology testing |
| Manual server creation for tests | Dynamic server lifecycle via API | Phase 11 | End-to-end testing of server management features |
| No E2E testing | Playwright browser automation | Phase 13 | Catch UI regressions before deployment |
| Manual releases | Automated semantic versioning | Phase 13 | Consistent releases, automated changelog |
| Selenium WebDriver | Playwright | 2023-2024 | Auto-waiting reduces flake, simpler API, cross-browser |
| Custom test servers | WebApplicationFactory | ASP.NET Core 2.1+ | In-memory testing, no ports/network issues |
| Manual Kafka setup | Testcontainers | 2022-2024 | Reproducible integration tests, automatic cleanup |

**Deprecated/outdated:**
- Selenium with explicit waits: Replaced by Playwright's auto-waiting (2023+)
- Manual database cleanup: Replaced by Respawn library (current standard)
- GitHub Releases UI for publishing: Replaced by GitHub CLI and Actions (2021+)
- Manual changelog maintenance: Replaced by conventional-changelog automation (2020+)

## Open Questions

Things that couldn't be fully resolved:

1. **Start-Simulator.ps1 Script Scope**
   - What we know: Need automated way to start full simulator for E2E tests
   - What's unclear: Should it replace Deploy-Production.ps1 or complement it? Build images locally or pull from registry?
   - Recommendation: Create Start-Simulator.ps1 as wrapper around Deploy-Production.ps1 with `-Wait` parameter and browser auto-open

2. **E2E Test Execution Time**
   - What we know: Full simulator deployment takes ~5 minutes (Phase 12-07)
   - What's unclear: Can E2E tests reuse running simulator or require fresh deployment per test run?
   - Recommendation: Add IClassFixture that starts simulator once per test class, configure via environment variable for CI (use existing instance) vs local (start fresh)

3. **TestConsole as Kubernetes Job**
   - What we know: TestConsole currently runs on Windows host
   - What's unclear: Should TestConsole be deployable as Kubernetes Job for in-cluster testing?
   - Recommendation: Phase 13 focus on local execution, defer Kubernetes Job deployment to future enhancement (v2.1)

4. **Changelog Section Organization**
   - What we know: Conventional commits define types (feat, fix, docs, etc.)
   - What's unclear: Custom sections for v2.0 features (API, Dashboard, NAS, Kafka)?
   - Recommendation: Use standard sections (Features, Bug Fixes, Breaking Changes) with commit scopes for grouping: `feat(api)`, `feat(dashboard)`, `feat(kafka)`

5. **Release Automation Trigger**
   - What we know: Manual release creation for v2.0 milestone
   - What's unclear: Future releases automated on main branch push or manual dispatch?
   - Recommendation: Phase 13 manual release, Phase 14+ setup GitHub Action with workflow_dispatch for controlled releases

## Sources

### Primary (HIGH confidence)
- Microsoft.Playwright NuGet 1.58.0 (released 2026-02-02) - https://www.nuget.org/packages/Microsoft.Playwright
- Playwright official docs - https://playwright.dev/docs/best-practices
- Microsoft ASP.NET Core integration testing docs - https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
- Confluent Kafka testing docs - https://docs.confluent.io/cloud/current/client-apps/testing.html
- GitHub Actions release automation - https://docs.github.com/en/actions/sharing-automations/creating-actions/releasing-and-maintaining-actions

### Secondary (MEDIUM confidence)
- BrowserStack Playwright best practices 2026 - https://www.browserstack.com/guide/playwright-best-practices
- Code Maze ASP.NET Core integration testing - https://code-maze.com/aspnet-core-integration-testing/
- Tim Deschryver's Web API testing guide - https://timdeschryver.dev/blog/how-to-test-your-csharp-web-api
- Testcontainers for Kafka - GitHub issue #1023 confluentinc/confluent-kafka-dotnet
- semantic-release GitHub - https://github.com/semantic-release/semantic-release
- Conventional Commits spec - https://www.conventionalcommits.org/en/v1.0.0/

### Tertiary (LOW confidence - WebSearch only)
- DeviQA E2E testing guide 2026 - https://www.deviqa.com/blog/guide-to-playwright-end-to-end-testing-in-2025/
- Medium articles on testing strategies - Various authors

### Project Context (HIGH confidence)
- Existing TestConsole implementation: C:\Users\UserC\source\repos\file-simulator-suite\src\FileSimulator.TestConsole\Program.cs
- Control API project: FileSimulator.ControlApi.csproj with .NET 9, KubernetesClient 18.0.13, Confluent.Kafka 2.12.0
- Phase 12 production readiness completed with Deploy-Production.ps1
- v1.0 Multi-NAS topology with 7 servers operational

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Official packages from NuGet.org, versions verified for Feb 2026
- Architecture patterns: HIGH - Based on official docs (Playwright, Microsoft), validated project structure
- Pitfalls: HIGH - Common issues documented in official guides and project experience
- Release automation: MEDIUM - Best practices from GitHub docs, semantic-release is standard but project-specific trigger strategy needs validation

**Research date:** 2026-02-05
**Valid until:** 2026-03-05 (30 days - stable technologies, but Playwright updates frequently)
