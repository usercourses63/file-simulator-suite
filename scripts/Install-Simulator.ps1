#Requires -RunAsAdministrator
<#
.SYNOPSIS
    File Simulator Suite - Complete Installation Script

.DESCRIPTION
    This script installs the File Simulator Suite from scratch, including:
    - Creating Windows directory structure
    - Creating Minikube cluster with Hyper-V driver
    - Deploying Helm chart with all file protocol services
    - Starting minikube tunnel for SMB LoadBalancer support
    - Configuring and verifying all services

.PARAMETER SimulatorPath
    Base path for simulator data files. Default: C:\simulator-data

.PARAMETER MinikubeProfile
    Minikube profile name. Default: file-simulator

.PARAMETER MinikubeMemory
    Memory allocation for Minikube VM in MB. Default: 4096

.PARAMETER MinikubeCPUs
    Number of CPUs for Minikube VM. Default: 2

.PARAMETER MinikubeDisk
    Disk size for Minikube VM. Default: 20g

.PARAMETER SkipMinikubeCreate
    Skip Minikube cluster creation (use if cluster already exists)

.PARAMETER SkipHelmDeploy
    Skip Helm chart deployment

.PARAMETER StartTunnel
    Automatically start minikube tunnel in a new window

.PARAMETER ValuesFile
    Optional custom Helm values file path

.EXAMPLE
    .\Install-Simulator.ps1
    # Full installation with defaults

.EXAMPLE
    .\Install-Simulator.ps1 -MinikubeMemory 8192 -MinikubeCPUs 4
    # Installation with custom resource allocation

.EXAMPLE
    .\Install-Simulator.ps1 -SkipMinikubeCreate -SkipHelmDeploy
    # Only setup directories and configuration
#>

