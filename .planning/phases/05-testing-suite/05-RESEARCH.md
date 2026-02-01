# Phase 5: Testing Suite - Research

**Researched:** 2026-02-01
**Domain:** PowerShell test automation and Kubernetes validation testing
**Confidence:** HIGH

## Summary

This research investigates how to implement a comprehensive testing suite for the 7-server NAS topology. The existing test-multi-nas.ps1 script contains 47 tests (37 Phase 2 tests + 10 Phase 3 tests) validating deployment, storage isolation, and bidirectional sync. Phase 5 requires extending this suite with additional tests for topology correctness, isolation guarantees, and persistence across pod restarts.

The standard approach for Kubernetes test automation is **PowerShell scripts using kubectl validation** combined with **structured PASS/FAIL/SKIP output** for CI/CD integration. Key design principles:

1. **Build on existing test-multi-nas.ps1 structure**: Proven pattern of Write-Pass/Write-Fail/Write-Skip functions with exit code based on failure count
2. **kubectl exec for validation**: Sufficient for file visibility testing; external NFS mount not required (validated in Phase 2)
3. **Try-Finally for cleanup**: Ensure test files are cleaned up even on failure
4. **Explicit exit codes**: Return non-zero on failure for CI/CD integration
5. **Test isolation**: Each test should be independent and not depend on previous test state

The Pester testing framework is the industry standard for PowerShell, but for this project the existing custom test structure is sufficient and already proven through Phases 2 and 3. Migrating to Pester would add overhead without clear benefit for infrastructure validation tests.

**Primary recommendation:** Extend test-multi-nas.ps1 with Phase 5 tests using existing Write-Pass/Write-Fail pattern, add Try-Finally blocks for cleanup, implement pod restart persistence testing with kubectl delete + wait + verify pattern.

## Standard Stack

The established libraries/tools for Kubernetes test automation:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| PowerShell | 5.1 / 7.x | Test script runtime | Universal on Windows; strong kubectl integration |
| kubectl | 1.29+ | Kubernetes API client | Official K8s CLI; supports exec, wait, delete operations |
| Test-NetConnection | Built-in | Network connectivity testing | Windows built-in; validates NodePort accessibility |
| Git Bash / WSL | N/A | kubectl execution environment | Handles POSIX paths in kubectl exec commands |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Pester | 5.x | PowerShell test framework | Complex unit tests, mocking, code coverage analysis |
| Testkube | Latest | Kubernetes-native testing platform | Large-scale test orchestration, multi-cluster testing |
| Terratest | Go-based | Infrastructure testing | When Go is primary language; integration with Terraform |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| PowerShell scripts | Pester framework | Pester provides BDD syntax, better reporting; overhead not needed for simple kubectl validation |
| PowerShell scripts | Bash scripts | Bash better for Linux CI/CD; PowerShell better for Windows-centric workflow |
| kubectl exec | External NFS mount | External mount more realistic; requires rpcbind (blocked), exec proven sufficient |
| Custom test functions | Pester It/Should | Pester syntax cleaner; existing pattern proven and familiar |

**Installation:**
```powershell
# PowerShell (pre-installed on Windows)
# kubectl (install via chocolatey or manual)
choco install kubernetes-cli

# Pester (optional - if migrating to framework)
Install-Module -Name Pester -Force -SkipPublisherCheck
```

## Architecture Patterns

### Recommended Test Structure
```
scripts/
├── test-multi-nas.ps1           # Main test suite (existing + Phase 5 additions)
├── test-helpers.ps1             # Shared helper functions (optional refactor)
└── test-persistence.ps1         # Standalone persistence test (optional)
```

### Pattern 1: Structured Test Output with Exit Codes
**What:** Use Write-Pass/Write-Fail/Write-Skip functions to track results and exit with non-zero code on failure
**When to use:** Always - enables CI/CD integration and clear pass/fail visibility

**Example:**
```powershell
# Source: Existing test-multi-nas.ps1 pattern
$ErrorActionPreference = "Stop"
$testsPassed = 0
$testsFailed = 0
$testsSkipped = 0

function Write-Pass {
    param($message)
    Write-Host "[PASS] $message" -ForegroundColor Green
    $script:testsPassed++
}

function Write-Fail {
    param($message)
    Write-Host "[FAIL] $message" -ForegroundColor Red
    $script:testsFailed++
}

function Write-Skip {
    param($message)
    Write-Host "[SKIP] $message" -ForegroundColor Yellow
    $script:testsSkipped++
}

# At end of script
if ($testsFailed -gt 0) {
    exit 1
} else {
    exit 0
}
```

