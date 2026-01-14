# File Simulator Suite - Windows Protocol Testing Script
# Tests all protocols directly from Windows (no WSL needed for most)

param(
    [string]$MinikubeIp = "",
    [switch]$Interactive = $false
)

$ErrorActionPreference = "Stop"

# Get Minikube IP
if (-not $MinikubeIp) {
    $MinikubeIp = (minikube ip 2>$null)
    if (-not $MinikubeIp) {
        Write-Host "ERROR: Could not get Minikube IP. Is Minikube running?" -ForegroundColor Red
        exit 1
    }
}

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  File Simulator - Windows Protocol Tests" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Minikube IP: $MinikubeIp`n"

$testFile = "windows-test-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
$testContent = "Hello from Windows! Created at $(Get-Date)"
$tempDir = Join-Path $env:TEMP "file-simulator-tests"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
$localTestFile = Join-Path $tempDir $testFile
$testContent | Out-File $localTestFile -Encoding UTF8

# ============================================
# TEST 1: HTTP (Browser & PowerShell)
# ============================================
Write-Host "═══ TEST 1: HTTP File Server ═══" -ForegroundColor Yellow

try {
    # Health check
    $health = Invoke-WebRequest -Uri "http://${MinikubeIp}:30088/health" -UseBasicParsing -TimeoutSec 10
    Write-Host "  [OK] HTTP server is healthy" -ForegroundColor Green
    
    # Directory listing
    $listing = Invoke-WebRequest -Uri "http://${MinikubeIp}:30088/" -UseBasicParsing -TimeoutSec 10
    Write-Host "  [OK] Directory listing works" -ForegroundColor Green
    
    # WebDAV Upload
    $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("httpuser:httppass123"))
    $headers = @{ Authorization = "Basic $cred" }
    
    Invoke-WebRequest -Uri "http://${MinikubeIp}:30088/webdav/$testFile" `
        -Method PUT -InFile $localTestFile -Headers $headers -UseBasicParsing | Out-Null
    Write-Host "  [OK] WebDAV upload successful" -ForegroundColor Green
    
    # Download
    $downloaded = Invoke-WebRequest -Uri "http://${MinikubeIp}:30088/download/$testFile" -UseBasicParsing
    Write-Host "  [OK] HTTP download successful" -ForegroundColor Green
    
    Write-Host "`n  URLs to open in browser:" -ForegroundColor Cyan
    Write-Host "    Browse files: http://${MinikubeIp}:30088/"
    Write-Host "    Management:   http://${MinikubeIp}:30180/ (admin/admin123)"
    
    if ($Interactive) {
        Start-Process "http://${MinikubeIp}:30088/"
    }
}
catch {
    Write-Host "  [FAIL] HTTP test failed: $_" -ForegroundColor Red
}

# ============================================
# TEST 2: S3/MinIO (PowerShell & Browser)
# ============================================
Write-Host "`n═══ TEST 2: S3/MinIO ═══" -ForegroundColor Yellow

try {
    # Health check
    $health = Invoke-WebRequest -Uri "http://${MinikubeIp}:30900/minio/health/live" -UseBasicParsing -TimeoutSec 10
    Write-Host "  [OK] MinIO is healthy" -ForegroundColor Green
    
    # Console accessible
    $console = Invoke-WebRequest -Uri "http://${MinikubeIp}:30901" -UseBasicParsing -TimeoutSec 10
    Write-Host "  [OK] MinIO Console accessible" -ForegroundColor Green
    
    Write-Host "`n  MinIO Console: http://${MinikubeIp}:30901/" -ForegroundColor Cyan
    Write-Host "    Username: minioadmin"
    Write-Host "    Password: minioadmin123"
    
    # Test with AWS CLI if available
    $awsCli = Get-Command aws -ErrorAction SilentlyContinue
    if ($awsCli) {
        $env:AWS_ACCESS_KEY_ID = "minioadmin"
        $env:AWS_SECRET_ACCESS_KEY = "minioadmin123"
        
        # List buckets
        $buckets = aws --endpoint-url "http://${MinikubeIp}:30900" s3 ls 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] S3 bucket listing works" -ForegroundColor Green
            
            # Upload
            aws --endpoint-url "http://${MinikubeIp}:30900" s3 cp $localTestFile "s3://input/$testFile" 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  [OK] S3 upload successful" -ForegroundColor Green
            }
            
            # Download
            $s3Download = Join-Path $tempDir "s3-downloaded.txt"
            aws --endpoint-url "http://${MinikubeIp}:30900" s3 cp "s3://input/$testFile" $s3Download 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  [OK] S3 download successful" -ForegroundColor Green
            }
        }
    } else {
        Write-Host "  [INFO]  AWS CLI not found - install for S3 command line access" -ForegroundColor Cyan
        Write-Host "      Install: winget install Amazon.AWSCLI" -ForegroundColor Gray
    }
    
    if ($Interactive) {
        Start-Process "http://${MinikubeIp}:30901/"
    }
}
catch {
    Write-Host "  [FAIL] S3/MinIO test failed: $_" -ForegroundColor Red
}