param(
    [string]$SimulatorPath = "C:\simulator-data",
    [string]$MinikubeProfile = "file-simulator",
    [int]$MinikubeMemory = 4096,
    [int]$MinikubeCPUs = 2,
    [string]$MinikubeDisk = "20g",
    [switch]$SkipMinikubeCreate,
    [switch]$SkipHelmDeploy,
    [switch]$StartTunnel,
    [string]$ValuesFile
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$HelmChartPath = Join-Path $ProjectRoot "helm-chart\file-simulator"

# Colors for output
function Write-Step { param([string]$Message) Write-Host "`n==> $Message" -ForegroundColor Cyan }
function Write-Info { param([string]$Message) Write-Host "    $Message" -ForegroundColor White }
function Write-Success { param([string]$Message) Write-Host "    [OK] $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "    [!] $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "    [X] $Message" -ForegroundColor Red }

# Banner
Write-Host ""
Write-Host "  ╔═══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  ║                                                               ║" -ForegroundColor Cyan
Write-Host "  ║           FILE SIMULATOR SUITE - INSTALLATION                 ║" -ForegroundColor Cyan
Write-Host "  ║                                                               ║" -ForegroundColor Cyan
Write-Host "  ║   FTP | SFTP | HTTP | WebDAV | S3 | SMB | NFS                ║" -ForegroundColor Cyan
Write-Host "  ║                                                               ║" -ForegroundColor Cyan
Write-Host "  ╚═══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# STEP 1: Check Prerequisites
# ============================================================================
Write-Step "Checking Prerequisites"

# Check Hyper-V
Write-Info "Checking Hyper-V..."
$hypervFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -ErrorAction SilentlyContinue
if ($hypervFeature -and $hypervFeature.State -eq "Enabled") {
    Write-Success "Hyper-V is enabled"
} else {
    Write-Error "Hyper-V is not enabled"
    Write-Host ""
    Write-Host "    SMB requires Hyper-V for LoadBalancer support." -ForegroundColor Yellow
    Write-Host "    To enable Hyper-V, run this command and restart:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "    Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All" -ForegroundColor White
    Write-Host ""
    $continue = Read-Host "    Continue without Hyper-V? (SMB will not work) [y/N]"
    if ($continue -ne "y" -and $continue -ne "Y") {
        exit 1
    }
}

# Check Minikube
Write-Info "Checking Minikube..."
$minikubeCmd = Get-Command minikube -ErrorAction SilentlyContinue
if ($minikubeCmd) {
    $minikubeVersion = minikube version --short 2>$null
    Write-Success "Minikube installed: $minikubeVersion"
} else {
    Write-Error "Minikube is not installed"
    Write-Host ""
    Write-Host "    Install Minikube from:" -ForegroundColor Yellow
    Write-Host "    https://minikube.sigs.k8s.io/docs/start/" -ForegroundColor White
    Write-Host ""
    Write-Host "    Or using Chocolatey:" -ForegroundColor Yellow
    Write-Host "    choco install minikube" -ForegroundColor White
    Write-Host ""
    exit 1
}

# Check kubectl
Write-Info "Checking kubectl..."
$kubectlCmd = Get-Command kubectl -ErrorAction SilentlyContinue
if ($kubectlCmd) {
    $kubectlVersion = kubectl version --client --short 2>$null
    if (-not $kubectlVersion) {
        $kubectlVersion = (kubectl version --client -o json 2>$null | ConvertFrom-Json).clientVersion.gitVersion
    }
    Write-Success "kubectl installed: $kubectlVersion"
} else {
    Write-Error "kubectl is not installed"
    Write-Host ""
    Write-Host "    Install kubectl from:" -ForegroundColor Yellow
    Write-Host "    https://kubernetes.io/docs/tasks/tools/install-kubectl-windows/" -ForegroundColor White
    Write-Host ""
    exit 1
}

# Check Helm
Write-Info "Checking Helm..."
$helmCmd = Get-Command helm -ErrorAction SilentlyContinue
if ($helmCmd) {
    $helmVersion = helm version --short 2>$null
    Write-Success "Helm installed: $helmVersion"
} else {
    Write-Error "Helm is not installed"
    Write-Host ""
    Write-Host "    Install Helm from:" -ForegroundColor Yellow
    Write-Host "    https://helm.sh/docs/intro/install/" -ForegroundColor White
    Write-Host ""
    Write-Host "    Or using Chocolatey:" -ForegroundColor Yellow
    Write-Host "    choco install kubernetes-helm" -ForegroundColor White
    Write-Host ""
    exit 1
}

# Check Helm chart exists
Write-Info "Checking Helm chart..."
if (Test-Path $HelmChartPath) {
    Write-Success "Helm chart found at: $HelmChartPath"
} else {
    Write-Error "Helm chart not found at: $HelmChartPath"
    Write-Host ""
    Write-Host "    Make sure you're running this script from the project directory." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# ============================================================================
# STEP 2: Create Windows Directory Structure
# ============================================================================
Write-Step "Creating Windows Directory Structure"

$directories = @(
    $SimulatorPath,
    "$SimulatorPath\input",
    "$SimulatorPath\output",
    "$SimulatorPath\temp",
    "$SimulatorPath\config"
)

foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Success "Created: $dir"
    } else {
        Write-Info "Exists: $dir"
    }
}

# Set permissions
Write-Info "Setting directory permissions..."
icacls $SimulatorPath /grant "Everyone:(OI)(CI)F" /T /Q 2>$null
Write-Success "Permissions set for: $SimulatorPath"

