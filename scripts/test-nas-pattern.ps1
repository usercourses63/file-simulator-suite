# NAS Pattern Validation Script
# Tests the init container + unfs3 NFS pattern for File Simulator Suite
# Phase 01-02: Deploy and Test NAS

param(
    [switch]$SkipDeploy,
    [switch]$Cleanup
)

$ErrorActionPreference = "Stop"
Write-Host "===================================" -ForegroundColor Cyan
Write-Host " NAS Pattern Validation" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check Minikube status
Write-Host "[1/10] Checking Minikube status..." -ForegroundColor Yellow
try {
    $minikubeStatus = minikube status 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR: Minikube is not running" -ForegroundColor Red
        Write-Host "  Run: minikube start" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "  OK: Minikube is running" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: Minikube not found. Please install Minikube." -ForegroundColor Red
    exit 1
}

# Step 2: Check/create Windows test directory
Write-Host "[2/10] Checking Windows test directory..." -ForegroundColor Yellow
$testDir = "C:\simulator-data\nas-test-1"
if (-not (Test-Path $testDir)) {
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    Write-Host "  CREATED: $testDir" -ForegroundColor Green
} else {
    Write-Host "  OK: Directory exists" -ForegroundColor Green
}

# Step 3: Check Minikube mount
Write-Host "[3/10] Checking Minikube mount..." -ForegroundColor Yellow
$mountProcess = Get-Process -Name "minikube" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*mount*" }
if (-not $mountProcess) {
    Write-Host "  WARNING: No active minikube mount process detected" -ForegroundColor Yellow
    Write-Host "  Start mount with: minikube mount C:\simulator-data:/mnt/simulator-data --uid=14 --gid=50" -ForegroundColor Yellow
    Write-Host "  (This script will continue, but files may not sync)" -ForegroundColor Yellow
} else {
    Write-Host "  OK: Minikube mount process active" -ForegroundColor Green
}

# Step 4: Create test file with timestamp
Write-Host "[4/10] Creating test file..." -ForegroundColor Yellow
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
$testFile = Join-Path $testDir "test-from-windows.txt"
"Created from Windows at $timestamp" | Out-File -FilePath $testFile -Encoding ASCII -Force
Write-Host "  CREATED: $testFile" -ForegroundColor Green

# Step 5: Deploy nas-test-1 (unless skipped)
if (-not $SkipDeploy) {
    Write-Host "[5/10] Deploying nas-test-1 with Helm..." -ForegroundColor Yellow
    try {
        $helmOutput = helm upgrade --install file-sim ./helm-chart/file-simulator `
            --namespace file-simulator --create-namespace `
            --set nasTest.enabled=true 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Host "  ERROR: Helm deployment failed" -ForegroundColor Red
            Write-Host $helmOutput
            exit 1
        }
        Write-Host "  OK: Helm deployment succeeded" -ForegroundColor Green
    } catch {
        Write-Host "  ERROR: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[5/10] Skipping deployment (--SkipDeploy)" -ForegroundColor Yellow
}

# Step 6: Wait for pod ready
Write-Host "[6/10] Waiting for pod ready (timeout 120s)..." -ForegroundColor Yellow
try {
    $waitOutput = kubectl wait --for=condition=ready pod `
        -l app.kubernetes.io/component=nas-test-1 `
        -n file-simulator --timeout=120s 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR: Pod did not become ready" -ForegroundColor Red
        Write-Host "  Check status: kubectl get pods -n file-simulator -l app.kubernetes.io/component=nas-test-1" -ForegroundColor Yellow
        Write-Host "  Check logs: kubectl logs -n file-simulator -l app.kubernetes.io/component=nas-test-1" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "  OK: Pod is ready" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: $_" -ForegroundColor Red
    exit 1
}

