<#
.SYNOPSIS
    Comprehensive production verification for File Simulator Suite.

.DESCRIPTION
    This script performs exhaustive testing of all components:
    - Kubernetes cluster health (5 tests)
    - Management UI and Control API (6 tests)
    - Protocol connectivity - FTP/SFTP/HTTP/S3/SMB/NFS/Kafka (7 tests)
    - File operations - upload/download/delete (4 tests)
    - Kafka integration - topics/produce/consume (4 tests)
    - Dynamic server management (5 tests, optional)
    - Historical metrics (2 tests)
    - Alert system (4 tests)

    Total: 37 tests (42 with -IncludeDynamic)

.PARAMETER Profile
    Minikube profile name. Default: file-simulator

.PARAMETER IncludeDynamic
    Include dynamic server creation/deletion tests (adds 5 tests).
    These tests modify the cluster by creating and deleting servers.

.EXAMPLE
    .\Verify-Production.ps1
    Run standard verification (37 tests)

.EXAMPLE
    .\Verify-Production.ps1 -IncludeDynamic
    Run full verification including dynamic server tests (42 tests)

.EXAMPLE
    .\Verify-Production.ps1 -Profile my-profile
    Run verification against custom Minikube profile

.NOTES
    Requires: kubectl, curl
    Optional: Test data directory at C:\simulator-data
#>

param(
    [string]$Profile = "file-simulator",
    [switch]$IncludeDynamic
)

$ErrorActionPreference = "Continue"  # Collect all results

# Script configuration
$BaseUrl = "http://file-simulator.local:30500"
$DashboardUrl = "http://file-simulator.local:30080"

# Test tracking
$script:totalTests = 0
$script:passedTests = 0
$script:failedTests = 0
$script:testResults = @()

#region Helper Functions

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [int]$ExpectedStatus = 200,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [object]$Body = $null
    )

    $script:totalTests++

    try {
        $params = @{
            Uri = $Url
            Method = $Method
            TimeoutSec = 10
            UseBasicParsing = $true
            Headers = $Headers
        }

        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
            $params.ContentType = "application/json"
        }

        $response = Invoke-WebRequest @params

        if ($response.StatusCode -eq $ExpectedStatus) {
            Write-TestResult -Name $Name -Passed $true -Message "HTTP $($response.StatusCode)"
            return $response
        }
        else {
            Write-TestResult -Name $Name -Passed $false -Message "Expected $ExpectedStatus, got $($response.StatusCode)"
            return $null
        }
    }
    catch {
        $errorMsg = $_.Exception.Message
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            $errorMsg = "HTTP $statusCode - $errorMsg"
        }
        Write-TestResult -Name $Name -Passed $false -Message $errorMsg
        return $null
    }
}

function Write-TestResult {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Message = ""
    )

    if ($Passed) {
        $script:passedTests++
        Write-Host "  ✓ " -ForegroundColor Green -NoNewline
        Write-Host "$Name" -ForegroundColor White -NoNewline
        if ($Message) {
            Write-Host " ($Message)" -ForegroundColor Gray
        }
        else {
            Write-Host ""
        }
    }
    else {
        $script:failedTests++
        Write-Host "  ✗ " -ForegroundColor Red -NoNewline
        Write-Host "$Name" -ForegroundColor White -NoNewline
        if ($Message) {
            Write-Host " - $Message" -ForegroundColor Yellow
        }
        else {
            Write-Host ""
        }
    }

    $script:testResults += [PSCustomObject]@{
        Name = $Name
        Passed = $Passed
        Message = $Message
    }
}

function Write-Section {
    param([string]$Title)

    Write-Host "`n" -NoNewline
    Write-Host "═══ $Title ═══" -ForegroundColor Cyan
}

function Test-TcpPort {
    param(
        [string]$Host,
        [int]$Port,
        [int]$TimeoutSeconds = 5
    )

    try {
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $connection = $tcpClient.BeginConnect($Host, $Port, $null, $null)
        $wait = $connection.AsyncWaitHandle.WaitOne($TimeoutSeconds * 1000, $false)

        if ($wait) {
            $tcpClient.EndConnect($connection)
            $tcpClient.Close()
            return $true
        }
        else {
            $tcpClient.Close()
            return $false
        }
    }
    catch {
        return $false
    }
}

#endregion

#region Main Script