# ============================================================================
# STEP 3: Create/Start Minikube Cluster
# ============================================================================
if (-not $SkipMinikubeCreate) {
    Write-Step "Creating Minikube Cluster"

    # Check if profile exists
    $existingProfiles = minikube profile list -o json 2>$null | ConvertFrom-Json
    $profileExists = $existingProfiles.valid | Where-Object { $_.Name -eq $MinikubeProfile }

    if ($profileExists) {
        Write-Warning "Minikube profile '$MinikubeProfile' already exists"
        $recreate = Read-Host "    Delete and recreate? [y/N]"
        if ($recreate -eq "y" -or $recreate -eq "Y") {
            Write-Info "Deleting existing profile..."
            minikube delete -p $MinikubeProfile 2>$null
        } else {
            Write-Info "Using existing profile"
            $SkipMinikubeCreate = $true
        }
    }

    if (-not $SkipMinikubeCreate) {
        Write-Info "Creating Minikube cluster with Hyper-V driver..."
        Write-Info "  Profile: $MinikubeProfile"
        Write-Info "  Memory:  $MinikubeMemory MB"
        Write-Info "  CPUs:    $MinikubeCPUs"
        Write-Info "  Disk:    $MinikubeDisk"
        Write-Info "  Mount:   $SimulatorPath -> /mnt/simulator-data"
        Write-Host ""

        $minikubeArgs = @(
            "start",
            "--profile", $MinikubeProfile,
            "--driver=hyperv",
            "--memory=$MinikubeMemory",
            "--cpus=$MinikubeCPUs",
            "--disk-size=$MinikubeDisk",
            "--mount",
            "--mount-string=${SimulatorPath}:/mnt/simulator-data"
        )

        & minikube @minikubeArgs

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to create Minikube cluster"
            exit 1
        }

        Write-Success "Minikube cluster created successfully"
    }
}

# Ensure correct profile is active
Write-Info "Setting active Minikube profile..."
minikube profile $MinikubeProfile 2>$null
Write-Success "Active profile: $MinikubeProfile"

# Get Minikube IP
Write-Info "Getting Minikube IP..."
$minikubeIP = minikube ip -p $MinikubeProfile
if (-not $minikubeIP) {
    Write-Error "Could not get Minikube IP. Is the cluster running?"
    exit 1
}
Write-Success "Minikube IP: $minikubeIP"

# Verify mount
Write-Info "Verifying mount..."
$mountCheck = minikube ssh -p $MinikubeProfile "ls -la /mnt/simulator-data 2>/dev/null" 2>$null
if ($mountCheck) {
    Write-Success "Mount verified: /mnt/simulator-data"
} else {
    Write-Warning "Mount may not be active. Files may not sync."
}

# ============================================================================
# STEP 4: Deploy Helm Chart
# ============================================================================
if (-not $SkipHelmDeploy) {
    Write-Step "Deploying Helm Chart"

    # Build helm command
    $helmArgs = @(
        "upgrade", "--install",
        "file-sim",
        $HelmChartPath,
        "--namespace", "file-simulator",
        "--create-namespace",
        "--wait",
        "--timeout", "5m"
    )

    if ($ValuesFile -and (Test-Path $ValuesFile)) {
        Write-Info "Using custom values file: $ValuesFile"
        $helmArgs += "-f"
        $helmArgs += $ValuesFile
    }

    Write-Info "Running helm upgrade --install..."
    & helm @helmArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Helm deployment failed"
        Write-Host ""
        Write-Host "    Check the error above and try again." -ForegroundColor Yellow
        Write-Host "    You can also try:" -ForegroundColor Yellow
        Write-Host "    kubectl get events -n file-simulator --sort-by='.lastTimestamp'" -ForegroundColor White
        Write-Host ""
        exit 1
    }

    Write-Success "Helm chart deployed successfully"

    # Wait for pods
    Write-Info "Waiting for pods to be ready..."
    $maxRetries = 30
    $retryCount = 0

    while ($retryCount -lt $maxRetries) {
        $pendingPods = kubectl get pods -n file-simulator --no-headers 2>$null | Where-Object { $_ -notmatch "Running|Completed" }
        if (-not $pendingPods) {
            Write-Success "All pods are running"
            break
        }
        $retryCount++
        Start-Sleep -Seconds 5
        Write-Host "." -NoNewline
    }

    if ($retryCount -eq $maxRetries) {
        Write-Warning "Some pods may not be ready yet. Check with: kubectl get pods -n file-simulator"
    }
}

# ============================================================================
# STEP 5: Start Minikube Tunnel (for SMB)
# ============================================================================
Write-Step "Configuring SMB LoadBalancer"