# ============================================
# TEST 3: SMB (Windows Native!)
# ============================================
Write-Host "`n═══ TEST 3: SMB/CIFS (Windows Native) ═══" -ForegroundColor Yellow

try {
    # Test port connectivity
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect($MinikubeIp, 30445)
    
    if ($tcpClient.Connected) {
        Write-Host "  [OK] SMB port 30445 is accessible" -ForegroundColor Green
        $tcpClient.Close()
        
        Write-Host "`n  Map network drive:" -ForegroundColor Cyan
        Write-Host "    net use Z: \\${MinikubeIp}\simulator /user:smbuser smbpass123"
        Write-Host ""
        Write-Host "  Or open in Explorer:" -ForegroundColor Cyan
        Write-Host "    \\${MinikubeIp}\simulator"
        
        if ($Interactive) {
            # Try to map the drive
            $mapResult = net use Z: "\\${MinikubeIp}\simulator" /user:smbuser smbpass123 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  [OK] Drive Z: mapped successfully" -ForegroundColor Green
                
                # Write test file
                Copy-Item $localTestFile "Z:\$testFile" -Force
                Write-Host "  [OK] SMB write successful" -ForegroundColor Green
                
                # Read it back
                $smbContent = Get-Content "Z:\$testFile"
                Write-Host "  [OK] SMB read successful" -ForegroundColor Green
                
                # Open Explorer
                Start-Process "explorer.exe" "Z:\"
            } else {
                Write-Host "  [WARN]  Could not map drive automatically: $mapResult" -ForegroundColor Yellow
            }
        }
    }
}
catch {
    Write-Host "  [FAIL] SMB test failed: $_" -ForegroundColor Red
    Write-Host "  Tip: Try in Explorer: \\${MinikubeIp}\simulator" -ForegroundColor Yellow
}

# ============================================
# TEST 4: FTP (Windows Native)
# ============================================
Write-Host "`n═══ TEST 4: FTP ═══" -ForegroundColor Yellow

try {
    # Test port connectivity
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect($MinikubeIp, 30021)
    
    if ($tcpClient.Connected) {
        Write-Host "  [OK] FTP port 30021 is accessible" -ForegroundColor Green
        $tcpClient.Close()
        
        # Try FTP upload using .NET
        try {
            $ftpUri = "ftp://${MinikubeIp}:30021/$testFile"
            $ftpRequest = [System.Net.FtpWebRequest]::Create($ftpUri)
            $ftpRequest.Method = [System.Net.WebRequestMethods+Ftp]::UploadFile
            $ftpRequest.Credentials = New-Object System.Net.NetworkCredential("ftpuser", "ftppass123")
            $ftpRequest.UseBinary = $true
            $ftpRequest.UsePassive = $true
            
            $fileContent = [System.IO.File]::ReadAllBytes($localTestFile)
            $ftpRequest.ContentLength = $fileContent.Length
            
            $requestStream = $ftpRequest.GetRequestStream()
            $requestStream.Write($fileContent, 0, $fileContent.Length)
            $requestStream.Close()
            
            $response = $ftpRequest.GetResponse()
            Write-Host "  [OK] FTP upload successful" -ForegroundColor Green
            $response.Close()
        }
        catch {
            Write-Host "  [WARN]  FTP upload via .NET failed (passive mode issue)" -ForegroundColor Yellow
            Write-Host "      Use FileZilla or WinSCP instead" -ForegroundColor Gray
        }
        
        Write-Host "`n  FTP Connection:" -ForegroundColor Cyan
        Write-Host "    Host: $MinikubeIp"
        Write-Host "    Port: 30021"
        Write-Host "    User: ftpuser"
        Write-Host "    Pass: ftppass123"
        Write-Host ""
        Write-Host "  In Explorer address bar:" -ForegroundColor Cyan
        Write-Host "    ftp://ftpuser:ftppass123@${MinikubeIp}:30021/"
        
        if ($Interactive) {
            Start-Process "ftp://ftpuser:ftppass123@${MinikubeIp}:30021/"
        }
    }
}
catch {
    Write-Host "  [FAIL] FTP test failed: $_" -ForegroundColor Red
}

# ============================================
# TEST 5: SFTP (Needs SSH Client)
# ============================================
Write-Host "`n═══ TEST 5: SFTP ═══" -ForegroundColor Yellow

