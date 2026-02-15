#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy and start the File Simulator Suite on Minikube (Kubernetes).

.DESCRIPTION
    One-command deployment for the full File Simulator Suite on Kubernetes.
    Handles the complete lifecycle:
    1. Ensures Minikube profile 'file-simulator' is running with mount
    2. Deploys (or upgrades) the Helm chart
    3. Waits for all pods to be ready
    4. Updates the Windows hosts file (if Administrator)
    5. Starts minikube tunnel for SMB LoadBalancer access

    The tunnel runs as a background PowerShell job. Stop it with:
      Stop-Job -Name "minikube-tunnel-*"
    Or use .\scripts\Stop-Simulator.ps1

.PARAMETER SkipHosts
    Skip updating the Windows hosts file.

.PARAMETER SkipTunnel
    Skip starting the minikube tunnel (SMB will not work).

.PARAMETER SimulatorPath
    Host path for simulator data. Default: C:\simulator-data

.PARAMETER Profile
    Minikube profile name. Default: file-simulator

.PARAMETER NoBrowser
    Don't open the dashboard in the browser after deployment.

.EXAMPLE
    .\Deploy-Simulator.ps1

    Full deployment: Minikube start, Helm deploy, hosts update, tunnel start.

.EXAMPLE
    .\Deploy-Simulator.ps1 -SkipHosts -SkipTunnel

    Deploy without hosts file update or tunnel (no Administrator needed).