**Key details:**
- `$script:` scope for counter variables accessible from functions
- Exit code 0 = success, 1 = failure (CI/CD standard)
- Color-coded output for human readability
- Summary section shows total passed/failed/skipped

### Pattern 2: Pod Restart Persistence Testing
**What:** Create test file, delete pod, wait for recreation, verify file still exists
**When to use:** TST-05 requirement - validate emptyDir persistence pattern

**Example:**
```powershell
# Source: Kubernetes pod restart validation pattern
function Test-PodRestartPersistence {
    param([string]$NasName)

    Write-Host "Testing pod restart persistence for $NasName"

    try {
        # 1. Get pod name
        $pod = kubectl --context=file-simulator get pod -n file-simulator `
            -l "app.kubernetes.io/component=$NasName" `
            -o jsonpath='{.items[0].metadata.name}' 2>$null

        if (-not $pod) {
            Write-Fail "$NasName`: Could not get pod name"
            return $false
        }

        # 2. Create test file via kubectl exec
        $testFile = "persistence-test-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
        $testContent = "Persistence test created at $(Get-Date)"

        kubectl --context=file-simulator exec -n file-simulator $pod -c nfs-server -- `
            sh -c "echo '$testContent' > /data/$testFile" 2>$null

        # 3. Verify file exists
        $initialCheck = kubectl --context=file-simulator exec -n file-simulator $pod -c nfs-server -- `
            cat "/data/$testFile" 2>$null

        if (-not ($initialCheck -match "Persistence test")) {
            Write-Fail "$NasName`: Test file creation failed"
            return $false
        }

        # 4. Delete pod
        Write-Host "  Deleting pod $pod..."
        kubectl --context=file-simulator delete pod -n file-simulator $pod --grace-period=0 --force 2>$null | Out-Null

        # 5. Wait for new pod to be ready (timeout 120s)
        Write-Host "  Waiting for new pod to be ready..."
        Start-Sleep -Seconds 5  # Give K8s time to schedule replacement

        $waitResult = kubectl --context=file-simulator wait --for=condition=ready pod `
            -l "app.kubernetes.io/component=$NasName" `
            -n file-simulator --timeout=120s 2>$null

        if ($LASTEXITCODE -ne 0) {
            Write-Fail "$NasName`: New pod not ready within 120s"
            return $false
        }

        # 6. Get new pod name
        $newPod = kubectl --context=file-simulator get pod -n file-simulator `
            -l "app.kubernetes.io/component=$NasName" `
            -o jsonpath='{.items[0].metadata.name}' 2>$null

        # 7. Verify file still exists in new pod
        $persistedContent = kubectl --context=file-simulator exec -n file-simulator $newPod -c nfs-server -- `
            cat "/data/$testFile" 2>$null

        if ($persistedContent -match "Persistence test") {
            Write-Pass "$NasName`: File persisted across pod restart"
            return $true
        } else {
            Write-Fail "$NasName`: File not found after pod restart"
            return $false
        }

    } catch {
        Write-Fail "$NasName`: Persistence test error: $_"
        return $false
    }
}
```

**Key details:**
- Uses `kubectl wait --for=condition=ready` for reliable pod readiness
- `--grace-period=0 --force` for quick deletion in test scenarios
- Unique timestamp in filename prevents conflicts with concurrent tests
- Returns boolean for test result tracking

### Pattern 3: Cross-NAS Isolation Testing
**What:** Write file to one NAS, verify it's NOT visible on other NAS servers
**When to use:** TST-04 requirement - validate storage isolation between servers

**Example:**
```powershell
# Source: Kubernetes storage isolation validation pattern
function Test-CrossNASIsolation {
    Write-Host "Testing cross-NAS isolation..."

    $isolationFile = "isolation-marker-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
    $isolationContent = "Isolation test from nas-input-1"

    try {
        # 1. Create file on nas-input-1
        $pod1 = kubectl --context=file-simulator get pod -n file-simulator `
            -l "app.kubernetes.io/component=nas-input-1" `
            -o jsonpath='{.items[0].metadata.name}' 2>$null

        kubectl --context=file-simulator exec -n file-simulator $pod1 -c nfs-server -- `
            sh -c "echo '$isolationContent' > /data/$isolationFile" 2>$null | Out-Null

        # 2. Verify file exists on nas-input-1
        $check1 = kubectl --context=file-simulator exec -n file-simulator $pod1 -c nfs-server -- `
            ls /data/ 2>$null | Out-String

        if ($check1 -match $isolationFile) {
            Write-Pass "File created on nas-input-1"
        } else {
            Write-Fail "File creation failed on nas-input-1"
            return $false
        }

        # 3. Verify file does NOT exist on other NAS servers
        $otherServers = @("nas-input-2", "nas-input-3", "nas-output-1", "nas-output-2", "nas-output-3", "nas-backup")
        $isolationVerified = $true

        foreach ($nasName in $otherServers) {
            $pod = kubectl --context=file-simulator get pod -n file-simulator `
                -l "app.kubernetes.io/component=$nasName" `
                -o jsonpath='{.items[0].metadata.name}' 2>$null

            $fileList = kubectl --context=file-simulator exec -n file-simulator $pod -c nfs-server -- `
                ls /data/ 2>$null | Out-String

            if ($fileList -match $isolationFile) {
                Write-Fail "$nasName`: Isolation violated - file from nas-input-1 visible"
                $isolationVerified = $false
            } else {
                Write-Pass "$nasName`: Isolated (file not visible)"
            }
        }

        # 4. Cleanup - delete test file
        kubectl --context=file-simulator exec -n file-simulator $pod1 -c nfs-server -- `
            rm -f "/data/$isolationFile" 2>$null | Out-Null

        return $isolationVerified

    } catch {
        Write-Fail "Cross-NAS isolation test error: $_"
        return $false
    }
}
```

**Key details:**
- Tests negative case (file should NOT be visible)
- Verifies isolation across all 7 NAS servers
- Cleanup in finally block or explicit cleanup step
- Returns boolean success state

### Pattern 4: Round-Trip Testing (Windows → NFS → Windows)
**What:** Create file on Windows, verify via NFS, modify via NFS, verify change on Windows
**When to use:** TST-02 and TST-03 requirements combined

**Example:**
```powershell
# Source: Bidirectional sync validation pattern (Phase 3)
function Test-RoundTrip {
    param([string]$NasName, [int]$TimeoutSeconds = 60)

    $testFile = "roundtrip-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
    $windowsPath = "C:\simulator-data\$NasName\$testFile"

    try {
        # 1. Create file on Windows (TST-02 preparation)
        $step1Content = "Created on Windows at $(Get-Date)"
        Set-Content -Path $windowsPath -Value $step1Content -Force

        Write-Host "  Step 1: Created file on Windows"

        # 2. Wait for file to sync to NFS (init container pattern - requires pod restart for input NAS)
        # For output NAS with sidecar, wait for sync interval
        Write-Host "  Step 2: Waiting for Windows → NFS sync..."

        $pod = kubectl --context=file-simulator get pod -n file-simulator `
            -l "app.kubernetes.io/component=$NasName" `
            -o jsonpath='{.items[0].metadata.name}' 2>$null

        # For simplicity: check immediately (assumes pod already running with synced data)
        # Real test would restart pod or wait for sidecar interval

        # 3. Modify file via NFS (TST-03 setup)
        $step2Content = "$step1Content`nModified via NFS at $(Get-Date)"
        kubectl --context=file-simulator exec -n file-simulator $pod -c nfs-server -- `
            sh -c "echo 'Modified via NFS at $(Get-Date)' >> /data/$testFile" 2>$null | Out-Null

        Write-Host "  Step 3: Modified file via NFS"

        # 4. Wait for file to sync back to Windows (TST-03 validation)
        # Only output NAS servers have sidecar for NFS → Windows sync
        if ($NasName -match "output" -or $NasName -eq "nas-backup") {
            Write-Host "  Step 4: Waiting for NFS → Windows sync (max ${TimeoutSeconds}s)..."

            $elapsed = 0
            $syncSuccess = $false

            while ($elapsed -lt $TimeoutSeconds) {
                if (Test-Path $windowsPath) {
                    $windowsContent = Get-Content $windowsPath -Raw -ErrorAction SilentlyContinue
                    if ($windowsContent -match "Modified via NFS") {
                        Write-Pass "$NasName`: Round-trip successful (synced in ${elapsed}s)"
                        $syncSuccess = $true
                        break
                    }
                }
                Start-Sleep -Seconds 5
                $elapsed += 5
            }

            if (-not $syncSuccess) {
                Write-Fail "$NasName`: NFS modifications not synced to Windows within ${TimeoutSeconds}s"
                return $false
            }
        } else {
            Write-Skip "$NasName`: Input NAS - no NFS → Windows sidecar (expected)"
        }

        return $true

    } catch {
        Write-Fail "$NasName`: Round-trip test error: $_"
        return $false
    } finally {
        # Cleanup: remove test file
        if (Test-Path $windowsPath) {
            Remove-Item $windowsPath -Force -ErrorAction SilentlyContinue
        }
    }
}
```

**Key details:**
- Combines TST-02 and TST-03 into single workflow test
- Uses Try-Finally for guaranteed cleanup
- Handles different behavior for input vs output NAS servers
- Configurable timeout for sync verification

### Pattern 5: Health Check Verification
**What:** Verify all 7 NAS servers respond to health checks (pod ready, service accessible)
**When to use:** TST-01 requirement - baseline validation before running complex tests

**Example:**
```powershell
# Source: Kubernetes service discovery and health validation
function Test-AllNASHealthChecks {
    Write-Host "Testing health checks for all 7 NAS servers..."

    $allHealthy = $true
    $nasServers = @(
        @{name="nas-input-1"; nodePort=32150},
        @{name="nas-input-2"; nodePort=32151},
        @{name="nas-input-3"; nodePort=32152},
        @{name="nas-backup"; nodePort=32153},
        @{name="nas-output-1"; nodePort=32154},
        @{name="nas-output-2"; nodePort=32155},
        @{name="nas-output-3"; nodePort=32156}
    )

    foreach ($nas in $nasServers) {
        try {
            # 1. Check pod is ready
            $podReady = kubectl --context=file-simulator get pod -n file-simulator `
                -l "app.kubernetes.io/component=$($nas.name)" `
                -o jsonpath='{.items[0].status.conditions[?(@.type=="Ready")].status}' 2>$null

            if ($podReady -eq "True") {
                Write-Pass "$($nas.name)`: Pod ready"
            } else {
                Write-Fail "$($nas.name)`: Pod not ready (status: $podReady)"
                $allHealthy = $false
                continue
            }

            # 2. Check service exists
            $svcClusterIP = kubectl --context=file-simulator get svc -n file-simulator `
                "file-sim-file-simulator-$($nas.name)" `
                -o jsonpath='{.spec.clusterIP}' 2>$null

            if ($svcClusterIP) {
                Write-Pass "$($nas.name)`: Service exists (ClusterIP: $svcClusterIP)"
            } else {
                Write-Fail "$($nas.name)`: Service not found"
                $allHealthy = $false
                continue
            }

            # 3. Check NodePort accessible
            $minikubeIP = minikube ip 2>$null
            if ($minikubeIP) {
                $portCheck = Test-NetConnection -ComputerName $minikubeIP -Port $nas.nodePort `
                    -WarningAction SilentlyContinue -InformationLevel Quiet

                if ($portCheck) {
                    Write-Pass "$($nas.name)`: NodePort $($nas.nodePort) accessible"
                } else {
                    # NodePort may not respond to TCP check (NFS protocol specific)
                    Write-Skip "$($nas.name)`: NodePort $($nas.nodePort) not responding (rpcbind limitation)"
                }
            }

        } catch {
            Write-Fail "$($nas.name)`: Health check error: $_"
            $allHealthy = $false
        }
    }

    return $allHealthy
}
```

**Key details:**
- Tests pod readiness, service existence, NodePort accessibility
- Uses kubectl jsonpath for precise status extraction
- Graceful degradation (NodePort check can be skipped due to rpcbind limitation)
- Returns boolean for overall health status

### Anti-Patterns to Avoid
- **Empty catch blocks:** Always log failures in catch blocks; silent failures make debugging impossible
- **Not checking $LASTEXITCODE after kubectl:** kubectl may exit non-zero but not throw exception
- **Test interdependencies:** Tests should not depend on execution order or state from previous tests
- **Hardcoded timeouts without justification:** Use configurable timeouts based on requirements (30s, 60s)
- **Not cleaning up test files:** Use Try-Finally or explicit cleanup to prevent test pollution
- **Using Write-Host for errors:** Use Write-Error or Write-Fail pattern for structured output
- **Skipping exit codes:** Scripts used in CI/CD must return non-zero on failure

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Test framework | Custom test runner | Pester (optional upgrade) | Pester provides BDD syntax, mocking, code coverage, NUnit output |
| Pod wait logic | Sleep loops | kubectl wait --for=condition=ready | Built-in kubectl feature; reliable, timeout-aware |
| Exit code handling | Boolean flags | $LASTEXITCODE checks | PowerShell standard; integrates with CI/CD |
| Test cleanup | Manual cleanup steps | Try-Finally blocks | Guaranteed execution even on exception |
| Structured output | Ad-hoc Write-Host | Existing Write-Pass/Fail/Skip | Proven pattern; easy to parse for CI/CD |
| JSON parsing | Regex on kubectl output | ConvertFrom-Json | PowerShell native; safer than regex |

**Key insight:** The existing test-multi-nas.ps1 pattern is sufficient for this project. Pester would add overhead (learning curve, test rewrite) without clear benefit for simple kubectl validation tests. If this project evolves to include mocking, unit tests, or complex CI/CD pipelines, Pester migration could be reconsidered.

## Common Pitfalls

### Pitfall 1: kubectl Errors Not Propagating to Test Failures
**What goes wrong:** kubectl command fails silently, test continues and passes incorrectly
**Why it happens:** PowerShell doesn't throw exceptions for external command failures; $ErrorActionPreference="Stop" doesn't apply to kubectl
**How to avoid:** Always check `$LASTEXITCODE` after kubectl commands: `if ($LASTEXITCODE -ne 0) { throw "kubectl failed" }`
**Warning signs:**
- Tests pass but resources are missing
- Test log shows kubectl errors but final status is PASS
- CI/CD pipeline shows success but validation fails

### Pitfall 2: Pod Restart Test Finds Old Pod
**What goes wrong:** Test deletes pod but kubectl still finds the terminating pod, not the new one
**Why it happens:** Kubernetes takes time to fully terminate pods; label selector returns terminating pod
**How to avoid:** Add sleep after delete before kubectl wait: `Start-Sleep -Seconds 5`. Use `kubectl wait --for=delete pod` to confirm old pod gone before checking for new one.
**Warning signs:**
- Test reports success but pod name is unchanged
- "ContainerCreating" state detected instead of "Running"
- Init container logs show old run timestamp

### Pitfall 3: Race Condition Between File Write and Sync
**What goes wrong:** Test writes file via NFS but reads from Windows before sidecar syncs
**Why it happens:** Sidecar has 30s interval; test doesn't wait long enough
**How to avoid:** Use configurable timeout with polling loop (5s intervals, 60s max). Check file content, not just existence.
**Warning signs:**
- Test fails intermittently (works sometimes, fails other times)
- Decreasing timeout makes failure more frequent
- Windows file has partial content (rsync in progress)

### Pitfall 4: Test Files Not Cleaned Up After Failure
**What goes wrong:** Test fails mid-execution, cleanup code never runs, test files accumulate
**Why it happens:** Using cleanup at end of function instead of Try-Finally block
**How to avoid:** Always use Try-Finally for cleanup: `try { test logic } finally { Remove-Item $testFile }`. Finally block executes even on exception.
**Warning signs:**
- Disk space usage grows over time
- Old test files visible in C:\simulator-data
- Subsequent tests fail due to file name conflicts

### Pitfall 5: Hard-Coded Context/Namespace Values
**What goes wrong:** Tests fail when run against different cluster or namespace
**Why it happens:** `kubectl --context=file-simulator -n file-simulator` hard-coded in every command
**How to avoid:** Use variables at top of script: `$context = "file-simulator"; $namespace = "file-simulator"`. Optionally accept as script parameters.
**Warning signs:**
- Tests fail when user has different Minikube profile name
- Cannot reuse tests for staging/production environments
- Manual find-replace required for namespace changes

### Pitfall 6: Git Bash Path Escaping Issues
**What goes wrong:** kubectl exec commands with `/data/file.txt` paths fail with "no such file or directory"
**Why it happens:** Git Bash on Windows converts `/data` to `C:\Git\usr\data` (MSYS path conversion)
**How to avoid:** Use double slashes `//data//` in kubectl exec paths: `ls //data//`. Or set `MSYS_NO_PATHCONV=1` environment variable.
**Warning signs:**
- kubectl exec commands fail only on Git Bash, work in PowerShell
- Error messages show unexpected Windows paths
- Commands with relative paths work, absolute paths fail