# Check if tunnel is needed
$smbService = kubectl get svc -n file-simulator -o json 2>$null | ConvertFrom-Json |
    Select-Object -ExpandProperty items |
    Where-Object { $_.metadata.name -match "smb" -and $_.spec.type -eq "LoadBalancer" }

if ($smbService) {
    $externalIP = $smbService.status.loadBalancer.ingress[0].ip

    if (-not $externalIP) {
        Write-Warning "SMB LoadBalancer has no external IP"
        Write-Info "SMB requires 'minikube tunnel' to be running"
        Write-Host ""

        if ($StartTunnel) {
            Write-Info "Starting minikube tunnel in new window..."
            Start-Process powershell -ArgumentList "-NoExit", "-Command", "minikube tunnel -p $MinikubeProfile" -Verb RunAs
            Write-Success "Tunnel started in new window"

            # Wait for external IP
            Write-Info "Waiting for external IP assignment..."
            $maxRetries = 30
            $retryCount = 0

            while ($retryCount -lt $maxRetries) {
                Start-Sleep -Seconds 2
                $smbService = kubectl get svc -n file-simulator -l app.kubernetes.io/component=smb -o json 2>$null | ConvertFrom-Json
                $externalIP = $smbService.items[0].status.loadBalancer.ingress[0].ip
                if ($externalIP) {
                    Write-Success "SMB External IP: $externalIP"
                    break
                }
                $retryCount++
                Write-Host "." -NoNewline
            }

            if ($retryCount -eq $maxRetries) {
                Write-Warning "External IP not assigned yet. Check tunnel window."
            }
        } else {
            Write-Host "    Run this command in an Administrator terminal:" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "    minikube tunnel -p $MinikubeProfile" -ForegroundColor White
            Write-Host ""
            Write-Host "    Or re-run this script with -StartTunnel" -ForegroundColor Yellow
        }
    } else {
        Write-Success "SMB External IP: $externalIP"
    }
} else {
    Write-Info "SMB service not found or not using LoadBalancer"
}

# ============================================================================
# STEP 6: Create Configuration Files
# ============================================================================
Write-Step "Creating Configuration Files"

# Get final IPs
$minikubeIP = minikube ip -p $MinikubeProfile
$smbIP = $minikubeIP

# Try to get LoadBalancer IP for SMB
$smbService = kubectl get svc -n file-simulator -l app.kubernetes.io/component=smb -o json 2>$null | ConvertFrom-Json
if ($smbService.items -and $smbService.items[0].status.loadBalancer.ingress) {
    $smbIP = $smbService.items[0].status.loadBalancer.ingress[0].ip
    if (-not $smbIP) { $smbIP = $minikubeIP }
}

# Create environment configuration
$envConfig = @"
# File Simulator Suite - Environment Configuration
# Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
# Profile: $MinikubeProfile

# Minikube IP
`$env:MINIKUBE_IP = "$minikubeIP"
`$env:SIMULATOR_PATH = "$SimulatorPath"