try {
    # Test port connectivity
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect($MinikubeIp, 30022)
    
    if ($tcpClient.Connected) {
        Write-Host "  [OK] SFTP port 30022 is accessible" -ForegroundColor Green
        $tcpClient.Close()
        
        # Check for OpenSSH
        $sftpCmd = Get-Command sftp -ErrorAction SilentlyContinue
        $scpCmd = Get-Command scp -ErrorAction SilentlyContinue
        
        if ($sftpCmd) {
            Write-Host "  [OK] OpenSSH sftp client found" -ForegroundColor Green
            Write-Host "`n  SFTP command:" -ForegroundColor Cyan
            Write-Host "    sftp -P 30022 sftpuser@${MinikubeIp}"
            Write-Host "    Password: sftppass123"
        } else {
            Write-Host "  [INFO]  OpenSSH not found" -ForegroundColor Cyan
            Write-Host "      Install: Settings > Apps > Optional Features > OpenSSH Client" -ForegroundColor Gray
        }
        
        # Check for WinSCP
        $winscpPaths = @(
            "$env:ProgramFiles\WinSCP\WinSCP.exe",
            "${env:ProgramFiles(x86)}\WinSCP\WinSCP.exe",
            "$env:LOCALAPPDATA\Programs\WinSCP\WinSCP.exe"
        )
        $winscp = $winscpPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
        
        if ($winscp) {
            Write-Host "  [OK] WinSCP found" -ForegroundColor Green
            if ($Interactive) {
                Start-Process $winscp "sftp://sftpuser:sftppass123@${MinikubeIp}:30022/"
            }
        } else {
            Write-Host "`n  Recommended: Install WinSCP" -ForegroundColor Cyan
            Write-Host "    winget install WinSCP.WinSCP" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Host "  [FAIL] SFTP test failed: $_" -ForegroundColor Red
}

# ============================================
# TEST 6: NFS (Requires Windows Feature)
# ============================================
Write-Host "`n═══ TEST 6: NFS ═══" -ForegroundColor Yellow

try {
    # Test port connectivity
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect($MinikubeIp, 32149)
    
    if ($tcpClient.Connected) {
        Write-Host "  [OK] NFS port 32149 is accessible" -ForegroundColor Green
        $tcpClient.Close()
        
        # Check if NFS client is installed
        $nfsClient = Get-WindowsOptionalFeature -Online -FeatureName "ServicesForNFS-ClientOnly" -ErrorAction SilentlyContinue
        
        if ($nfsClient -and $nfsClient.State -eq "Enabled") {
            Write-Host "  [OK] Windows NFS Client is enabled" -ForegroundColor Green
            
            Write-Host "`n  Mount NFS:" -ForegroundColor Cyan
            Write-Host "    mount -o anon \\${MinikubeIp}\data N:"
            
            if ($Interactive) {
                # Try to mount
                $mountResult = mount -o anon "\\${MinikubeIp}\data" N: 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "  [OK] NFS mounted to N:" -ForegroundColor Green
                    Start-Process "explorer.exe" "N:\"
                }
            }
        } else {
            Write-Host "  [WARN]  Windows NFS Client not enabled" -ForegroundColor Yellow
            Write-Host "`n  To enable NFS Client (requires Admin PowerShell):" -ForegroundColor Cyan
            Write-Host "    Enable-WindowsOptionalFeature -Online -FeatureName ServicesForNFS-ClientOnly -All" -ForegroundColor Gray
            Write-Host ""
            Write-Host "  Or use Settings:" -ForegroundColor Cyan
            Write-Host "    Settings > Apps > Optional Features > Add > NFS Client" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Host "  [FAIL] NFS test failed: $_" -ForegroundColor Red
}

# ============================================
# SUMMARY
# ============================================
Write-Host "`n=============================================" -ForegroundColor Cyan
Write-Host "  Quick Access URLs & Commands" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

Write-Host "`n[MGMT] Management UI:" -ForegroundColor White
Write-Host "   http://${MinikubeIp}:30180/  (admin / admin123)"

Write-Host "`n[HTTP] HTTP:" -ForegroundColor White
Write-Host "   Browse: http://${MinikubeIp}:30088/"
Write-Host "   WebDAV: http://${MinikubeIp}:30088/webdav/ (httpuser / httppass123)"

Write-Host "`n[S3] S3/MinIO:" -ForegroundColor White
Write-Host "   Console: http://${MinikubeIp}:30901/ (minioadmin / minioadmin123)"
Write-Host "   API: http://${MinikubeIp}:30900/"

Write-Host "`n[SMB] SMB (Windows Native):" -ForegroundColor White
Write-Host "   Explorer: \\${MinikubeIp}\simulator"
Write-Host "   Map: net use Z: \\${MinikubeIp}\simulator /user:smbuser smbpass123"

Write-Host "`n[FTP] FTP:" -ForegroundColor White
Write-Host "   Explorer: ftp://ftpuser:ftppass123@${MinikubeIp}:30021/"

Write-Host "`n[SFTP] SFTP:" -ForegroundColor White
Write-Host "   WinSCP/PuTTY: sftp://sftpuser@${MinikubeIp}:30022/ (sftppass123)"

Write-Host "`n[NFS] NFS:" -ForegroundColor White
Write-Host "   Mount: mount -o anon \\${MinikubeIp}\data N:"
Write-Host "   (Requires NFS Client feature enabled)"

# Cleanup
Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`nTIP: Run with -Interactive to auto-open connections" -ForegroundColor Gray
Write-Host "   .\scripts\test-windows.ps1 -Interactive`n" -ForegroundColor Gray
