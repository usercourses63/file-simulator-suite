#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Install Playwright browsers for E2E testing

.DESCRIPTION
    Builds the E2E test project and installs the required Playwright browsers.
    This is a one-time setup step before running E2E tests.

.EXAMPLE
    .\Install-PlaywrightBrowsers.ps1

.NOTES
    Requires .NET 9 SDK and PowerShell 7+
#>

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
$projectDir = Join-Path $repoRoot "tests\FileSimulator.E2ETests"

Write-Host "Installing Playwright browsers for E2E testing..." -ForegroundColor Cyan
Write-Host ""

# Navigate to project directory
Push-Location $projectDir

try {
    # Build project to generate playwright.ps1
    Write-Host "Building E2E test project..." -ForegroundColor Yellow
    dotnet build

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build E2E test project"
    }

    Write-Host ""
    Write-Host "Installing Playwright browsers..." -ForegroundColor Yellow

    # Find playwright.ps1 script
    $playwrightScript = Join-Path $projectDir "bin\Debug\net9.0\playwright.ps1"

    if (-not (Test-Path $playwrightScript)) {
        throw "playwright.ps1 not found at: $playwrightScript. Build the project first."
    }

    # Run browser installation
    & pwsh $playwrightScript install

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install Playwright browsers"
    }

    Write-Host ""
    Write-Host "Playwright browsers installed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now run E2E tests with:" -ForegroundColor Cyan
    Write-Host "  dotnet test" -ForegroundColor White
    Write-Host ""
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}
