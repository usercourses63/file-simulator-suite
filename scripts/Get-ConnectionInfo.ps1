<#
.SYNOPSIS
    Gets connection information for file-simulator services.

.DESCRIPTION
    Queries the Control API to get connection details for all file simulator
    services. Outputs in various formats suitable for configuration.

.PARAMETER Format
    Output format: json (default), env, shell, yaml, or dotnet

.PARAMETER ApiUrl
    Control API URL. Default: http://file-simulator.local:30500

.PARAMETER Save
    Save output to a file

.EXAMPLE
    .\Get-ConnectionInfo.ps1

.EXAMPLE
    .\Get-ConnectionInfo.ps1 -Format env -Save .env

.EXAMPLE
    .\Get-ConnectionInfo.ps1 -Format yaml -Save config/file-simulator.yaml
#>

param(
    [ValidateSet("json", "env", "shell", "yaml", "dotnet")]
    [string]$Format = "json",

    [string]$ApiUrl = "http://file-simulator.local:30500",

    [string]$Save
)

$ErrorActionPreference = "Stop"

Write-Host "Fetching connection info from $ApiUrl..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "$ApiUrl/api/connection-info?format=$Format" -Method Get -TimeoutSec 10

    if ($Save) {
        if ($Format -eq "json") {
            $response | ConvertTo-Json -Depth 10 | Set-Content -Path $Save
        } else {
            $response | Set-Content -Path $Save
        }
        Write-Host "Connection info saved to: $Save" -ForegroundColor Green
    } else {
        if ($Format -eq "json") {
            $response | ConvertTo-Json -Depth 10
        } else {
            $response
        }
    }
} catch {
    Write-Host "Error: Failed to get connection info" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Yellow

    # Fallback to default values
    Write-Host "`nUsing default connection info:" -ForegroundColor Cyan

    $defaultInfo = @"
FILE_SIMULATOR_HOST=file-simulator.local

# Endpoints
FILE_SIMULATOR_DASHBOARD_URL=http://file-simulator.local:30080
FILE_SIMULATOR_API_URL=http://file-simulator.local:30500

# FTP
FILE_FTP_HOST=file-simulator.local
FILE_FTP_PORT=30021
FILE_FTP_USERNAME=simuser
FILE_FTP_PASSWORD=simpass123

# SFTP
FILE_SFTP_HOST=file-simulator.local
FILE_SFTP_PORT=30022
FILE_SFTP_USERNAME=simuser
FILE_SFTP_PASSWORD=simpass123

# HTTP/WebDAV
FILE_HTTP_URL=http://file-simulator.local:30088
FILE_HTTP_USERNAME=admin
FILE_HTTP_PASSWORD=admin

# S3/MinIO
FILE_S3_ENDPOINT=http://file-simulator.local:30900
FILE_S3_ACCESS_KEY=minioadmin
FILE_S3_SECRET_KEY=minioadmin
FILE_S3_BUCKET=simulator

# SMB
FILE_SMB_PATH=\\file-simulator.local\shared
FILE_SMB_USERNAME=simuser
FILE_SMB_PASSWORD=simpass

# NFS
FILE_NFS_SERVER=file-simulator.local
FILE_NFS_PORT=32049
FILE_NFS_PATH=/data
"@

    if ($Save) {
        $defaultInfo | Set-Content -Path $Save
        Write-Host "Default connection info saved to: $Save" -ForegroundColor Green
    } else {
        Write-Host $defaultInfo
    }
}
