<#
.SYNOPSIS
    Automated production deployment for File Simulator Suite.

.DESCRIPTION
    This script automates the complete deployment process for the File Simulator Suite:
    - Manages Minikube cluster (start fresh or verify existing)
    - Sets up container registry with port forwarding
    - Builds Control API and Dashboard images
    - Pushes images to local registry
    - Deploys Helm chart with --wait
    - Updates Windows hosts file (when run as Administrator)
    - Verifies deployment health and displays access URLs

.PARAMETER Clean
    If specified, deletes existing cluster and starts fresh.
    Otherwise, verifies existing cluster is running.

.PARAMETER Profile
    Minikube profile name. Default: file-simulator

.PARAMETER Memory
    Memory allocation in MB. Default: 12288 (12GB)

.PARAMETER Cpus
    CPU allocation. Default: 4

.EXAMPLE
    .\Deploy-Production.ps1
    Deploys to existing cluster or starts new one.

.EXAMPLE
    .\Deploy-Production.ps1 -Clean
    Deletes existing cluster and deploys fresh.

.EXAMPLE
    .\Deploy-Production.ps1 -Memory 16384 -Cpus 6
    Deploys with custom resource allocation.

.NOTES
    Requires: minikube, kubectl, helm, docker
    Must run as Administrator to update hosts file.
#>

param(
    [switch]$Clean,
    [string]$Profile = "file-simulator",
    [int]$Memory = 12288,
    [int]$Cpus = 4
)

$ErrorActionPreference = "Stop"

# Get script directory and project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

# Progress tracking
$StepNumber = 0

function Write-Step {
    param(
        [string]$Message
    )
    $script:StepNumber++
    Write-Host "`n[$script:StepNumber] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✓ $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  → $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "  ✗ $Message" -ForegroundColor Red
}

function Test-Prerequisites {
    Write-Step "Verifying prerequisites..."

    $required = @(
        @{ Name = "minikube"; Command = "minikube version" },
        @{ Name = "kubectl"; Command = "kubectl version --client" },
        @{ Name = "helm"; Command = "helm version" },
        @{ Name = "docker"; Command = "docker --version" }
    )

    foreach ($tool in $required) {
        try {
            $null = Invoke-Expression $tool.Command 2>&1
            Write-Success "$($tool.Name) is installed"
        }
        catch {
            Write-Error "$($tool.Name) is not installed or not in PATH"
            throw "Missing prerequisite: $($tool.Name)"
        }
    }
}

function Start-Cluster {
    Write-Step "Managing Minikube cluster..."

    if ($Clean) {
        Write-Info "Deleting existing cluster..."
        minikube delete --profile $Profile 2>$null
        Write-Success "Existing cluster deleted"
    }

    # Check if cluster exists and is running
    $status = minikube status --profile $Profile 2>$null
    if ($LASTEXITCODE -eq 0 -and $status -match "Running") {
        Write-Success "Cluster '$Profile' is already running"
        return
    }

    Write-Info "Starting new cluster..."
    Write-Info "  Profile: $Profile"
    Write-Info "  Memory: $Memory MB"
    Write-Info "  CPUs: $Cpus"
    Write-Info "  Driver: hyperv"

    minikube start `
        --profile $Profile `
        --driver=hyperv `
        --memory=$Memory `
        --cpus=$Cpus `
        --disk-size=20g `
        --mount `
        --mount-string="C:\simulator-data:/mnt/simulator-data"

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start Minikube cluster"
    }

    Write-Success "Cluster started successfully"
}

function Setup-Registry {
    Write-Step "Setting up container registry..."

    # Enable registry addon
    Write-Info "Enabling registry addon..."
    minikube addons enable registry --profile $Profile
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to enable registry addon"
    }
    Write-Success "Registry addon enabled"

    # Start port forwarding in background
    Write-Info "Starting registry port forward (5000 -> registry:80)..."

    # Stop any existing port-forward jobs
    Get-Job -Name "RegistryPortForward" -ErrorAction SilentlyContinue | Stop-Job
    Get-Job -Name "RegistryPortForward" -ErrorAction SilentlyContinue | Remove-Job

    # Start new port-forward job
    $script:RegistryJob = Start-Job -Name "RegistryPortForward" -ScriptBlock {
        param($profileName)
        kubectl --context=$profileName port-forward --namespace kube-system service/registry 5000:80
    } -ArgumentList $Profile

    # Wait for registry to be accessible
    Write-Info "Waiting for registry to be accessible..."
    $maxAttempts = 30
    $attempt = 0
    $registryReady = $false

    while ($attempt -lt $maxAttempts -and -not $registryReady) {
        try {
            $null = Invoke-WebRequest -Uri "http://localhost:5000/v2/" -Method GET -TimeoutSec 2 -ErrorAction SilentlyContinue
            $registryReady = $true
            Write-Success "Registry is accessible at localhost:5000"
        }
        catch {
            $attempt++
            Start-Sleep -Seconds 1
        }
    }

    if (-not $registryReady) {
        throw "Registry did not become accessible after $maxAttempts seconds"
    }
}

function Build-Images {
    Write-Step "Building Control API image..."

    $controlApiImage = "localhost:5000/file-simulator-control-api:latest"

    docker build `
        -t $controlApiImage `
        -f "$ProjectRoot/src/FileSimulator.ControlApi/Dockerfile" `
        "$ProjectRoot/src"

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build Control API image"
    }

    Write-Success "Control API image built"

    Write-Step "Building Dashboard image..."

    $dashboardImage = "localhost:5000/file-simulator-dashboard:latest"
    $apiUrl = "http://file-simulator.local:30500"

    docker build `
        -t $dashboardImage `
        --build-arg VITE_API_BASE_URL=$apiUrl `
        "$ProjectRoot/src/FileSimulator.Dashboard"

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build Dashboard image"
    }

    Write-Success "Dashboard image built"

    Write-Step "Pushing images to registry..."

    Write-Info "Pushing Control API..."
    docker push $controlApiImage
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to push Control API image"
    }
    Write-Success "Control API pushed"

    Write-Info "Pushing Dashboard..."
    docker push $dashboardImage
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to push Dashboard image"
    }
    Write-Success "Dashboard pushed"
}

# Main execution will be implemented in subsequent tasks