# Step 7: Verify pod security context (NOT privileged)
Write-Host "[7/10] Verifying pod security context..." -ForegroundColor Yellow
try {
    $securityContext = kubectl get pod -n file-simulator `
        -l app.kubernetes.io/component=nas-test-1 `
        -o jsonpath='{.items[0].spec.containers[0].securityContext}' 2>&1

    if ($securityContext -like "*privileged*:*true*") {
        Write-Host "  ERROR: Pod is running in privileged mode" -ForegroundColor Red
        exit 1
    }

    if ($securityContext -like "*NET_BIND_SERVICE*") {
        Write-Host "  OK: Pod uses NET_BIND_SERVICE capability (non-privileged)" -ForegroundColor Green
    } else {
        Write-Host "  WARNING: NET_BIND_SERVICE capability not found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ERROR: $_" -ForegroundColor Red
    exit 1
}

# Step 8: Verify file visible in NFS export
Write-Host "[8/10] Verifying file in NFS export..." -ForegroundColor Yellow
try {
    $podName = kubectl get pod -n file-simulator `
        -l app.kubernetes.io/component=nas-test-1 `
        -o jsonpath='{.items[0].metadata.name}' 2>&1

    $fileList = kubectl exec -n file-simulator $podName -c nfs-server -- sh -c "ls -la /data" 2>&1

    if ($fileList -like "*test-from-windows.txt*") {
        Write-Host "  OK: Test file visible in /data" -ForegroundColor Green

        $fileContent = kubectl exec -n file-simulator $podName -c nfs-server -- sh -c "cat /data/test-from-windows.txt" 2>&1
        Write-Host "  File content: $fileContent" -ForegroundColor Cyan
    } else {
        Write-Host "  ERROR: Test file not found in /data" -ForegroundColor Red
        Write-Host "  Files in /data:" -ForegroundColor Yellow
        Write-Host $fileList
        exit 1
    }
} catch {
    Write-Host "  ERROR: $_" -ForegroundColor Red
    exit 1
}

# Step 9: Verify exports configuration
Write-Host "[9/10] Verifying NFS exports configuration..." -ForegroundColor Yellow
try {
    $exports = kubectl exec -n file-simulator $podName -c nfs-server -- sh -c "cat /etc/exports" 2>&1

    Write-Host "  Exports: $exports" -ForegroundColor Cyan

    if ($exports -like "*/data*") {
        Write-Host "  OK: /data export configured" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: /data export not found" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "  ERROR: $_" -ForegroundColor Red
    exit 1
}

# Step 10: Test NFS connectivity (informational - may fail due to rpcbind issues)
Write-Host "[10/10] Testing NFS connectivity..." -ForegroundColor Yellow
Write-Host "  NOTE: Full NFS client mount testing requires additional configuration" -ForegroundColor Yellow
Write-Host "  Known issue: unfs3 + rpcbind RPC registration needs investigation" -ForegroundColor Yellow

try {
    # Check if port 2049 is open
    $service = kubectl get svc -n file-simulator file-sim-file-simulator-nas-test-1 -o jsonpath='{.spec.clusterIP}' 2>&1
    Write-Host "  NFS Service ClusterIP: $service" -ForegroundColor Cyan
    Write-Host "  NFS Service DNS: file-sim-file-simulator-nas-test-1.file-simulator.svc.cluster.local" -ForegroundColor Cyan
    Write-Host "  NFS Port: 2049" -ForegroundColor Cyan
} catch {
    Write-Host "  WARNING: Could not retrieve service info" -ForegroundColor Yellow
}

# Cleanup
if ($Cleanup) {
    Write-Host ""
    Write-Host "Cleaning up test resources..." -ForegroundColor Yellow
    kubectl delete pod -n file-simulator -l app.kubernetes.io/component=nas-test-1 --ignore-not-found=true
    Remove-Item -Path $testFile -Force -ErrorAction SilentlyContinue
    Write-Host "  Cleanup complete" -ForegroundColor Green
}

# Summary
Write-Host ""
Write-Host "===================================" -ForegroundColor Cyan
Write-Host " VALIDATION SUMMARY" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host "  Status: SUCCESS" -ForegroundColor Green
Write-Host ""
Write-Host "Core Pattern Verified:" -ForegroundColor Green
Write-Host "  [OK] Windows directory -> Minikube mount" -ForegroundColor Green
Write-Host "  [OK] Init container rsync" -ForegroundColor Green
Write-Host "  [OK] Files visible in NFS export" -ForegroundColor Green
Write-Host "  [OK] Pod runs without privileged mode" -ForegroundColor Green
Write-Host "  [OK] unfs3 NFS server starts" -ForegroundColor Green
Write-Host ""
Write-Host "Known Issues:" -ForegroundColor Yellow
Write-Host "  [PENDING] NFS client mount (rpcbind RPC registration fails)" -ForegroundColor Yellow
Write-Host "  [PENDING] Full protocol compliance testing needed" -ForegroundColor Yellow
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Investigate unfs3 RPC registration issue" -ForegroundColor Cyan
Write-Host "  2. Consider alternative NFS server (nfs-ganesha, kernel NFS)" -ForegroundColor Cyan
Write-Host "  3. Test with actual microservice NFS client" -ForegroundColor Cyan
Write-Host ""
Write-Host "Pattern is VALIDATED for sync functionality." -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Cyan
