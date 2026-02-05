#Requires -Version 7.0
<#
.SYNOPSIS
    Runs FileSimulator integration tests with JUnit XML output.

.DESCRIPTION
    Executes all integration tests and generates JUnit XML report for CI/CD integration.
    Returns exit code 0 only if 100% of tests pass.

.PARAMETER Filter
    Optional test filter (e.g., "FullyQualifiedName~Protocol")

.PARAMETER ResultsDir
    Directory for test results (default: test-results)

.PARAMETER Verbosity
    Test output verbosity: minimal, normal, detailed (default: normal)

.PARAMETER NoBuild
    Skip build step if tests are already built

.EXAMPLE
    .\Run-IntegrationTests.ps1

.EXAMPLE
    .\Run-IntegrationTests.ps1 -Filter "FullyQualifiedName~SmokeTests" -Verbosity detailed
#>
param(
    [string]$Filter,
    [string]$ResultsDir = "test-results",
    [ValidateSet("minimal", "normal", "detailed")]
    [string]$Verbosity = "normal",
    [switch]$NoBuild
)

$ErrorActionPreference = "Continue"
$projectPath = Join-Path $PSScriptRoot "..\tests\FileSimulator.IntegrationTests"
$resultsPath = Join-Path $PSScriptRoot "..\$ResultsDir"

# Ensure results directory exists
if (!(Test-Path $resultsPath)) {
    New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
}

Write-Host "╔═══════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║           FileSimulator Integration Tests                         ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Build arguments
$testArgs = @(
    "test"
    $projectPath
    "--logger:junit;LogFilePath=$resultsPath/junit-integration-tests.xml"
    "--logger:console;verbosity=$Verbosity"
    "--results-directory:$resultsPath"
)

if ($NoBuild) {
    $testArgs += "--no-build"
}

if ($Filter) {
    $testArgs += "--filter"
    $testArgs += $Filter
    Write-Host "Filter: $Filter" -ForegroundColor Yellow
}

Write-Host "Running: dotnet $($testArgs -join ' ')" -ForegroundColor Gray
Write-Host ""

# Run tests
$startTime = Get-Date
& dotnet @testArgs
$exitCode = $LASTEXITCODE
$endTime = Get-Date
$duration = $endTime - $startTime

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan

# Parse JUnit XML for summary
$junitFile = Join-Path $resultsPath "junit-integration-tests.xml"
if (Test-Path $junitFile) {
    [xml]$junit = Get-Content $junitFile
    $testSuites = $junit.testsuites

    $totalTests = [int]$testSuites.tests
    $failures = [int]$testSuites.failures
    $errors = [int]$testSuites.errors
    $skipped = [int]$testSuites.skipped
    $passed = $totalTests - $failures - $errors - $skipped

    Write-Host ""
    Write-Host "Test Summary:" -ForegroundColor White
    Write-Host "  Total:   $totalTests" -ForegroundColor White
    Write-Host "  Passed:  $passed" -ForegroundColor Green
    if ($failures -gt 0) {
        Write-Host "  Failed:  $failures" -ForegroundColor Red
    }
    if ($errors -gt 0) {
        Write-Host "  Errors:  $errors" -ForegroundColor Red
    }
    if ($skipped -gt 0) {
        Write-Host "  Skipped: $skipped" -ForegroundColor Yellow
    }
    Write-Host "  Duration: $($duration.ToString('mm\:ss'))" -ForegroundColor Gray
    Write-Host ""
    Write-Host "JUnit XML: $junitFile" -ForegroundColor Gray
}

# Exit with appropriate code
if ($exitCode -eq 0) {
    Write-Host "✓ All tests passed!" -ForegroundColor Green
} else {
    Write-Host "✗ Tests failed (exit code: $exitCode)" -ForegroundColor Red
}

exit $exitCode