# Service Endpoints
`$env:FILE_MGMT_URL = "http://${minikubeIP}:30180"
`$env:FILE_FTP_HOST = "$minikubeIP"
`$env:FILE_FTP_PORT = "30021"
`$env:FILE_SFTP_HOST = "$minikubeIP"
`$env:FILE_SFTP_PORT = "30022"
`$env:FILE_HTTP_URL = "http://${minikubeIP}:30088"
`$env:FILE_WEBDAV_URL = "http://${minikubeIP}:30089"
`$env:FILE_S3_ENDPOINT = "http://${minikubeIP}:30900"
`$env:FILE_S3_CONSOLE = "http://${minikubeIP}:30901"
`$env:FILE_SMB_HOST = "$smbIP"
`$env:FILE_SMB_PORT = "445"
`$env:FILE_NFS_HOST = "$minikubeIP"
`$env:FILE_NFS_PORT = "32149"

# Credentials
`$env:FILE_FTP_USER = "ftpuser"
`$env:FILE_FTP_PASS = "ftppass123"
`$env:FILE_SFTP_USER = "sftpuser"
`$env:FILE_SFTP_PASS = "sftppass123"
`$env:FILE_HTTP_USER = "httpuser"
`$env:FILE_HTTP_PASS = "httppass123"
`$env:FILE_S3_ACCESS_KEY = "minioadmin"
`$env:FILE_S3_SECRET_KEY = "minioadmin123"
`$env:FILE_SMB_USER = "smbuser"
`$env:FILE_SMB_PASS = "smbpass123"
`$env:FILE_MGMT_USER = "admin"
`$env:FILE_MGMT_PASS = "admin123"

Write-Host "File Simulator environment loaded" -ForegroundColor Green
"@
$envConfig | Out-File "$SimulatorPath\config\env.ps1" -Encoding UTF8
Write-Success "Created: $SimulatorPath\config\env.ps1"

# Create appsettings.json template
$appsettings = @"
{
  "FileSimulator": {
    "Ftp": {
      "Host": "$minikubeIP",
      "Port": 30021,
      "Username": "ftpuser",
      "Password": "ftppass123",
      "BasePath": "/output"
    },
    "Sftp": {
      "Host": "$minikubeIP",
      "Port": 30022,
      "Username": "sftpuser",
      "Password": "sftppass123",
      "BasePath": "/data/output"
    },
    "Http": {
      "BaseUrl": "http://${minikubeIP}:30088",
      "BasePath": "/output"
    },
    "WebDav": {
      "BaseUrl": "http://${minikubeIP}:30089",
      "Username": "httpuser",
      "Password": "httppass123",
      "BasePath": "/output"
    },
    "S3": {
      "ServiceUrl": "http://${minikubeIP}:30900",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin123",
      "BucketName": "simulator",
      "BasePath": "output"
    },
    "Smb": {
      "Host": "$smbIP",
      "Port": 445,
      "ShareName": "simulator",
      "Username": "smbuser",
      "Password": "smbpass123",
      "BasePath": "output"
    }
  },
  "TestSettings": {
    "TestFileSizeBytes": 1024,
    "TimeoutSeconds": 30
  }
}
"@
$appsettings | Out-File "$SimulatorPath\config\appsettings.json" -Encoding UTF8
Write-Success "Created: $SimulatorPath\config\appsettings.json"

# Create SMB connect script
$smbScript = @"
# Connect to SMB Share
`$smbIP = "$smbIP"
`$shareName = "simulator"
`$username = "smbuser"
`$password = "smbpass123"

Write-Host "Connecting to SMB share..." -ForegroundColor Cyan
Write-Host "  Host: `$smbIP" -ForegroundColor White
Write-Host "  Share: `$shareName" -ForegroundColor White
Write-Host ""

# Remove existing connection
net use Z: /delete 2>`$null | Out-Null

# Connect
`$result = net use Z: "\\`$smbIP\`$shareName" /user:`$username `$password 2>&1

if (`$LASTEXITCODE -eq 0) {
    Write-Host "Connected successfully!" -ForegroundColor Green
    Write-Host "  Drive Z: mapped to \\`$smbIP\`$shareName" -ForegroundColor White
    Write-Host ""
    explorer Z:
} else {
    Write-Host "Connection failed!" -ForegroundColor Red
    Write-Host `$result -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Make sure 'minikube tunnel' is running in another window." -ForegroundColor Yellow
}
"@
$smbScript | Out-File "$SimulatorPath\config\connect-smb.ps1" -Encoding UTF8
Write-Success "Created: $SimulatorPath\config\connect-smb.ps1"

# Create start tunnel script
$tunnelScript = @"
# Start Minikube Tunnel for SMB LoadBalancer
#Requires -RunAsAdministrator

Write-Host "Starting Minikube Tunnel for SMB support..." -ForegroundColor Cyan
Write-Host "Keep this window open while using SMB." -ForegroundColor Yellow
Write-Host ""
Write-Host "Press Ctrl+C to stop the tunnel." -ForegroundColor Gray
Write-Host ""

minikube tunnel -p $MinikubeProfile
"@
$tunnelScript | Out-File "$SimulatorPath\config\start-tunnel.ps1" -Encoding UTF8
Write-Success "Created: $SimulatorPath\config\start-tunnel.ps1"

