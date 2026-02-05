#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Start the File Simulator Suite for local development and testing

.DESCRIPTION
    Starts the Control API and Dashboard services for local development.
    Optionally starts Minikube if needed. Waits for services to be healthy.

.PARAMETER NoBrowser
    Don't open browser to dashboard after starting

.PARAMETER ApiOnly
    Only start the Control API

.PARAMETER DashboardOnly
    Only start the Dashboard

.PARAMETER Wait
    Wait for services to be healthy and exit (for CI/test integration)

.EXAMPLE
    .\Start-Simulator.ps1
    Start both services and open browser

.EXAMPLE
    .\Start-Simulator.ps1 -NoBrowser
    Start both services without opening browser

.EXAMPLE
    .\Start-Simulator.ps1 -Wait
    Start services, wait for health, then exit (for tests)
#>
[CmdletBinding()]
param(
    [switch]$NoBrowser,
    [switch]$ApiOnly,
    [switch]$DashboardOnly,
    [switch]$Wait
)

$ErrorActionPreference = "Stop"

# Resolve script paths
$repoRoot = Split-Path $PSScriptRoot -Parent
$apiPath = Join-Path $repoRoot "control-plane\FileSimulator.ControlAPI"
$dashboardPath = Join-Path $repoRoot "dashboard"

# Service URLs
$apiUrl = "http://localhost:5000"
$dashboardUrl = "http://localhost:3000"

# Track background jobs
$jobs = @()

function Write-Status {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor Red
}

function Wait-ForService {
    param(
        [string]$Name,
        [string]$Url,
        [int]$TimeoutSeconds = 300
    )

    Write-Status "Waiting for $Name at $Url..."
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                Write-Success "$Name is ready!"
                return $true
            }
        }
        catch {
            # Service not ready yet
        }

        Start-Sleep -Milliseconds 1000
    }

    Write-Error "$Name did not start within $TimeoutSeconds seconds"
    return $false
}

function Stop-BackgroundJobs {
    Write-Status "Stopping background services..."

    foreach ($job in $jobs) {
        if ($job -and $job.State -eq 'Running') {
            Write-Status "Stopping $($job.Name)..."
            Stop-Job -Job $job -ErrorAction SilentlyContinue
            Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
        }
    }

    # Also kill any dotnet/node processes we might have started
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -like "*FileSimulator.ControlAPI*" } |
        Stop-Process -Force -ErrorAction SilentlyContinue

    Get-Process -Name "node" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like "*vite*" } |
        Stop-Process -Force -ErrorAction SilentlyContinue

    Write-Success "Background services stopped"
}

# Register cleanup on exit
Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action {
    Stop-BackgroundJobs
} | Out-Null

try {
    Write-Status "Starting File Simulator Suite..."

    # Check Minikube status
    Write-Status "Checking Minikube status..."
    $minikubeStatus = minikube status -p file-simulator 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Minikube profile 'file-simulator' is not running!"
        Write-Host ""
        Write-Host "Please start Minikube first:" -ForegroundColor Yellow
        Write-Host "  minikube start -p file-simulator --driver=hyperv --memory=12288 --cpus=4" -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }

    Write-Success "Minikube is running"

    # Start Control API
    if (-not $DashboardOnly) {
        Write-Status "Starting Control API..."

        if (-not (Test-Path $apiPath)) {
            Write-Error "Control API path not found: $apiPath"
            exit 1
        }

        $apiJob = Start-Job -Name "ControlAPI" -ScriptBlock {
            param($Path)
            Set-Location $Path
            dotnet run --no-launch-profile
        } -ArgumentList $apiPath

        $jobs += $apiJob
        Write-Success "Control API job started (Job ID: $($apiJob.Id))"

        # Wait for API to be ready
        if (-not (Wait-ForService -Name "Control API" -Url "$apiUrl/api/health")) {
            throw "Control API failed to start"
        }
    }

    # Start Dashboard
    if (-not $ApiOnly) {
        Write-Status "Starting Dashboard..."

        if (-not (Test-Path $dashboardPath)) {
            Write-Error "Dashboard path not found: $dashboardPath"
            exit 1
        }

        # Check if node_modules exists
        $nodeModulesPath = Join-Path $dashboardPath "node_modules"
        if (-not (Test-Path $nodeModulesPath)) {
            Write-Status "Installing dashboard dependencies..."
            Push-Location $dashboardPath
            npm install
            Pop-Location
        }

        $dashboardJob = Start-Job -Name "Dashboard" -ScriptBlock {
            param($Path)
            Set-Location $Path
            npm run dev
        } -ArgumentList $dashboardPath

        $jobs += $dashboardJob
        Write-Success "Dashboard job started (Job ID: $($dashboardJob.Id))"

        # Wait for Dashboard to be ready
        if (-not (Wait-ForService -Name "Dashboard" -Url $dashboardUrl)) {
            throw "Dashboard failed to start"
        }
    }

    Write-Success "All services started successfully!"
    Write-Host ""
    Write-Host "Services:" -ForegroundColor Green

    if (-not $DashboardOnly) {
        Write-Host "  Control API:  $apiUrl" -ForegroundColor Cyan
        Write-Host "  Health:       $apiUrl/api/health" -ForegroundColor Cyan
    }

    if (-not $ApiOnly) {
        Write-Host "  Dashboard:    $dashboardUrl" -ForegroundColor Cyan
    }

    Write-Host ""

    # Open browser
    if (-not $NoBrowser -and -not $Wait -and -not $ApiOnly) {
        Write-Status "Opening browser to dashboard..."
        Start-Process $dashboardUrl
    }

    # If -Wait flag, exit after services are healthy
    if ($Wait) {
        Write-Success "Services are healthy. Exiting (services still running in background)."
        exit 0
    }

    # Otherwise, keep running until Ctrl+C
    Write-Host "Press Ctrl+C to stop services..." -ForegroundColor Yellow
    Write-Host ""

    # Monitor jobs
    while ($true) {
        Start-Sleep -Seconds 5

        # Check if jobs are still running
        foreach ($job in $jobs) {
            if ($job.State -eq 'Failed' -or $job.State -eq 'Stopped') {
                Write-Error "$($job.Name) stopped unexpectedly!"
                Write-Host ""
                Write-Host "Job output:" -ForegroundColor Yellow
                Receive-Job -Job $job

                throw "$($job.Name) failed"
            }
        }
    }
}
catch {
    Write-Error "Error: $_"
    exit 1
}
finally {
    if (-not $Wait) {
        Stop-BackgroundJobs
    }
}
