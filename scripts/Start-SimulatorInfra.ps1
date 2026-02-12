#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Start and monitor Minikube mount and tunnel for File Simulator Suite

.DESCRIPTION
    Manages both 'minikube mount' and 'minikube tunnel' as background processes.
    Auto-restarts either process if it dies, with exponential backoff.
    Gracefully shuts down both on Ctrl+C.

    This replaces the manual workflow of running mount and tunnel in separate terminals.

.PARAMETER NoTunnel
    Skip starting minikube tunnel (if SMB LoadBalancer not needed)

.PARAMETER NoMount
    Skip starting minikube mount (if using emptyDir-only pods)

.PARAMETER Profile
    Minikube profile name (default: file-simulator)

.PARAMETER MountSource
    Windows source path to mount (default: C:\simulator-data)

.PARAMETER MountTarget
    Linux target path in Minikube VM (default: /mnt/simulator-data)

.EXAMPLE
    .\Start-SimulatorInfra.ps1
    Start both mount and tunnel

.EXAMPLE
    .\Start-SimulatorInfra.ps1 -NoTunnel
    Start mount only (no SMB LoadBalancer)

.EXAMPLE
    .\Start-SimulatorInfra.ps1 -NoMount
    Start tunnel only
#>
[CmdletBinding()]
param(
    [switch]$NoTunnel,
    [switch]$NoMount,
    [string]$Profile = "file-simulator",
    [string]$MountSource = "C:\simulator-data",
    [string]$MountTarget = "/mnt/simulator-data"
)

$ErrorActionPreference = "Stop"

# Backoff configuration (seconds)
$BackoffSteps = @(5, 10, 30, 60)

# Process tracking
$mountProcess = $null
$tunnelProcess = $null
$mountRestartCount = 0
$tunnelRestartCount = 0
$running = $true

function Write-Status {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor Yellow
}

function Write-Err {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor Red
}

function Get-BackoffDelay {
    param([int]$RestartCount)
    $index = [Math]::Min($RestartCount, $BackoffSteps.Length - 1)
    return $BackoffSteps[$index]
}

function Start-MountProcess {
    Write-Status "Starting minikube mount: $MountSource -> $MountTarget (profile: $Profile)"

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "minikube"
    $psi.Arguments = "mount `"${MountSource}:${MountTarget}`" -p $Profile"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $proc = [System.Diagnostics.Process]::Start($psi)
    Write-Success "Mount process started (PID: $($proc.Id))"
    return $proc
}

function Start-TunnelProcess {
    Write-Status "Starting minikube tunnel (profile: $Profile)"

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "minikube"
    $psi.Arguments = "tunnel -p $Profile"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $proc = [System.Diagnostics.Process]::Start($psi)
    Write-Success "Tunnel process started (PID: $($proc.Id))"
    return $proc
}

function Stop-AllProcesses {
    $script:running = $false

    if ($script:mountProcess -and -not $script:mountProcess.HasExited) {
        Write-Status "Stopping mount process (PID: $($script:mountProcess.Id))..."
        try {
            $script:mountProcess.Kill($true)
            $script:mountProcess.WaitForExit(5000)
        } catch { }
    }

    if ($script:tunnelProcess -and -not $script:tunnelProcess.HasExited) {
        Write-Status "Stopping tunnel process (PID: $($script:tunnelProcess.Id))..."
        try {
            $script:tunnelProcess.Kill($true)
            $script:tunnelProcess.WaitForExit(5000)
        } catch { }
    }

    Write-Success "All processes stopped"
}

# Verify minikube is running
Write-Status "Checking Minikube profile '$Profile'..."
$minikubeStatus = minikube status -p $Profile 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Err "Minikube profile '$Profile' is not running!"
    Write-Host ""
    Write-Host "Start it with:" -ForegroundColor Yellow
    Write-Host "  minikube start -p $Profile --driver=hyperv --memory=12288 --cpus=4 --mount --mount-string=`"${MountSource}:${MountTarget}`"" -ForegroundColor Yellow
    exit 1
}
Write-Success "Minikube profile '$Profile' is running"

# Register Ctrl+C handler
$null = Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action {
    Stop-AllProcesses
}

# Handle Ctrl+C via console
[Console]::TreatControlCAsInput = $true

try {
    # Start processes
    if (-not $NoMount) {
        $mountProcess = Start-MountProcess
    }

    if (-not $NoTunnel) {
        # Check if running as administrator (required for tunnel)
        $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator)

        if (-not $isAdmin) {
            Write-Warn "Minikube tunnel requires Administrator privileges for SMB LoadBalancer."
            Write-Warn "Run this script as Administrator, or use -NoTunnel to skip."
        }

        $tunnelProcess = Start-TunnelProcess
    }

    Write-Host ""
    Write-Success "Infrastructure manager running. Press Ctrl+C to stop."
    Write-Host ""

    $statusLine = @()
    if (-not $NoMount) { $statusLine += "Mount: $MountSource -> $MountTarget" }
    if (-not $NoTunnel) { $statusLine += "Tunnel: active" }
    Write-Status ($statusLine -join " | ")
    Write-Host ""

    # Monitor loop
    while ($running) {
        # Check for Ctrl+C
        if ([Console]::KeyAvailable) {
            $key = [Console]::ReadKey($true)
            if ($key.Key -eq 'C' -and $key.Modifiers -band [ConsoleModifiers]::Control) {
                Write-Host ""
                Write-Status "Ctrl+C received. Shutting down..."
                break
            }
        }

        # Check mount process
        if (-not $NoMount -and $mountProcess -and $mountProcess.HasExited) {
            $exitCode = $mountProcess.ExitCode
            Write-Warn "Mount process exited (code: $exitCode)"

            $delay = Get-BackoffDelay -RestartCount $mountRestartCount
            Write-Status "Restarting mount in ${delay}s (attempt $($mountRestartCount + 1))..."
            Start-Sleep -Seconds $delay

            if ($running) {
                $mountProcess = Start-MountProcess
                $mountRestartCount++
            }
        }

        # Check tunnel process
        if (-not $NoTunnel -and $tunnelProcess -and $tunnelProcess.HasExited) {
            $exitCode = $tunnelProcess.ExitCode
            Write-Warn "Tunnel process exited (code: $exitCode)"

            $delay = Get-BackoffDelay -RestartCount $tunnelRestartCount
            Write-Status "Restarting tunnel in ${delay}s (attempt $($tunnelRestartCount + 1))..."
            Start-Sleep -Seconds $delay

            if ($running) {
                $tunnelProcess = Start-TunnelProcess
                $tunnelRestartCount++
            }
        }

        # Reset backoff counters after 5 minutes of stability
        if ($mountRestartCount -gt 0 -and $mountProcess -and -not $mountProcess.HasExited) {
            $uptime = (Get-Date) - $mountProcess.StartTime
            if ($uptime.TotalMinutes -ge 5) {
                $mountRestartCount = 0
            }
        }
        if ($tunnelRestartCount -gt 0 -and $tunnelProcess -and -not $tunnelProcess.HasExited) {
            $uptime = (Get-Date) - $tunnelProcess.StartTime
            if ($uptime.TotalMinutes -ge 5) {
                $tunnelRestartCount = 0
            }
        }

        Start-Sleep -Seconds 5
    }
}
finally {
    [Console]::TreatControlCAsInput = $false
    Stop-AllProcesses
}