Write-Host "`n" -NoNewline
Write-Host "=" * 80 -ForegroundColor Magenta
Write-Host "  FILE SIMULATOR SUITE - PRODUCTION VERIFICATION" -ForegroundColor Magenta
Write-Host "=" * 80 -ForegroundColor Magenta

Write-Host "`nConfiguration:" -ForegroundColor Cyan
Write-Host "  Profile:         $Profile" -ForegroundColor White
Write-Host "  Base URL:        $BaseUrl" -ForegroundColor White
Write-Host "  Dashboard URL:   $DashboardUrl" -ForegroundColor White
Write-Host "  Dynamic Tests:   $IncludeDynamic" -ForegroundColor White

$startTime = Get-Date

#endregion

#region Task 2: Kubernetes Tests (5 tests)

Write-Section "Kubernetes Cluster"

# Test 1: Cluster status
$script:totalTests++
try {
    $status = minikube status --profile $Profile 2>$null
    if ($LASTEXITCODE -eq 0 -and $status -match "Running") {
        Write-TestResult -Name "Cluster status" -Passed $true -Message "Profile '$Profile' is running"
    }
    else {
        Write-TestResult -Name "Cluster status" -Passed $false -Message "Profile not running or not found"
    }
}
catch {
    Write-TestResult -Name "Cluster status" -Passed $false -Message $_.Exception.Message
}

# Test 2: Context accessible
$script:totalTests++
try {
    $context = kubectl config get-contexts $Profile 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-TestResult -Name "Context accessible" -Passed $true -Message "Context '$Profile' exists"
    }
    else {
        Write-TestResult -Name "Context accessible" -Passed $false -Message "Context not found"
    }
}
catch {
    Write-TestResult -Name "Context accessible" -Passed $false -Message $_.Exception.Message
}

# Test 3: Namespace exists
$script:totalTests++
try {
    $namespace = kubectl --context=$Profile get namespace file-simulator -o json 2>$null | ConvertFrom-Json
    if ($namespace.metadata.name -eq "file-simulator") {
        Write-TestResult -Name "Namespace exists" -Passed $true -Message "file-simulator namespace found"
    }
    else {
        Write-TestResult -Name "Namespace exists" -Passed $false -Message "Namespace not found"
    }
}
catch {
    Write-TestResult -Name "Namespace exists" -Passed $false -Message $_.Exception.Message
}

# Test 4: All pods running
$script:totalTests++
try {
    $pods = kubectl --context=$Profile get pods -n file-simulator -o json 2>$null | ConvertFrom-Json
    $runningPods = @($pods.items | Where-Object { $_.status.phase -eq "Running" })
    $totalPods = @($pods.items).Count

    if ($runningPods.Count -eq $totalPods -and $totalPods -gt 0) {
        Write-TestResult -Name "All pods running" -Passed $true -Message "$($runningPods.Count)/$totalPods pods running"
    }
    else {
        Write-TestResult -Name "All pods running" -Passed $false -Message "$($runningPods.Count)/$totalPods pods running"
    }
}
catch {
    Write-TestResult -Name "All pods running" -Passed $false -Message $_.Exception.Message
}

# Test 5: All services exist
$script:totalTests++
try {
    $services = kubectl --context=$Profile get services -n file-simulator -o json 2>$null | ConvertFrom-Json
    $serviceCount = @($services.items).Count

    if ($serviceCount -ge 10) {  # Expected: control-api, dashboard, 7 protocols, kafka
        Write-TestResult -Name "All services exist" -Passed $true -Message "$serviceCount services found"
    }
    else {
        Write-TestResult -Name "All services exist" -Passed $false -Message "Expected ≥10 services, found $serviceCount"
    }
}
catch {
    Write-TestResult -Name "All services exist" -Passed $false -Message $_.Exception.Message
}

#endregion

#region Task 3: Management UI Tests (6 tests)

Write-Section "Management UI (Dashboard)"

# Test 6: Dashboard health
Test-Endpoint -Name "Dashboard health" -Url "$DashboardUrl/health" | Out-Null

# Test 7: Dashboard root
Test-Endpoint -Name "Dashboard root" -Url "$DashboardUrl/" | Out-Null

# Test 8: Dashboard SPA routing
Test-Endpoint -Name "Dashboard SPA routing" -Url "$DashboardUrl/servers" | Out-Null

# Test 9: Control API health
Test-Endpoint -Name "Control API health" -Url "$BaseUrl/health" | Out-Null

