# File Simulator Suite - Windows Setup Script
# Run as Administrator

param(
    [string]$SimulatorPath = "C:\simulator-data",
    [switch]$StartMinikube = $false,
    [switch]$DeployChart = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  File Simulator Suite - Windows Setup" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# 1. Create directory structure
Write-Host "`n[1/6] Creating directory structure..." -ForegroundColor Yellow

$directories = @(
    "$SimulatorPath",
    "$SimulatorPath\input",
    "$SimulatorPath\output",
    "$SimulatorPath\temp",
    "$SimulatorPath\config"
)

foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "  Created: $dir" -ForegroundColor Green
    } else {
        Write-Host "  Exists: $dir" -ForegroundColor Gray
    }
}

# NAS Server directories (Phase 4: Configuration Templates)
Write-Host "`n[2/6] Creating NAS server directories..." -ForegroundColor Yellow

$nasServers = @(
    @{ name = "nas-input-1"; role = "Input files for processing" },
    @{ name = "nas-input-2"; role = "Input files for processing" },
    @{ name = "nas-input-3"; role = "Input files for processing" },
    @{ name = "nas-backup"; role = "Backup storage (read-only export)" },
    @{ name = "nas-output-1"; role = "Output files from processing" },
    @{ name = "nas-output-2"; role = "Output files from processing" },
    @{ name = "nas-output-3"; role = "Output files from processing" }
)

foreach ($nas in $nasServers) {
    $dir = "$SimulatorPath\$($nas.name)"
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "  Created: $dir" -ForegroundColor Green

        # Create README.txt with purpose
        $readme = @"
NAS Server: $($nas.name)
Created: $(Get-Date)
Purpose: $($nas.role)

This directory is mounted into the $($nas.name) NAS server pod.
Files placed here will be available via NFS mount after pod restart.
"@
        Set-Content -Path "$dir\README.txt" -Value $readme
    } else {
        Write-Host "  Exists: $dir" -ForegroundColor Gray
    }
}

# 3. Configure Minikube mount
Write-Host "`n[3/6] Configuring Minikube mount..." -ForegroundColor Yellow

# Check if Minikube is installed
$minikubeInstalled = Get-Command minikube -ErrorAction SilentlyContinue
if (-not $minikubeInstalled) {
    Write-Host "  ERROR: Minikube not found. Please install Minikube first." -ForegroundColor Red
    exit 1
}

# Get Minikube status
$minikubeStatus = minikube status --format='{{.Host}}' 2>$null
if ($minikubeStatus -ne "Running") {
    if ($StartMinikube) {
        Write-Host "  Starting Minikube..." -ForegroundColor Yellow
        minikube start --mount --mount-string="$SimulatorPath`:/mnt/simulator-data"
    } else {
        Write-Host "  Minikube is not running. Start with: minikube start --mount --mount-string='$SimulatorPath`:/mnt/simulator-data'" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Minikube is running" -ForegroundColor Green
    
    # Check if mount exists
    $mountCheck = minikube ssh "ls /mnt/simulator-data 2>/dev/null"
    if (-not $mountCheck) {
        Write-Host "  Mount not found. Restart Minikube with mount:" -ForegroundColor Yellow
        Write-Host "  minikube stop" -ForegroundColor White
        Write-Host "  minikube start --mount --mount-string='$SimulatorPath`:/mnt/simulator-data'" -ForegroundColor White
    }
}

# 4. Get Minikube IP
Write-Host "`n[4/6] Getting Minikube IP..." -ForegroundColor Yellow
$minikubeIP = minikube ip 2>$null
if ($minikubeIP) {
    Write-Host "  Minikube IP: $minikubeIP" -ForegroundColor Green
    
    # Create environment file
    $envContent = @"
# File Simulator Environment Variables
# Generated: $(Get-Date)

`$env:MINIKUBE_IP = "$minikubeIP"
`$env:SIMULATOR_PATH = "$SimulatorPath"

# Service URLs
`$env:SIMULATOR_MGMT_URL = "http://$minikubeIP`:30080"
`$env:SIMULATOR_FTP_HOST = "$minikubeIP"
`$env:SIMULATOR_FTP_PORT = "30021"
`$env:SIMULATOR_SFTP_HOST = "$minikubeIP"
`$env:SIMULATOR_SFTP_PORT = "30022"
`$env:SIMULATOR_HTTP_URL = "http://$minikubeIP`:30088"
`$env:SIMULATOR_S3_URL = "http://$minikubeIP`:30900"
`$env:SIMULATOR_S3_CONSOLE = "http://$minikubeIP`:30901"
"@
    $envContent | Out-File "$SimulatorPath\config\env.ps1" -Encoding UTF8
    Write-Host "  Created: $SimulatorPath\config\env.ps1" -ForegroundColor Green
}

# 5. Create helper scripts
Write-Host "`n[5/6] Creating helper scripts..." -ForegroundColor Yellow

# Quick-connect scripts
$ftpScript = @"
# Quick FTP Connect
`$env:MINIKUBE_IP = (minikube ip)
Write-Host "Connecting to FTP at `$env:MINIKUBE_IP:30021"
Write-Host "Username: ftpuser"
Write-Host "Password: ftppass123"
ftp `$env:MINIKUBE_IP 30021
"@
$ftpScript | Out-File "$SimulatorPath\config\connect-ftp.ps1" -Encoding UTF8

$sftpScript = @"
# Quick SFTP Connect (requires OpenSSH or PuTTY)
`$env:MINIKUBE_IP = (minikube ip)
Write-Host "Connecting to SFTP at `$env:MINIKUBE_IP:30022"
Write-Host "Username: sftpuser"
Write-Host "Password: sftppass123"

# Try OpenSSH first
`$sftpCmd = Get-Command sftp -ErrorAction SilentlyContinue
if (`$sftpCmd) {
    sftp -P 30022 sftpuser@`$env:MINIKUBE_IP
} else {
    # Try PuTTY psftp
    `$psftpCmd = Get-Command psftp -ErrorAction SilentlyContinue
    if (`$psftpCmd) {
        psftp -P 30022 sftpuser@`$env:MINIKUBE_IP
    } else {
        Write-Host "No SFTP client found. Install OpenSSH or PuTTY." -ForegroundColor Red
    }
}
"@
$sftpScript | Out-File "$SimulatorPath\config\connect-sftp.ps1" -Encoding UTF8

