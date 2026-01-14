# File Simulator Suite - Verification Test Script
# Tests all protocols to ensure the simulator is working correctly

param(
    [string]$MinikubeIp = "",
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Success { param($msg) Write-Host "  ✅ $msg" -ForegroundColor Green }
function Write-Failure { param($msg) Write-Host "  ❌ $msg" -ForegroundColor Red }
function Write-Info { param($msg) Write-Host "  ℹ️  $msg" -ForegroundColor Cyan }
function Write-TestHeader { param($msg) Write-Host "`n═══ $msg ═══" -ForegroundColor Yellow }

# Get Minikube IP if not provided
if (-not $MinikubeIp) {
    $MinikubeIp = (minikube ip 2>$null)
    if (-not $MinikubeIp) {
        Write-Host "ERROR: Could not get Minikube IP. Is Minikube running?" -ForegroundColor Red
        exit 1
    }
}

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  File Simulator Suite - Verification Tests" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Minikube IP: $MinikubeIp"

$testFile = "test-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
$testContent = "Hello from File Simulator Test - $(Get-Date)"
$tempDir = Join-Path $env:TEMP "file-simulator-tests"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

$results = @{
    Passed = 0
    Failed = 0
    Skipped = 0
}

# ============================================
# Test 1: Kubernetes Connectivity
# ============================================
Write-TestHeader "TEST 1: Kubernetes Connectivity"

try {
    $pods = kubectl get pods -n file-simulator -o json 2>$null | ConvertFrom-Json
    $runningPods = $pods.items | Where-Object { $_.status.phase -eq "Running" }
    
    Write-Info "Found $($runningPods.Count) running pods in file-simulator namespace"
    
    foreach ($pod in $runningPods) {
        $name = $pod.metadata.name
        $ready = ($pod.status.containerStatuses | Where-Object { $_.ready }).Count
        $total = $pod.status.containerStatuses.Count
        Write-Success "$name ($ready/$total ready)"
    }
    $results.Passed++
}
catch {
    Write-Failure "Cannot connect to Kubernetes: $_"
    $results.Failed++
}

# ============================================
# Test 2: Management UI (FileBrowser)
# ============================================
Write-TestHeader "TEST 2: Management UI (FileBrowser)"

try {
    $response = Invoke-WebRequest -Uri "http://${MinikubeIp}:30080/health" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Success "Management UI is accessible at http://${MinikubeIp}:30080"
        $results.Passed++
    }
}
catch {
    Write-Failure "Management UI not accessible: $_"
    $results.Failed++
}

# ============================================
# Test 3: HTTP File Server
# ============================================
Write-TestHeader "TEST 3: HTTP File Server"

try {
    # Test health endpoint
    $response = Invoke-WebRequest -Uri "http://${MinikubeIp}:30088/health" -TimeoutSec 10 -UseBasicParsing
    Write-Success "HTTP server health check passed"

    # Test directory listing
    $response = Invoke-WebRequest -Uri "http://${MinikubeIp}:30088/" -TimeoutSec 10 -UseBasicParsing
    Write-Success "HTTP directory listing works"

    # Test WebDAV upload
    $testFilePath = Join-Path $tempDir "http-test.txt"
    $testContent | Out-File $testFilePath -Encoding UTF8
    
    $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("httpuser:httppass123"))
    $headers = @{ Authorization = "Basic $cred" }
    
    $uploadResult = Invoke-WebRequest `
        -Uri "http://${MinikubeIp}:30088/webdav/http-test.txt" `
        -Method PUT `
        -InFile $testFilePath `
        -Headers $headers `
        -TimeoutSec 30 `
        -UseBasicParsing
    
    Write-Success "HTTP WebDAV upload successful"
    $results.Passed++
}
catch {
    Write-Failure "HTTP test failed: $_"
    $results.Failed++
}

# ============================================
# Test 4: S3/MinIO
# ============================================
Write-TestHeader "TEST 4: S3/MinIO"

try {
    # Test MinIO health
    $response = Invoke-WebRequest -Uri "http://${MinikubeIp}:30900/minio/health/live" -TimeoutSec 10 -UseBasicParsing
    Write-Success "MinIO health check passed"

    # Test MinIO Console
    $response = Invoke-WebRequest -Uri "http://${MinikubeIp}:30901" -TimeoutSec 10 -UseBasicParsing
    Write-Success "MinIO Console accessible at http://${MinikubeIp}:30901"

    # Test S3 operations with AWS CLI (if available)
    $awsCli = Get-Command aws -ErrorAction SilentlyContinue
    if ($awsCli) {
        $env:AWS_ACCESS_KEY_ID = "minioadmin"
        $env:AWS_SECRET_ACCESS_KEY = "minioadmin123"
        
        # List buckets
        $buckets = aws --endpoint-url "http://${MinikubeIp}:30900" s3 ls 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "S3 bucket listing works"
            
            # Try upload
            $testFilePath = Join-Path $tempDir "s3-test.txt"
            $testContent | Out-File $testFilePath -Encoding UTF8
            
            aws --endpoint-url "http://${MinikubeIp}:30900" s3 cp $testFilePath s3://input/s3-test.txt 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Success "S3 file upload successful"
            }
        }
    }
    else {
        Write-Info "AWS CLI not found - skipping S3 upload test"
    }
    
    $results.Passed++
}
catch {
    Write-Failure "S3/MinIO test failed: $_"
    $results.Failed++
}

