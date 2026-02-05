# Testing Guide

This document provides comprehensive guidance for testing the File Simulator Suite.

## Table of Contents

- [Overview](#overview)
- [TestConsole](#testconsole)
  - [Installation](#installation)
  - [Command-Line Reference](#command-line-reference)
  - [Test Modes](#test-modes)
  - [Configuration](#configuration)
- [E2E Tests](#e2e-tests)
  - [Setup](#setup)
  - [Running Tests](#running-tests)
  - [Test Structure](#test-structure)
  - [Page Objects](#page-objects)
  - [Writing New Tests](#writing-new-tests)
- [Multi-NAS Test Suite](#multi-nas-test-suite)
- [CI Integration](#ci-integration)
- [Troubleshooting](#troubleshooting)

---

## Overview

The File Simulator Suite includes three testing approaches:

| Test Type | Tool | Purpose |
|-----------|------|---------|
| Protocol Testing | TestConsole | Validates FTP, SFTP, HTTP, S3, SMB, NFS, Kafka connectivity |
| UI Testing | Playwright E2E | Validates dashboard functionality in browser |
| Infrastructure Testing | PowerShell Scripts | Validates Multi-NAS topology and deployment |

---

## TestConsole

The TestConsole is a .NET CLI application that tests all simulator protocols and Control API integrations.

### Installation

```powershell
cd src/FileSimulator.TestConsole
dotnet build
```

### Command-Line Reference

```powershell
# Basic usage (runs all protocol tests)
dotnet run

# With specific API URL
dotnet run -- --api-url http://file-simulator.local:30500

# Require API (fail if Control API unavailable)
dotnet run -- --require-api

# Combined options
dotnet run -- --api-url http://localhost:5000 --require-api
```

### Test Modes

#### Default Mode (All Protocols)

Tests FTP, SFTP, HTTP, WebDAV, S3, SMB, NFS connectivity with file upload/download operations.

```powershell
dotnet run
```

Output shows a colorful table with pass/fail status for each protocol.

#### NAS-Only Mode

Tests only the 7 Multi-NAS servers (nas-input-1/2/3, nas-backup, nas-output-1/2/3).

```powershell
dotnet run -- --nas-only
```

#### Kafka Mode

Tests Kafka broker connectivity, topic operations, and message produce/consume cycle.

```powershell
dotnet run -- --kafka
```

**What it tests:**
1. Kafka broker health check
2. Create a test topic
3. Produce a test message
4. Consume and verify the message
5. Delete the test topic

#### Dynamic Server Mode

Tests dynamic server creation, health verification, and cleanup.

```powershell
# Basic dynamic server tests (connectivity only)
dotnet run -- --dynamic

# Full dynamic server tests (includes file operations)
dotnet run -- --dynamic --full-dynamic-test
```

**What it tests:**
1. Create dynamic FTP server via API
2. Wait for server readiness (polling with timeout)
3. Verify TCP connectivity to assigned NodePort
4. (With --full-dynamic-test) Upload/download file via dynamic server
5. Delete dynamic server and verify cleanup

**Warning:** Dynamic tests modify cluster state by creating Kubernetes resources.

#### Cross-Protocol Mode

Tests that files written via one protocol are visible via others.

```powershell
dotnet run -- --cross-protocol
# or
dotnet run -- -x
```

### Configuration

The TestConsole uses a layered configuration approach:

1. **API Configuration** (preferred): Fetches settings from `/api/connection-info`
2. **appsettings.json**: Fallback when API unavailable
3. **Environment variables**: Override any setting
4. **Command-line arguments**: Highest priority

#### appsettings.json

```json
{
  "ControlApi": {
    "BaseUrl": "http://file-simulator.local:30500",
    "RequireApi": false
  },
  "FileSimulator": {
    "Ftp": {
      "Host": "file-simulator.local",
      "Port": 30021,
      "Username": "ftpuser",
      "Password": "ftppass123"
    },
    "Sftp": {
      "Host": "file-simulator.local",
      "Port": 30022,
      "Username": "sftpuser",
      "Password": "sftppass123"
    }
    // ... other protocols
  }
}
```

---

## E2E Tests

Browser-based tests using Playwright for dashboard validation.

### Setup

#### 1. Install Playwright Browsers

```powershell
# Run the installation script
pwsh .\scripts\Install-PlaywrightBrowsers.ps1

# Or install manually
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium
```

#### 2. Configure Test Settings

Edit `tests/FileSimulator.E2ETests/playwright.settings.json`:

```json
{
  "Dashboard": {
    "BaseUrl": "http://localhost:3000",
    "ApiUrl": "http://localhost:5000"
  },
  "Playwright": {
    "Headless": true,
    "SlowMo": 0,
    "Timeout": 30000,
    "BrowserType": "chromium"
  },
  "Simulator": {
    "StartScript": "../../scripts/Start-Simulator.ps1",
    "StartupTimeout": 300000,
    "UseExistingInstance": false
  }
}
```

### Running Tests

#### Against Existing Simulator

```powershell
cd tests/FileSimulator.E2ETests

# Set environment variable to use existing simulator
$env:USE_EXISTING_SIMULATOR = "true"

# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~DashboardTests"
dotnet test --filter "FullyQualifiedName~ServerManagementTests"
dotnet test --filter "FullyQualifiedName~FileOperationsTests"
dotnet test --filter "FullyQualifiedName~KafkaTests"
dotnet test --filter "FullyQualifiedName~AlertsTests"
dotnet test --filter "FullyQualifiedName~HistoryTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~DashboardTests.Dashboard_Should_Load_And_Show_Servers"
```

#### With Automatic Startup

Remove the environment variable to let tests start the simulator:

```powershell
cd tests/FileSimulator.E2ETests
$env:USE_EXISTING_SIMULATOR = $null
dotnet test
```

The test fixture will:
1. Run `Start-Simulator.ps1 -Wait`
2. Wait for API and dashboard health
3. Run tests
4. Clean up processes on completion

#### Debug Mode (Headed Browser)

```powershell
$env:PWDEBUG = "1"
dotnet test --filter "FullyQualifiedName~DashboardTests"
```

This opens a visible browser window and pauses at each step.

### Test Structure

```
tests/FileSimulator.E2ETests/
|-- Fixtures/
|   +-- SimulatorTestFixture.cs    # xUnit IAsyncLifetime fixture
|
|-- PageObjects/
|   |-- DashboardPage.cs           # Base dashboard with tab navigation
|   |-- ServersPage.cs             # Servers tab interactions
|   |-- FilesPage.cs               # Files tab interactions
|   |-- KafkaPage.cs               # Kafka tab interactions
|   |-- AlertsPage.cs              # Alerts tab interactions
|   +-- HistoryPage.cs             # History tab interactions
|
|-- Support/
|   +-- WaitHelpers.cs             # Async wait utilities
|
|-- Tests/
|   |-- SmokeTests.cs              # Basic connectivity tests
|   |-- DashboardTests.cs          # Tab navigation, layout
|   |-- ServerManagementTests.cs   # Dynamic server CRUD
|   |-- FileOperationsTests.cs     # File upload/download/delete
|   |-- KafkaTests.cs              # Topic and message operations
|   |-- AlertsTests.cs             # Alert display and filtering
|   +-- HistoryTests.cs            # Metrics charts
|
+-- SimulatorTestCollection.cs     # xUnit collection definition
```

### Page Objects

Each tab has a corresponding Page Object with methods for common operations.

#### Example: ServersPage

```csharp
public class ServersPage
{
    private readonly IPage _page;

    public ServersPage(IPage page) => _page = page;

    public async Task NavigateAsync()
    {
        await _page.GetByRole(AriaRole.Tab, new() { Name = "Servers" }).ClickAsync();
        await _page.WaitForSelectorAsync("[data-testid='servers-grid']");
    }

    public async Task<int> GetServerCountAsync()
    {
        var cards = await _page.QuerySelectorAllAsync("[data-testid='server-card']");
        return cards.Count;
    }

    public async Task ClickCreateServerAsync()
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Create Server" }).ClickAsync();
    }

    public async Task<IReadOnlyList<string>> GetServerNamesAsync()
    {
        var names = await _page.Locator("[data-testid='server-name']").AllTextContentsAsync();
        return names;
    }
}
```

### Writing New Tests

#### 1. Create Test Class

```csharp
using FileSimulator.E2ETests.Fixtures;
using FileSimulator.E2ETests.PageObjects;
using Xunit;

namespace FileSimulator.E2ETests.Tests;

[Collection("Simulator")]
public class MyFeatureTests
{
    private readonly SimulatorTestFixture _fixture;

    public MyFeatureTests(SimulatorTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyFeature_Should_DoSomething()
    {
        // Arrange
        var page = await _fixture.Context.NewPageAsync();
        await page.GotoAsync(_fixture.DashboardUrl);

        var dashboard = new DashboardPage(page);
        var myPage = new MyFeaturePage(page);

        // Act
        await myPage.NavigateAsync();
        await myPage.PerformActionAsync();

        // Assert
        var result = await myPage.GetResultAsync();
        Assert.NotEmpty(result);

        // Cleanup
        await page.CloseAsync();
    }
}
```

#### 2. Follow Best Practices

- Use `GetByRole()` selectors for accessibility
- Add `data-testid` attributes for complex elements
- Use try/finally for cleanup of created resources
- Use flexible assertions (`NotBeEmpty` vs `HaveCount`) for varying state
- Keep tests independent (no shared state between tests)

---

## Multi-NAS Test Suite

PowerShell script for validating the 7-server NAS topology.

```powershell
# Full test suite (57 tests)
.\scripts\test-multi-nas.ps1

# Skip slow persistence tests
.\scripts\test-multi-nas.ps1 -SkipPersistenceTests

# Verbose output
.\scripts\test-multi-nas.ps1 -Verbose
```

### Test Categories

| Category | Tests | Description |
|----------|-------|-------------|
| Health | 7 | Each NAS server responds to NFS mount |
| Isolation | 21 | Files on one NAS not visible on others |
| Sync | 10 | Bidirectional Windows sync timing |
| Persistence | 19 | Files survive pod restart |

---

## CI Integration

### GitHub Actions Example

```yaml
name: E2E Tests

on: [push, pull_request]

jobs:
  e2e:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Install Playwright
        run: |
          dotnet tool install --global Microsoft.Playwright.CLI
          playwright install chromium

      - name: Start Minikube
        run: |
          minikube start --profile file-simulator --driver=hyperv --memory=12288

      - name: Deploy Simulator
        run: |
          helm upgrade --install file-sim ./helm-chart/file-simulator `
            --kube-context=file-simulator `
            --namespace file-simulator `
            --create-namespace

      - name: Wait for Ready
        run: |
          kubectl --context=file-simulator wait --for=condition=available `
            deployment --all -n file-simulator --timeout=300s

      - name: Run E2E Tests
        run: |
          cd tests/FileSimulator.E2ETests
          $env:USE_EXISTING_SIMULATOR = "true"
          dotnet test --logger "trx;LogFileName=results.trx"

      - name: Upload Results
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: tests/FileSimulator.E2ETests/TestResults/
```

---

## Troubleshooting

### TestConsole

**Error: "Control API unavailable"**
- Check API is running: `curl http://file-simulator.local:30500/api/health`
- Verify port-forwarding or NodePort access
- Use `--api-url` to specify correct URL

**Error: "Connection refused" for protocol**
- Verify pods are running: `kubectl --context=file-simulator get pods -n file-simulator`
- Check NodePort assignment: `kubectl --context=file-simulator get svc -n file-simulator`
- Verify Minikube tunnel for SMB

### E2E Tests

**Error: "Playwright browsers not installed"**
```powershell
pwsh .\scripts\Install-PlaywrightBrowsers.ps1
```

**Tests timeout waiting for dashboard**
- Increase `StartupTimeout` in playwright.settings.json
- Check dashboard is accessible: `curl http://localhost:3000`
- Verify React dev server is running

**Element not found errors**
- Add waits: `await page.WaitForSelectorAsync("[data-testid='element']")`
- Check for dynamic content loading
- Use `PWDEBUG=1` to debug visually

### Multi-NAS Tests

**Tests fail with "pod not found"**
- Ensure Multi-NAS deployment: `helm upgrade ... -f values-multi-nas.yaml`
- Check all 7 NAS pods: `kubectl get pods -l simulator.protocol=nfs`

**Sync tests fail (>60s)**
- Check sidecar logs: `kubectl logs <pod> -c sync-sidecar`
- Verify Windows mount: `ls C:\simulator-data\nas-output-1`