$s3Script = @"
# Configure AWS CLI for MinIO
`$env:MINIKUBE_IP = (minikube ip)
`$env:AWS_ACCESS_KEY_ID = "minioadmin"
`$env:AWS_SECRET_ACCESS_KEY = "minioadmin123"
`$env:AWS_ENDPOINT_URL = "http://`${env:MINIKUBE_IP}:30900"

Write-Host "S3 (MinIO) configured"
Write-Host "Endpoint: `$env:AWS_ENDPOINT_URL"
Write-Host ""
Write-Host "Example commands:"
Write-Host "  aws --endpoint-url `$env:AWS_ENDPOINT_URL s3 ls"
Write-Host "  aws --endpoint-url `$env:AWS_ENDPOINT_URL s3 cp file.txt s3://input/"
"@
$s3Script | Out-File "$SimulatorPath\config\configure-s3.ps1" -Encoding UTF8

$mapDriveScript = @"
# Map network drives for SMB access
`$env:MINIKUBE_IP = (minikube ip)

# Remove existing mappings
net use S: /delete 2>`$null

# Map the simulator share
net use S: "\\`$env:MINIKUBE_IP\simulator" /user:smbuser smbpass123

if (`$?) {
    Write-Host "Drive S: mapped successfully" -ForegroundColor Green
    explorer S:
} else {
    Write-Host "Failed to map drive" -ForegroundColor Red
}
"@
$mapDriveScript | Out-File "$SimulatorPath\config\map-drive.ps1" -Encoding UTF8

Write-Host "  Created helper scripts in $SimulatorPath\config\" -ForegroundColor Green

# 6. Deploy Helm chart (if requested)
if ($DeployChart) {
    Write-Host "`n[6/6] Deploying Helm chart..." -ForegroundColor Yellow
    $helmInstalled = Get-Command helm -ErrorAction SilentlyContinue
    if ($helmInstalled) {
        $chartPath = Join-Path $PSScriptRoot "helm-chart\file-simulator"
        if (Test-Path $chartPath) {
            helm upgrade --install file-sim $chartPath --namespace file-simulator --create-namespace
        } else {
            Write-Host "  Chart not found at: $chartPath" -ForegroundColor Red
        }
    } else {
        Write-Host "  Helm not installed. Install from: https://helm.sh/docs/intro/install/" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n[6/6] Skipping Helm deployment (use -DeployChart to deploy)" -ForegroundColor Gray
}

# Summary
Write-Host "`n=============================================" -ForegroundColor Cyan
Write-Host "  Setup Complete!" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Directory Structure:" -ForegroundColor White
Write-Host "  $SimulatorPath\"
Write-Host "  ├── input\       <- General input files"
Write-Host "  ├── output\      <- General output files"
Write-Host "  ├── temp\        <- Temporary files"
Write-Host "  ├── config\      <- Helper scripts"
Write-Host "  ├── nas-input-1\ <- NAS input server 1"
Write-Host "  ├── nas-input-2\ <- NAS input server 2"
Write-Host "  ├── nas-input-3\ <- NAS input server 3"
Write-Host "  ├── nas-backup\  <- NAS backup server (read-only)"
Write-Host "  ├── nas-output-1\<- NAS output server 1"
Write-Host "  ├── nas-output-2\<- NAS output server 2"
Write-Host "  └── nas-output-3\<- NAS output server 3"
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor White
Write-Host "  1. Ensure Minikube has the mount:"
Write-Host "     minikube start --mount --mount-string='$SimulatorPath`:/mnt/simulator-data'"
Write-Host ""
Write-Host "  2. Deploy the Helm chart:"
Write-Host "     helm upgrade --install file-sim .\helm-chart\file-simulator --namespace file-simulator --create-namespace"
Write-Host ""
Write-Host "  3. Access Management UI:"
Write-Host "     http://$(minikube ip):30080"
Write-Host ""