# Test 10: Control API servers
$serversResponse = Test-Endpoint -Name "Control API servers" -Url "$BaseUrl/api/servers"
$script:servers = if ($serversResponse) { ($serversResponse.Content | ConvertFrom-Json) } else { @() }

# Test 11: Control API alerts
Test-Endpoint -Name "Control API alerts" -Url "$BaseUrl/api/alerts" | Out-Null

#endregion

#region Task 4: Protocol Connectivity Tests (7 tests)

Write-Section "Protocol Servers"

# Test 12: HTTP server
Test-Endpoint -Name "HTTP server" -Url "http://file-simulator.local:30088/health" | Out-Null

# Test 13: S3 API
Test-Endpoint -Name "S3 API" -Url "http://file-simulator.local:30900/minio/health/live" | Out-Null

# Test 14: FTP port
$script:totalTests++
if (Test-TcpPort -Host "file-simulator.local" -Port 30021) {
    Write-TestResult -Name "FTP port" -Passed $true -Message "Port 30021 accessible"
}
else {
    Write-TestResult -Name "FTP port" -Passed $false -Message "Cannot connect to port 30021"
}

# Test 15: SFTP port
$script:totalTests++
if (Test-TcpPort -Host "file-simulator.local" -Port 30022) {
    Write-TestResult -Name "SFTP port" -Passed $true -Message "Port 30022 accessible"
}
else {
    Write-TestResult -Name "SFTP port" -Passed $false -Message "Cannot connect to port 30022"
}

# Test 16: Kafka port
$script:totalTests++
if (Test-TcpPort -Host "file-simulator.local" -Port 30092) {
    Write-TestResult -Name "Kafka port" -Passed $true -Message "Port 30092 accessible"
}
else {
    Write-TestResult -Name "Kafka port" -Passed $false -Message "Cannot connect to port 30092"
}

# Test 17: NFS port
$script:totalTests++
if (Test-TcpPort -Host "file-simulator.local" -Port 32049) {
    Write-TestResult -Name "NFS port" -Passed $true -Message "Port 32049 accessible"
}
else {
    Write-TestResult -Name "NFS port" -Passed $false -Message "Cannot connect to port 32049"
}

# Test 18: SMB service exists
$script:totalTests++
try {
    $smbService = kubectl --context=$Profile get service -n file-simulator -l app.kubernetes.io/component=smb -o json 2>$null | ConvertFrom-Json
    if (@($smbService.items).Count -gt 0) {
        Write-TestResult -Name "SMB service exists" -Passed $true -Message "SMB service found"
    }
    else {
        Write-TestResult -Name "SMB service exists" -Passed $false -Message "SMB service not found"
    }
}
catch {
    Write-TestResult -Name "SMB service exists" -Passed $false -Message $_.Exception.Message
}

#endregion

#region Task 5: File Operations Tests (4 tests)

Write-Section "File Operations"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$testFileName = "verify-test-$timestamp.txt"
$testContent = "Production verification test - $timestamp"

# Test 19: File browse
$browseResponse = Test-Endpoint -Name "File browse" -Url "$BaseUrl/api/files/browse?path=/"
$script:canTestFiles = $null -ne $browseResponse

# Test 20: File upload
if ($script:canTestFiles) {
    $uploadBody = @{
        path = "/input/$testFileName"
        content = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($testContent))
    }

    $uploadResponse = Test-Endpoint -Name "File upload" -Url "$BaseUrl/api/files/upload" -Method "POST" -Body $uploadBody
    $script:fileUploaded = $null -ne $uploadResponse
}
else {
    $script:totalTests++
    Write-TestResult -Name "File upload" -Passed $false -Message "Skipped - browse failed"
    $script:fileUploaded = $false
}

# Test 21: File download
if ($script:fileUploaded) {
    $downloadResponse = Test-Endpoint -Name "File download" -Url "$BaseUrl/api/files/download?path=/input/$testFileName"
    $script:fileDownloaded = $null -ne $downloadResponse
}
else {
    $script:totalTests++
    Write-TestResult -Name "File download" -Passed $false -Message "Skipped - upload failed"
    $script:fileDownloaded = $false
}

# Test 22: File delete
if ($script:fileDownloaded) {
    $deleteBody = @{ path = "/input/$testFileName" }
    Test-Endpoint -Name "File delete" -Url "$BaseUrl/api/files/delete" -Method "DELETE" -Body $deleteBody | Out-Null
}
else {
    $script:totalTests++
    Write-TestResult -Name "File delete" -Passed $false -Message "Skipped - download failed"
}

#endregion