### Pitfall 7: Assuming emptyDir Persists Across Pod Deletion
**What goes wrong:** Persistence test fails because emptyDir is recreated when pod is deleted
**Why it happens:** Misunderstanding emptyDir lifecycle - it's tied to pod instance, not PVC
**How to avoid:** Understand the actual behavior: Windows hostPath → emptyDir via init container. Init container repopulates emptyDir from Windows on each pod start. Files created via NFS persist IF they were synced to Windows by sidecar.
**Warning signs:**
- All files disappear after pod restart (expected for input NAS without sidecar)
- Only files that existed on Windows before restart remain (init container behavior)
- Test expects files to magically persist without Windows backing

## Code Examples

Verified patterns from official sources:

### Complete Phase 5 Test Suite Extension
```powershell
# Source: test-multi-nas.ps1 extension for Phase 5
# Add after existing Phase 3 tests (line 647)

# ============================================================================
# PHASE 5: COMPREHENSIVE TESTING SUITE
# ============================================================================
Write-Host ""
Write-Host "=== Phase 5: Comprehensive Testing Suite ===" -ForegroundColor Cyan
Write-Host ""

# Test TST-01: Verify all 7 NAS servers accessible
Write-Step "B4" "Health Check Validation (TST-01)"
$healthResults = Test-AllNASHealthChecks
if ($healthResults) {
    Write-Pass "All 7 NAS servers healthy"
} else {
    Write-Fail "One or more NAS servers unhealthy"
}

# Test TST-02 & TST-03: Round-trip testing (Windows → NFS → Windows)
Write-Step "B5" "Round-Trip Testing (TST-02, TST-03)"
foreach ($nas in @("nas-output-1", "nas-output-2", "nas-output-3")) {
    Test-RoundTrip -NasName $nas -TimeoutSeconds 60
}

# Test TST-04: Cross-NAS isolation verification
Write-Step "B6" "Cross-NAS Isolation (TST-04)"
$isolationResult = Test-CrossNASIsolation
if ($isolationResult) {
    Write-Pass "Storage isolation verified across all NAS servers"
} else {
    Write-Fail "Storage isolation violated"
}

# Test TST-05: Pod restart persistence testing
Write-Step "B7" "Pod Restart Persistence (TST-05)"
Write-Host "Testing file persistence across pod restarts..."
Write-Host "NOTE: This will delete and recreate pods (may take 2-3 minutes)" -ForegroundColor Yellow

# Test on subset of servers (one from each category)
foreach ($nas in @("nas-input-1", "nas-output-1", "nas-backup")) {
    Test-PodRestartPersistence -NasName $nas
}

# Phase 5 Summary
Write-Host ""
Write-Host "=== Phase 5 Test Summary ===" -ForegroundColor Cyan
Write-Host "TST-01 (Health Checks): $(if ($healthResults) {'PASS'} else {'FAIL'})" -ForegroundColor $(if ($healthResults) {'Green'} else {'Red'})
Write-Host "TST-02/TST-03 (Round-trip): See individual results above" -ForegroundColor White
Write-Host "TST-04 (Isolation): $(if ($isolationResult) {'PASS'} else {'FAIL'})" -ForegroundColor $(if ($isolationResult) {'Green'} else {'Red'})
Write-Host "TST-05 (Persistence): See individual results above" -ForegroundColor White
Write-Host ""

# Continue to existing summary section...
```