# ============================================================================
# STEP 7: Summary
# ============================================================================
Write-Host ""
Write-Host "  ╔═══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "  ║                                                               ║" -ForegroundColor Green
Write-Host "  ║              INSTALLATION COMPLETE!                           ║" -ForegroundColor Green
Write-Host "  ║                                                               ║" -ForegroundColor Green
Write-Host "  ╚═══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

Write-Host "  SERVICE ENDPOINTS" -ForegroundColor Cyan
Write-Host "  ────────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""
Write-Host "  Management UI:    http://${minikubeIP}:30180" -ForegroundColor White
Write-Host "                    Username: admin  |  Password: admin123" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  FTP:              ftp://${minikubeIP}:30021" -ForegroundColor White
Write-Host "                    Username: ftpuser  |  Password: ftppass123" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  SFTP:             sftp://${minikubeIP}:30022" -ForegroundColor White
Write-Host "                    Username: sftpuser  |  Password: sftppass123" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  HTTP:             http://${minikubeIP}:30088" -ForegroundColor White
Write-Host "  WebDAV:           http://${minikubeIP}:30089" -ForegroundColor White
Write-Host "                    Username: httpuser  |  Password: httppass123" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  S3 API:           http://${minikubeIP}:30900" -ForegroundColor White
Write-Host "  S3 Console:       http://${minikubeIP}:30901" -ForegroundColor White
Write-Host "                    Access Key: minioadmin  |  Secret: minioadmin123" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  SMB:              \\${smbIP}\simulator" -ForegroundColor White
Write-Host "                    Username: smbuser  |  Password: smbpass123" -ForegroundColor DarkGray
Write-Host "                    " -NoNewline
if ($smbIP -eq $minikubeIP) {
    Write-Host "(Requires: minikube tunnel)" -ForegroundColor Yellow
} else {
    Write-Host "(LoadBalancer active)" -ForegroundColor Green
}
Write-Host ""
Write-Host "  NFS:              ${minikubeIP}:32149:/data" -ForegroundColor White
Write-Host ""

Write-Host "  DIRECTORY STRUCTURE" -ForegroundColor Cyan
Write-Host "  ────────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host "  $SimulatorPath\" -ForegroundColor White
Write-Host "  ├── input\       <- Place test input files here" -ForegroundColor Gray
Write-Host "  ├── output\      <- Services write output files here" -ForegroundColor Gray
Write-Host "  ├── temp\        <- Temporary processing files" -ForegroundColor Gray
Write-Host "  └── config\      <- Configuration and helper scripts" -ForegroundColor Gray
Write-Host ""

Write-Host "  QUICK START" -ForegroundColor Cyan
Write-Host "  ────────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""
Write-Host "  1. Open Management UI:" -ForegroundColor White
Write-Host "     Start-Process 'http://${minikubeIP}:30180'" -ForegroundColor Yellow
Write-Host ""
Write-Host "  2. Load environment variables:" -ForegroundColor White
Write-Host "     . $SimulatorPath\config\env.ps1" -ForegroundColor Yellow
Write-Host ""
Write-Host "  3. Connect SMB drive (requires tunnel):" -ForegroundColor White
Write-Host "     # First, start tunnel in Admin PowerShell:" -ForegroundColor Gray
Write-Host "     minikube tunnel -p $MinikubeProfile" -ForegroundColor Yellow
Write-Host "     # Then connect:" -ForegroundColor Gray
Write-Host "     . $SimulatorPath\config\connect-smb.ps1" -ForegroundColor Yellow
Write-Host ""
Write-Host "  4. Test S3 with AWS CLI:" -ForegroundColor White
Write-Host "     aws --endpoint-url http://${minikubeIP}:30900 s3 ls" -ForegroundColor Yellow
Write-Host ""

Write-Host "  ────────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host "  Configuration saved to: $SimulatorPath\config\" -ForegroundColor DarkGray
Write-Host "  ────────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""