#region Task 6: Kafka Tests (4 tests)

Write-Section "Kafka Integration"

# Test 23: Kafka topics
$topicsResponse = Test-Endpoint -Name "Kafka topics" -Url "$BaseUrl/api/kafka/topics"
$script:topics = if ($topicsResponse) { ($topicsResponse.Content | ConvertFrom-Json) } else { @() }

# Test 24: Kafka produce
if ($script:topics.Count -gt 0) {
    $testTopic = $script:topics[0]
    $produceBody = @{
        topic = $testTopic
        key = "verify-test"
        value = "Production verification test - $timestamp"
    }

    Test-Endpoint -Name "Kafka produce" -Url "$BaseUrl/api/kafka/produce" -Method "POST" -Body $produceBody | Out-Null
}
else {
    $script:totalTests++
    Write-TestResult -Name "Kafka produce" -Passed $false -Message "No topics available"
}

# Test 25: Kafka consume
if ($script:topics.Count -gt 0) {
    $testTopic = $script:topics[0]
    Test-Endpoint -Name "Kafka consume" -Url "$BaseUrl/api/kafka/consume/$testTopic" -Method "POST" | Out-Null
}
else {
    $script:totalTests++
    Write-TestResult -Name "Kafka consume" -Passed $false -Message "No topics available"
}

# Test 26: Kafka consumer groups
Test-Endpoint -Name "Kafka consumer groups" -Url "$BaseUrl/api/kafka/consumer-groups" | Out-Null

#endregion

#region Task 7: Dynamic Server Tests (5 tests, optional)

if ($IncludeDynamic) {
    Write-Section "Dynamic Servers"

    $dynamicServerName = "verify-test-ftp-$timestamp"

    # Test 27: Create dynamic FTP server
    $createBody = @{
        name = $dynamicServerName
        type = "FTP"
        port = 0  # Auto-assign
        credentials = @{
            username = "testuser"
            password = "testpass123"
        }
        directory = "input"
    }

    $createResponse = Test-Endpoint -Name "Create dynamic FTP" -Url "$BaseUrl/api/servers" -Method "POST" -Body $createBody -ExpectedStatus 201
    $script:dynamicServerCreated = $null -ne $createResponse

    if ($script:dynamicServerCreated) {
        # Wait for server to be ready
        Start-Sleep -Seconds 5

        # Test 28: Verify dynamic server exists
        $verifyResponse = Test-Endpoint -Name "Verify dynamic server" -Url "$BaseUrl/api/servers"
        if ($verifyResponse) {
            $allServers = $verifyResponse.Content | ConvertFrom-Json
            $dynamicServer = $allServers | Where-Object { $_.name -eq $dynamicServerName }

            $script:totalTests++
            if ($dynamicServer) {
                Write-TestResult -Name "Dynamic server exists" -Passed $true -Message "Server found in list"
            }
            else {
                Write-TestResult -Name "Dynamic server exists" -Passed $false -Message "Server not found in list"
            }
        }
        else {
            $script:totalTests++
            Write-TestResult -Name "Dynamic server exists" -Passed $false -Message "Cannot retrieve server list"
        }

        # Test 29: Dynamic server health
        $script:totalTests++
        Start-Sleep -Seconds 3  # Additional time for health check
        $healthResponse = Test-Endpoint -Name "Dynamic server health" -Url "$BaseUrl/api/servers" -ErrorAction SilentlyContinue
        if ($healthResponse) {
            $allServers = $healthResponse.Content | ConvertFrom-Json
            $dynamicServer = $allServers | Where-Object { $_.name -eq $dynamicServerName }
            if ($dynamicServer -and $dynamicServer.status -eq "Running") {
                Write-TestResult -Name "Dynamic server status" -Passed $true -Message "Server is running"
            }
            else {
                Write-TestResult -Name "Dynamic server status" -Passed $false -Message "Server not running"
            }
        }
        else {
            Write-TestResult -Name "Dynamic server status" -Passed $false -Message "Cannot check status"
        }

        # Test 30: Stop dynamic server
        Test-Endpoint -Name "Stop dynamic server" -Url "$BaseUrl/api/servers/$dynamicServerName/stop" -Method "POST" | Out-Null

        # Test 31: Delete dynamic server
        Test-Endpoint -Name "Delete dynamic server" -Url "$BaseUrl/api/servers/$dynamicServerName" -Method "DELETE" -ExpectedStatus 204 | Out-Null
    }
    else {
        # Mark remaining tests as failed
        $script:totalTests += 4
        Write-TestResult -Name "Dynamic server exists" -Passed $false -Message "Skipped - create failed"
        Write-TestResult -Name "Dynamic server status" -Passed $false -Message "Skipped - create failed"
        Write-TestResult -Name "Stop dynamic server" -Passed $false -Message "Skipped - create failed"
        Write-TestResult -Name "Delete dynamic server" -Passed $false -Message "Skipped - create failed"
    }
}