#>
[CmdletBinding()]
param(
    [switch]$SkipHosts,
    [switch]$SkipTunnel,
    [switch]$NoBrowser,
    [string]$SimulatorPath = "C:\simulator-data",
    [string]$Profile = "file-simulator"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " File Simulator Suite - Deploy" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ── Step 1: Minikube ────────────────────────────────────────────────────────

Write-Host "[1/5] Checking Minikube profile '$Profile'..." -ForegroundColor Yellow

$profileRunning = $false
try {
    $hostStatus = minikube status -p $Profile --format='{{.Host}}' 2>&1
    if ($hostStatus -eq "Running") { $profileRunning = $true }
} catch { }

if (-not $profileRunning) {
    Write-Host "  Starting Minikube (this may take a few minutes)..." -ForegroundColor Cyan
    minikube start `
        --profile $Profile `
        --driver=hyperv `
        --memory=12288 `
        --cpus=4 `
        --disk-size=20g `
        --mount `
        --mount-string="${SimulatorPath}:/mnt/simulator-data"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR: Minikube failed to start." -ForegroundColor Red
        exit 1
    }
    Write-Host "  Minikube started." -ForegroundColor Green
} else {
    Write-Host "  Minikube is already running." -ForegroundColor Green
}

# ── Step 2: Helm deploy ────────────────────────────────────────────────────

Write-Host "`n[2/5] Deploying Helm chart..." -ForegroundColor Yellow

$repoRoot = Split-Path $PSScriptRoot -Parent
$chartPath = Join-Path $repoRoot "helm-chart\file-simulator"

if (-not (Test-Path $chartPath)) {
    Write-Host "  ERROR: Chart not found at $chartPath" -ForegroundColor Red
    exit 1
}

# Use --kube-context to avoid context-switching accidents
helm upgrade --install file-sim $chartPath `
    --kube-context=$Profile `
    --namespace file-simulator `
    --create-namespace

if ($LASTEXITCODE -ne 0) {
    Write-Host "  WARNING: Helm deploy returned non-zero. Check pods manually." -ForegroundColor Yellow
} else {
    Write-Host "  Helm chart deployed." -ForegroundColor Green
}

# ── Step 3: Wait for pods ──────────────────────────────────────────────────

Write-Host "`n[3/5] Waiting for pods to be ready..." -ForegroundColor Yellow

$maxWait = 180
$elapsed = 0
$allReady = $false

while ($elapsed -lt $maxWait) {
    $podLines = kubectl --context=$Profile get pods -n file-simulator --no-headers 2>&1
    $notReady = $podLines | Select-String -NotMatch "Running|Completed"

    if (-not $notReady) {
        $allReady = $true
        break
    }

    $pending = ($notReady | Measure-Object).Count
    Write-Host "  $pending pod(s) not ready... ($elapsed/${maxWait}s)" -ForegroundColor Gray
    Start-Sleep -Seconds 10
    $elapsed += 10
}

if ($allReady) {
    $podCount = (kubectl --context=$Profile get pods -n file-simulator --no-headers 2>&1 | Measure-Object).Count
    Write-Host "  All $podCount pods are running." -ForegroundColor Green
} else {
    Write-Host "  WARNING: Timeout waiting for pods. Some may still be starting." -ForegroundColor Yellow
    Write-Host "  Check: kubectl --context=$Profile get pods -n file-simulator" -ForegroundColor Gray
}

# ── Step 4: Hosts file ─────────────────────────────────────────────────────

if (-not $SkipHosts) {
    Write-Host "`n[4/5] Updating hosts file..." -ForegroundColor Yellow

    $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if ($isAdmin) {
        $hostsScript = Join-Path $PSScriptRoot "Setup-Hosts.ps1"
        if (Test-Path $hostsScript) {
            & $hostsScript -Profile $Profile
        } else {
            Write-Host "  Setup-Hosts.ps1 not found at $hostsScript" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  Skipping (not running as Administrator)." -ForegroundColor Yellow
        Write-Host "  Run separately as Admin: .\scripts\Setup-Hosts.ps1" -ForegroundColor Gray
    }
} else {
    Write-Host "`n[4/5] Skipping hosts file update (-SkipHosts)." -ForegroundColor Gray
}

# ── Step 5: Minikube tunnel for SMB ────────────────────────────────────────

if (-not $SkipTunnel) {
    Write-Host "`n[5/5] Starting minikube tunnel for SMB..." -ForegroundColor Yellow

    # Kill any existing tunnel jobs for this profile
    Get-Job -Name "minikube-tunnel-*" -ErrorAction SilentlyContinue |
        Where-Object { $_.State -eq 'Running' } |
        ForEach-Object {
            Write-Host "  Stopping existing tunnel job..." -ForegroundColor Gray
            Stop-Job $_
            Remove-Job $_ -Force
        }

    # Start tunnel as background job
    $tunnelJob = Start-Job -Name "minikube-tunnel-$Profile" -ScriptBlock {
        param($p)
        minikube tunnel -p $p 2>&1
    } -ArgumentList $Profile

    Write-Host "  Tunnel started (Job ID: $($tunnelJob.Id), Name: $($tunnelJob.Name))." -ForegroundColor Green

    # Wait for tunnel to establish
    Start-Sleep -Seconds 5

    # Verify LoadBalancer IP
    $smbIp = kubectl --context=$Profile get svc -n file-simulator file-sim-file-simulator-smb `
        -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>&1

    if ($smbIp -and $smbIp -ne "") {
        Write-Host "  SMB LoadBalancer IP: $smbIp" -ForegroundColor Green
    } else {
        Write-Host "  SMB LoadBalancer IP pending (may need a moment)." -ForegroundColor Yellow
    }
} else {
    Write-Host "`n[5/5] Skipping tunnel (-SkipTunnel). SMB will not work." -ForegroundColor Gray
}

# ── Summary ─────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " File Simulator Suite - Ready!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Dashboard:    http://file-simulator.local:30080" -ForegroundColor White
Write-Host "Control API:  http://file-simulator.local:30500" -ForegroundColor White
Write-Host "Kafka UI:     http://file-simulator.local:30093" -ForegroundColor White
Write-Host ""
Write-Host "Protocol Servers:" -ForegroundColor Yellow
Write-Host "  FTP:        file-simulator.local:30021" -ForegroundColor White
Write-Host "  SFTP:       file-simulator.local:30022" -ForegroundColor White
Write-Host "  HTTP:       http://file-simulator.local:30088" -ForegroundColor White
Write-Host "  S3/MinIO:   http://file-simulator.local:30900" -ForegroundColor White
Write-Host "  SMB:        \\file-simulator.local\simulator" -ForegroundColor White
Write-Host "  NFS:        file-simulator.local:32149" -ForegroundColor White
Write-Host ""
Write-Host "NAS Servers (7):" -ForegroundColor Yellow
Write-Host "  Input:   :32150, :32151, :32152" -ForegroundColor White
Write-Host "  Backup:  :32153 (read-only)" -ForegroundColor White
Write-Host "  Output:  :32154, :32155, :32156" -ForegroundColor White
Write-Host ""

if (-not $SkipTunnel) {
    Write-Host "Tunnel is running as background job. To stop:" -ForegroundColor Gray
    Write-Host "  Get-Job -Name 'minikube-tunnel-*' | Stop-Job" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "Quick test:   cd src\FileSimulator.TestConsole && dotnet run" -ForegroundColor Gray
Write-Host "E2E tests:    cd tests\FileSimulator.E2ETests && `$env:USE_EXISTING_SIMULATOR='true' && dotnet test" -ForegroundColor Gray
Write-Host ""

# Open browser
if (-not $NoBrowser) {
    Start-Process "http://file-simulator.local:30080"
}
