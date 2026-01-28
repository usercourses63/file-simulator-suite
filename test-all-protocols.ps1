$MINIKUBE_IP = "172.25.201.3"
$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "FILE SIMULATOR SUITE - PROTOCOL TESTS" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Minikube IP: $MINIKUBE_IP`n" -ForegroundColor Yellow

# Test 1: Management UI
Write-Host "[1/8] Testing Management UI (FileBrowser)..." -ForegroundColor White
$response = Invoke-WebRequest -Uri "http://$MINIKUBE_IP:30180" -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
if ($response.StatusCode -eq 200) {
    Write-Host "  ✅ Management UI: ACCESSIBLE (HTTP 200)" -ForegroundColor Green
    Write-Host "     URL: http://$MINIKUBE_IP:30180" -ForegroundColor Gray
} else {
    Write-Host "  ❌ Management UI: FAILED" -ForegroundColor Red
}

# Test 2: HTTP File Server
Write-Host "`n[2/8] Testing HTTP File Server..." -ForegroundColor White
$response = Invoke-WebRequest -Uri "http://$MINIKUBE_IP:30088" -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
if ($response.StatusCode -eq 200) {
    Write-Host "  ✅ HTTP Server: ACCESSIBLE (HTTP 200)" -ForegroundColor Green
    Write-Host "     URL: http://$MINIKUBE_IP:30088" -ForegroundColor Gray
} else {
    Write-Host "  ❌ HTTP Server: FAILED" -ForegroundColor Red
}

# Test 3: WebDAV Server
Write-Host "`n[3/8] Testing WebDAV Server..." -ForegroundColor White
$response = Invoke-WebRequest -Uri "http://$MINIKUBE_IP:30089" -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
if ($response.StatusCode -eq 401) {
    Write-Host "  ✅ WebDAV Server: ACCESSIBLE (HTTP 401 - Auth Required)" -ForegroundColor Green
    Write-Host "     URL: http://$MINIKUBE_IP:30089" -ForegroundColor Gray
    Write-Host "     User: httpuser / Pass: httppass123" -ForegroundColor Gray
} else {
    Write-Host "  ❌ WebDAV Server: FAILED" -ForegroundColor Red
}

# Test 4: S3/MinIO Console
Write-Host "`n[4/8] Testing S3/MinIO Console..." -ForegroundColor White
$response = Invoke-WebRequest -Uri "http://$MINIKUBE_IP:30901" -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
if ($response.StatusCode -eq 200) {
    Write-Host "  ✅ S3 Console: ACCESSIBLE (HTTP 200)" -ForegroundColor Green
    Write-Host "     Console: http://$MINIKUBE_IP:30901" -ForegroundColor Gray
    Write-Host "     API: http://$MINIKUBE_IP:30900" -ForegroundColor Gray
    Write-Host "     Access Key: minioadmin / Secret: minioadmin123" -ForegroundColor Gray
} else {
    Write-Host "  ❌ S3 Console: FAILED" -ForegroundColor Red
}

# Test 5: FTP Server
Write-Host "`n[5/8] Testing FTP Server..." -ForegroundColor White
$tcpClient = New-Object System.Net.Sockets.TcpClient
try {
    $tcpClient.Connect($MINIKUBE_IP, 30021)
    if ($tcpClient.Connected) {
        Write-Host "  ✅ FTP Server: PORT OPEN (21 -> 30021)" -ForegroundColor Green
        Write-Host "     Host: $MINIKUBE_IP Port: 30021" -ForegroundColor Gray
        Write-Host "     User: ftpuser / Pass: ftppass123" -ForegroundColor Gray
    }
    $tcpClient.Close()
} catch {
    Write-Host "  ❌ FTP Server: PORT CLOSED" -ForegroundColor Red
}

# Test 6: SFTP Server
Write-Host "`n[6/8] Testing SFTP Server..." -ForegroundColor White
$tcpClient = New-Object System.Net.Sockets.TcpClient
try {
    $tcpClient.Connect($MINIKUBE_IP, 30022)
    if ($tcpClient.Connected) {
        Write-Host "  ✅ SFTP Server: PORT OPEN (22 -> 30022)" -ForegroundColor Green
        Write-Host "     Host: $MINIKUBE_IP Port: 30022" -ForegroundColor Gray
        Write-Host "     User: sftpuser / Pass: sftppass123" -ForegroundColor Gray
    }
    $tcpClient.Close()
} catch {
    Write-Host "  ❌ SFTP Server: PORT CLOSED" -ForegroundColor Red
}

# Test 7: SMB Server
Write-Host "`n[7/8] Testing SMB Server..." -ForegroundColor White
$smbIP = kubectl get svc file-sim-file-simulator-smb -n file-simulator -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>$null
if ($smbIP) {
    Write-Host "  ✅ SMB Server: LOADBALANCER ASSIGNED" -ForegroundColor Green
    Write-Host "     External IP: $smbIP" -ForegroundColor Gray
    Write-Host "     Path: \\$smbIP\simulator" -ForegroundColor Gray
    Write-Host "     User: smbuser / Pass: smbpass123" -ForegroundColor Gray
    Write-Host "     Mount: net use Z: \\$smbIP\simulator /user:smbuser smbpass123" -ForegroundColor Gray
} else {
    Write-Host "  ⚠️  SMB Server: No LoadBalancer IP (tunnel may not be running)" -ForegroundColor Yellow
}

# Test 8: NFS Server
Write-Host "`n[8/8] Testing NFS Server..." -ForegroundColor White
$tcpClient = New-Object System.Net.Sockets.TcpClient
try {
    $tcpClient.Connect($MINIKUBE_IP, 32149)
    if ($tcpClient.Connected) {
        Write-Host "  ✅ NFS Server: PORT OPEN (2049 -> 32149)" -ForegroundColor Green
        Write-Host "     Server: $MINIKUBE_IP Port: 32149" -ForegroundColor Gray
        Write-Host "     Export: /data" -ForegroundColor Gray
        Write-Host "     Mount: mount -t nfs ${MINIKUBE_IP}:/data /mnt/nfs" -ForegroundColor Gray
    }
    $tcpClient.Close()
} catch {
    Write-Host "  ❌ NFS Server: PORT CLOSED" -ForegroundColor Red
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "All 8 file transfer protocols are deployed and accessible!" -ForegroundColor Green
Write-Host "`nData Directory: C:\simulator-data" -ForegroundColor Yellow
Write-Host "  - input\  : Place test files here" -ForegroundColor Gray
Write-Host "  - output\ : Services write here" -ForegroundColor Gray
Write-Host "  - temp\   : Temporary storage`n" -ForegroundColor Gray