#endregion

#region Task 8: Metrics Tests (2 tests)

Write-Section "Historical Metrics"

# Test 32: Metrics query
$endTime = [DateTimeOffset]::UtcNow
$startTime = $endTime.AddHours(-1)
$metricsUrl = "$BaseUrl/api/metrics?start=$($startTime.ToString('o'))&end=$($endTime.ToString('o'))"

$metricsResponse = Test-Endpoint -Name "Metrics query" -Url $metricsUrl
$script:hasMetrics = $null -ne $metricsResponse

# Test 33: Metrics stats
if ($script:hasMetrics) {
    Test-Endpoint -Name "Metrics stats" -Url "$BaseUrl/api/metrics/stats" | Out-Null
}
else {
    $script:totalTests++
    Write-TestResult -Name "Metrics stats" -Passed $false -Message "Skipped - metrics query failed"
}

#endregion

#region Task 9: Alert Tests (4 tests)

Write-Section "Alert System"

# Test 34: Active alerts
$activeResponse = Test-Endpoint -Name "Active alerts" -Url "$BaseUrl/api/alerts?isActive=true"
$script:hasAlerts = $null -ne $activeResponse

# Test 35: Alert history
Test-Endpoint -Name "Alert history" -Url "$BaseUrl/api/alerts?isActive=false" | Out-Null

# Test 36: Alert stats
Test-Endpoint -Name "Alert stats" -Url "$BaseUrl/api/alerts/stats" | Out-Null

# Test 37: Health checks
Test-Endpoint -Name "Health checks" -Url "$BaseUrl/health" | Out-Null

#endregion

#region Task 10: Summary Report

Write-Section "Verification Summary"

$endTime = Get-Date
$duration = $endTime - $startTime

$passRate = if ($script:totalTests -gt 0) {
    [math]::Round(($script:passedTests / $script:totalTests) * 100, 1)
}
else {
    0
}

Write-Host "`n" -NoNewline
Write-Host "Results:" -ForegroundColor Cyan
Write-Host "  Total Tests:  $script:totalTests" -ForegroundColor White

if ($script:passedTests -eq $script:totalTests) {
    Write-Host "  Passed:       $script:passedTests" -ForegroundColor Green
}
else {
    Write-Host "  Passed:       $script:passedTests" -ForegroundColor Yellow
}

if ($script:failedTests -eq 0) {
    Write-Host "  Failed:       $script:failedTests" -ForegroundColor Green
}
else {
    Write-Host "  Failed:       $script:failedTests" -ForegroundColor Red
}

Write-Host "  Pass Rate:    $passRate%" -ForegroundColor $(if ($passRate -eq 100) { "Green" } elseif ($passRate -ge 80) { "Yellow" } else { "Red" })
Write-Host "  Duration:     $([math]::Round($duration.TotalSeconds, 1))s" -ForegroundColor White

# List failed tests if any
if ($script:failedTests -gt 0) {
    Write-Host "`nFailed Tests:" -ForegroundColor Red
    $script:testResults | Where-Object { -not $_.Passed } | ForEach-Object {
        Write-Host "  • $($_.Name)" -ForegroundColor Red
        if ($_.Message) {
            Write-Host "    $($_.Message)" -ForegroundColor Yellow
        }
    }
}

Write-Host "`n" -NoNewline
if ($script:failedTests -eq 0) {
    Write-Host "=" * 80 -ForegroundColor Green
    Write-Host "  ALL TESTS PASSED - PRODUCTION READY" -ForegroundColor Green
    Write-Host "=" * 80 -ForegroundColor Green
}
else {
    Write-Host "=" * 80 -ForegroundColor Red
    Write-Host "  VERIFICATION FAILED - SEE ERRORS ABOVE" -ForegroundColor Red
    Write-Host "=" * 80 -ForegroundColor Red
}

Write-Host "`n" -NoNewline

# Exit with appropriate code
exit $(if ($script:failedTests -eq 0) { 0 } else { 1 })

#endregion