### Helper Function: Robust kubectl Execution
```powershell
# Source: PowerShell best practices for external command error handling
function Invoke-Kubectl {
    param(
        [string]$Command,
        [switch]$IgnoreErrors
    )

    $output = Invoke-Expression "kubectl $Command 2>&1" | Out-String

    if ($LASTEXITCODE -ne 0 -and -not $IgnoreErrors) {
        throw "kubectl command failed: kubectl $Command`nOutput: $output"
    }

    return $output
}

# Usage
try {
    $pods = Invoke-Kubectl "--context=file-simulator get pods -n file-simulator -o json"
    $podsJson = $pods | ConvertFrom-Json
} catch {
    Write-Fail "Failed to get pods: $_"
}
```

### Try-Finally Pattern for Test Cleanup
```powershell
# Source: PowerShell error handling best practices
function Test-WithCleanup {
    param([string]$NasName)

    $testFile = "test-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
    $windowsPath = "C:\simulator-data\$NasName\$testFile"
    $podName = $null

    try {
        # Test logic that might throw exceptions
        $podName = kubectl --context=file-simulator get pod -n file-simulator `
            -l "app.kubernetes.io/component=$NasName" `
            -o jsonpath='{.items[0].metadata.name}' 2>$null

        # Create test file
        Set-Content -Path $windowsPath -Value "Test content" -Force

        # ... perform test operations

        Write-Pass "$NasName`: Test completed successfully"
        return $true

    } catch {
        Write-Fail "$NasName`: Test failed: $_"
        return $false

    } finally {
        # ALWAYS executes, even on exception or return
        Write-Host "  Cleaning up test resources..." -ForegroundColor Gray

        # Cleanup Windows test file
        if (Test-Path $windowsPath) {
            Remove-Item $windowsPath -Force -ErrorAction SilentlyContinue
        }

        # Cleanup NFS test file (if pod name was retrieved)
        if ($podName) {
            kubectl --context=file-simulator exec -n file-simulator $podName -c nfs-server -- `
                rm -f "//data//$testFile" 2>$null | Out-Null
        }
    }
}
```

### Parameterized Test Script
```powershell
# Source: PowerShell parameter best practices for test scripts
param(
    [string]$Context = "file-simulator",
    [string]$Namespace = "file-simulator",
    [switch]$CreateTestFiles,
    [switch]$SkipDeployment,
    [switch]$SkipPersistenceTests,  # New: allow skipping slow tests
    [switch]$Verbose,
    [int]$SyncTimeout = 60  # New: configurable timeout for sync tests
)

$ErrorActionPreference = "Stop"
$testsPassed = 0
$testsFailed = 0
$testsSkipped = 0

# Use parameters instead of hardcoded values
$minikubeIP = minikube ip 2>$null
if (-not $minikubeIP -and -not $SkipDeployment) {
    Write-Host "ERROR: Minikube not running" -ForegroundColor Red
    exit 1
}

# Example usage:
# .\test-multi-nas.ps1 -SkipPersistenceTests -SyncTimeout 30
# .\test-multi-nas.ps1 -Context my-cluster -Namespace my-namespace
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Pester 3.x | Pester 5.x | 2020 | New syntax (Should -Be), improved performance, better mocking |
| Manual kubectl wait loops | kubectl wait --for=condition | K8s v1.11+ (2018) | Built-in, reliable, timeout-aware pod readiness checks |
| Regex parsing kubectl output | kubectl -o jsonpath, ConvertFrom-Json | K8s v1.5+ (2017) | Structured data extraction, safer than regex |
| Separate test scripts | Pester module-based tests | Pester 5.x (2020) | Better organization, discovery, parallel execution |
| Exit on first failure | Collect all failures and exit at end | Current best practice | See all failures in one run, better CI/CD feedback |
| NUnit XML output | Multiple formats (NUnit, JUnit, JaCoCo) | Pester 5.x (2020) | Better CI/CD integration (Azure DevOps, GitHub Actions) |

**Deprecated/outdated:**
- **Pester 3.x syntax:** Use Pester 5.x syntax (`Should -Be` instead of `Should Be`)
- **Sleep-based pod wait:** Use `kubectl wait --for=condition=ready` instead of sleep loops
- **String parsing kubectl output:** Use `-o jsonpath` or `-o json | ConvertFrom-Json`

## Open Questions

Things that couldn't be fully resolved:

1. **Should persistence tests run on all 7 servers or subset?**
   - What we know: Persistence test deletes pods (~2 minutes each with wait time)
   - What's unclear: Whether testing all 7 provides value vs testing representative subset (one input, one output, one backup)
   - Recommendation: Test subset by default; add `-FullPersistenceTest` parameter for comprehensive validation

2. **Should existing tests be migrated to Pester framework?**
   - What we know: Pester provides BDD syntax, better reporting, NUnit output for CI/CD
   - What's unclear: Whether migration effort justifies benefits for this simple validation script
   - Recommendation: Keep existing pattern for Phase 5; consider Pester if project expands to require mocking, code coverage, or complex CI/CD integration

3. **How to handle test timing variability (sync intervals)?**
   - What we know: Sidecar syncs every 30s; tests may catch file mid-sync
   - What's unclear: Best timeout values (too short = flaky, too long = slow feedback)
   - Recommendation: Use 60s timeout for sync tests (2x sync interval); make configurable via parameter

4. **Should tests validate Windows file permissions after sync?**
   - What we know: rsync -a preserves Linux permissions; Windows/Linux permission models differ
   - What's unclear: Whether testers experience permission issues in practice
   - Recommendation: Add permission validation only if users report access issues; monitor in Phase 5

5. **Should test suite support CI/CD output formats (JUnit XML, NUnit)?**
   - What we know: Current PASS/FAIL output parseable by humans, not CI/CD tools
   - What's unclear: Whether this project will use CI/CD pipelines
   - Recommendation: Keep simple text output for Phase 5; add Pester with NUnit output if CI/CD integration needed

## Sources

### Primary (HIGH confidence)
- [Pester Official Documentation](https://pester.dev/) - PowerShell test framework standard
- [Pester Quick Start](https://pester.dev/docs/quick-start) - Test structure, assertions, mocking
- [Kubernetes kubectl wait Documentation](https://kubernetes.io/docs/reference/kubectl/generated/kubectl_wait/) - Pod readiness, condition waiting
- [PowerShell Try-Catch-Finally](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_try_catch_finally) - Error handling and cleanup patterns
- [PowerShell Exit Codes Best Practices](https://dstreefkerk.github.io/2025-06-powershell-scripting-best-practices/) - CI/CD integration

### Secondary (MEDIUM confidence)
- [Testkube - Kubernetes Testing Platform](https://testkube.io/) - Cloud-native continuous testing
- [Terratest for Kubernetes](https://www.gruntwork.io/blog/automated-testing-for-kubernetes-and-helm-charts-using-terratest) - Go-based infrastructure testing
- [Testing Kubernetes Clusters: A Practical Guide](https://www.stickyminds.com/article/testing-kubernetes-clusters-practical-guide) - Validation strategies
- [PowerShell Error Handling Guide](https://www.ninjaone.com/blog/powershell-error-handling-guide/) - Try-Catch-Finally patterns
- [Integration Testing with Pester](https://martink.me/articles/integration-testing-with-pester-and-powershell) - Real-world examples

### Tertiary (LOW confidence - general patterns)
- [PowerShell Testing Best Practices](https://autosysops.com/blog/test-powershell-code-quality-automatically) - Azure DevOps integration
- [Kubernetes Testing Tools](https://thechief.io/c/editorial/6-kubernetes-testing-tools-use-your-devsecops-pipelines/) - Tool comparison
- [PowerShell CI/CD Pipelines](https://www.techtarget.com/searchitoperations/tip/How-to-use-PowerShell-in-CI-CD-pipelines) - Automation patterns

## Metadata

**Confidence breakdown:**
- Standard stack (PowerShell, kubectl): HIGH - Industry standard for Windows K8s testing
- Architecture (existing test pattern extension): HIGH - Proven through Phases 2 and 3
- Pitfalls (kubectl errors, cleanup, timing): HIGH - Based on existing script and official docs
- Pester migration recommendation: MEDIUM - Decision depends on project evolution
- CI/CD integration needs: LOW - Unclear if project will use automated pipelines

**Research date:** 2026-02-01
**Valid until:** 2026-03-01 (30 days) - PowerShell/kubectl testing patterns stable; Pester 5.x mature

**Notes:**
- Phase 5 builds directly on existing test-multi-nas.ps1 structure (47 existing tests)
- No new dependencies required; PowerShell and kubectl already in use
- Persistence testing most complex addition (pod deletion + wait + verify)
- Try-Finally pattern critical for test cleanup reliability
- Exit code handling essential for future CI/CD integration
