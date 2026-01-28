# Configure stable hostname for file-simulator cluster
# Run as Administrator

$CURRENT_IP = minikube ip -p file-simulator
$HOSTNAME = "file-simulator.local"
$HOSTS_FILE = "C:\Windows\System32\drivers\etc\hosts"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  File Simulator - Stable Hostname Setup" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

Write-Host "Current IP: $CURRENT_IP" -ForegroundColor Yellow
Write-Host "Hostname:   $HOSTNAME`n" -ForegroundColor Yellow

# Remove old entries for this hostname
$content = Get-Content $HOSTS_FILE -ErrorAction SilentlyContinue
$newContent = $content | Where-Object { $_ -notmatch "file-simulator\.local" }

# Add new entry
$newContent += ""
$newContent += "# File Simulator Suite - Minikube Hyper-V Cluster"
$newContent += "$CURRENT_IP    $HOSTNAME"

# Write back to hosts file
$newContent | Set-Content $HOSTS_FILE -Force

Write-Host "✅ Hosts file updated!" -ForegroundColor Green
Write-Host "`nEntry added:" -ForegroundColor White
Write-Host "  $CURRENT_IP    $HOSTNAME" -ForegroundColor Gray

Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "  Usage in your ez-platform configuration:" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

Write-Host @"

Instead of using the IP address, use the hostname:

OLD (IP-based):
  FTP_HOST: "$CURRENT_IP"
  SFTP_HOST: "$CURRENT_IP"
  S3_ENDPOINT: "http://$CURRENT_IP:30900"

NEW (hostname-based):
  FTP_HOST: "$HOSTNAME"
  SFTP_HOST: "$HOSTNAME"
  S3_ENDPOINT: "http://${HOSTNAME}:30900"

Service URLs:
  Management UI: http://${HOSTNAME}:30180
  HTTP Server:   http://${HOSTNAME}:30088
  S3 Console:    http://${HOSTNAME}:30901
  FTP:           ftp://${HOSTNAME}:30021
  SFTP:          sftp://${HOSTNAME}:30022
  NFS:           ${HOSTNAME}:32149
  SMB:           \\<LoadBalancer-IP>\simulator

"@

Write-Host "`n✅ Complete! Use 'file-simulator.local' in all configurations." -ForegroundColor Green
Write-Host "`n⚠️  If cluster IP changes, re-run this script to update hosts file." -ForegroundColor Yellow