# ============================================
# Test 5: FTP Server
# ============================================
Write-TestHeader "TEST 5: FTP Server"

try {
    # Test TCP connectivity
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect($MinikubeIp, 30021)
    
    if ($tcpClient.Connected) {
        Write-Success "FTP port 30021 is accessible"
        $tcpClient.Close()
        
        # Note: Full FTP test requires FTP client
        Write-Info "For full FTP test, use: ftp ${MinikubeIp} 30021 (user: ftpuser, pass: ftppass123)"
        $results.Passed++
    }
}
catch {
    Write-Failure "FTP connection failed: $_"
    $results.Failed++
}

# ============================================
# Test 6: SFTP Server
# ============================================
Write-TestHeader "TEST 6: SFTP Server"

try {
    # Test TCP connectivity
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect($MinikubeIp, 30022)
    
    if ($tcpClient.Connected) {
        Write-Success "SFTP port 30022 is accessible"
        $tcpClient.Close()
        
        Write-Info "For full SFTP test, use: sftp -P 30022 sftpuser@${MinikubeIp}"
        $results.Passed++
    }
}
catch {
    Write-Failure "SFTP connection failed: $_"
    $results.Failed++
}

# ============================================
# Test 7: SMB Server
# ============================================
Write-TestHeader "TEST 7: SMB Server"

try {
    # Test TCP connectivity
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect($MinikubeIp, 30445)
    
    if ($tcpClient.Connected) {
        Write-Success "SMB port 30445 is accessible"
        $tcpClient.Close()
        
        Write-Info "Map drive: net use Z: \\${MinikubeIp}\simulator /user:smbuser smbpass123"
        $results.Passed++
    }
}
catch {
    Write-Failure "SMB connection failed: $_"
    $results.Failed++
}

# ============================================
# Test 8: NFS Server
# ============================================
Write-TestHeader "TEST 8: NFS Server"

try {
    # Test TCP connectivity to NFS port
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect($MinikubeIp, 32049)
    
    if ($tcpClient.Connected) {
        Write-Success "NFS port 32049 is accessible"
        $tcpClient.Close()
        
        Write-Info "Mount (Linux): sudo mount -t nfs ${MinikubeIp}:/data /mnt/nfs"
        $results.Passed++
    }
}
catch {
    Write-Failure "NFS connection failed: $_"
    $results.Failed++
}

# ============================================
# Test 9: Cross-Protocol File Sharing
# ============================================
Write-TestHeader "TEST 9: Cross-Protocol File Sharing"

try {
    # Upload via HTTP, check it's visible
    $crossTestFile = "cross-protocol-test-$(Get-Date -Format 'HHmmss').txt"
    $crossTestPath = Join-Path $tempDir $crossTestFile
    "Cross-protocol test content" | Out-File $crossTestPath -Encoding UTF8
    
    $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("httpuser:httppass123"))
    $headers = @{ Authorization = "Basic $cred" }
    
    Invoke-WebRequest `
        -Uri "http://${MinikubeIp}:30088/webdav/$crossTestFile" `
        -Method PUT `
        -InFile $crossTestPath `
        -Headers $headers `
        -TimeoutSec 30 `
        -UseBasicParsing | Out-Null
    
    # Verify via HTTP GET
    $downloadResponse = Invoke-WebRequest `
        -Uri "http://${MinikubeIp}:30088/download/$crossTestFile" `
        -TimeoutSec 10 `
        -UseBasicParsing
    
    if ($downloadResponse.StatusCode -eq 200) {
        Write-Success "Cross-protocol test: File uploaded via WebDAV, downloadable via HTTP"
        $results.Passed++
    }
}
catch {
    Write-Failure "Cross-protocol test failed: $_"
    $results.Failed++
}

# ============================================
# Summary
# ============================================
Write-Host "`n=============================================" -ForegroundColor Cyan
Write-Host "  Test Results Summary" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Passed:  $($results.Passed)" -ForegroundColor Green
Write-Host "  Failed:  $($results.Failed)" -ForegroundColor Red
Write-Host "  Skipped: $($results.Skipped)" -ForegroundColor Yellow
Write-Host ""

# Cleanup
Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

if ($results.Failed -eq 0) {
    Write-Host "🎉 All tests passed! File Simulator Suite is working correctly." -ForegroundColor Green
    exit 0
}
else {
    Write-Host "⚠️  Some tests failed. Check the output above for details." -ForegroundColor Yellow
    exit 1
}
