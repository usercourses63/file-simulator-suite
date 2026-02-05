---
phase: 12
plan: 07
subsystem: deployment
tags: [powershell, automation, helm, docker, minikube]
requires: [12-06-helm-dashboard-deployment]
provides: [deploy-production-script, build-images-script]
affects: [deployment-workflow]
tech-stack:
  added: []
  patterns: [automated-deployment, registry-forwarding]
key-files:
  created:
    - scripts/Deploy-Production.ps1
    - scripts/Build-Images.ps1
  modified: []
decisions:
  - PowerShell scripts for Windows-based deployment automation
  - Background job for registry port-forwarding during deployment
  - 5m timeout for Helm deployment with 30s stabilization delay
  - Graceful handling when not running as Administrator
metrics:
  duration: 5min
  completed: 2026-02-05
---

# Phase 12 Plan 07: Deploy-Production.ps1 Automation Script Summary

**One-liner:** Complete deployment automation scripts for building images, deploying Helm chart, and verifying production readiness with single command execution.

## What Was Built

Created two PowerShell scripts for automated production deployment:

### 1. Build-Images.ps1 Helper Script
- Reusable functions for building Control API and Dashboard images
- Configurable registry host and API base URL parameters
- Separate build steps with colored console output
- Docker image push to registry automation
- Error handling with appropriate exit codes

### 2. Deploy-Production.ps1 Main Script
- Complete end-to-end deployment automation
- Cluster management with `-Clean` flag support
- Container registry setup with port-forwarding
- Image building and pushing workflow
- Helm chart deployment with wait
- Windows hosts file update (when Administrator)
- Deployment verification with health checks
- Formatted access URL summary display

## Technical Implementation

### Script Architecture
```powershell
# Build-Images.ps1 structure
- Build-ControlApi: Docker build from src/FileSimulator.ControlApi/Dockerfile
- Build-Dashboard: Docker build with VITE_API_BASE_URL build arg
- Push-Images: Push both images to registry
- Colored output helpers

# Deploy-Production.ps1 structure
- Test-Prerequisites: Verify minikube, kubectl, helm, docker installed
- Start-Cluster: Manage Minikube with Clean/verify logic
- Setup-Registry: Enable addon + background port-forward job
- Build-Images: Build and push both images
- Deploy-HelmChart: Helm upgrade --install with --wait
- Update-HostsFile: Call Setup-Hosts.ps1 when Administrator
- Test-Deployment: Verify pods, Control API, Dashboard
- Stop-RegistryPortForward: Cleanup background job
- Show-AccessUrls: Display all endpoints and commands
```

### Registry Port-Forwarding Pattern
```powershell
# Start background job for registry access
$script:RegistryJob = Start-Job -Name "RegistryPortForward" -ScriptBlock {
    kubectl --context=$profileName port-forward --namespace kube-system service/registry 5000:80
}

# Test with retry loop (30s timeout)
while ($attempt -lt $maxAttempts) {
    Invoke-WebRequest -Uri "http://localhost:5000/v2/" -TimeoutSec 2
}

# Cleanup on completion or error
Stop-Job -Job $script:RegistryJob
Remove-Job -Job $script:RegistryJob
```

### Deployment Flow
```
1. Verify prerequisites (minikube, kubectl, helm, docker)
2. Start/verify Minikube cluster (12GB RAM, 4 CPUs, hyperv driver)
3. Enable registry addon
4. Start port-forward in background (localhost:5000 -> registry:80)
5. Build Control API image
6. Build Dashboard image with API URL
7. Push both images to registry
8. Deploy Helm chart with --wait (5m timeout)
9. Wait 30s for pod stabilization
10. Update hosts file (if Administrator)
11. Verify pod status and health endpoints
12. Stop port-forward job
13. Display access URLs and useful commands
```

## Commits

| Commit | Task | Description |
|--------|------|-------------|
| 1f90f75 | 1 | Build-Images.ps1 helper script |
| ee356a7 | 2 | Deploy-Production.ps1 structure and prerequisites |
| 5309225 | 3 | Cluster management with Clean flag |
| 3bfd0b2 | 4 | Registry setup with port-forwarding |
| 1cecc22 | 5 | Image building and pushing |
| c711f4c | 6 | Helm deployment with wait |
| 11a9581 | 7 | Hosts file update with admin check |
| a365343 | 8 | Deployment verification with health checks |
| 14a713f | 9 | Cleanup and access URL summary |
| 5baabd1 | 10 | Error handling and exit codes |

## Usage Examples

### Standard Deployment
```powershell
# Deploy to existing cluster
.\scripts\Deploy-Production.ps1

# Deploy with clean cluster start
.\scripts\Deploy-Production.ps1 -Clean

# Deploy with custom resources
.\scripts\Deploy-Production.ps1 -Memory 16384 -Cpus 6
```

### Standalone Image Building
```powershell
# Build with defaults
.\scripts\Build-Images.ps1

# Build with custom registry
.\scripts\Build-Images.ps1 -RegistryHost "registry.example.com" `
                           -DashboardApiUrl "https://api.example.com"
```

## Key Features

### Progress Tracking
- Numbered steps with colored output
- Success (green), Info (yellow), Error (red) messages
- Detailed progress for each operation

### Error Handling
- Try/catch wraps entire deployment
- Stack trace on failure
- Cleanup on error (stop background jobs)
- Exit code 0 on success, 1 on failure

### Administrator Awareness
- Check for Administrator privilege
- Update hosts file automatically when admin
- Provide manual instructions when not admin
- Graceful degradation without blocking deployment

### Health Verification
- Parse kubectl JSON output for pod status
- Test Control API /health endpoint
- Test Dashboard root endpoint
- Handle hosts file not yet updated scenario

## Deviations from Plan

None - plan executed exactly as written.

## Next Phase Readiness

### Completed Deliverables
- ✅ Build-Images.ps1 provides reusable build functions
- ✅ Deploy-Production.ps1 automates full deployment workflow
- ✅ Script verifies prerequisites before starting
- ✅ Registry setup with port-forwarding automation
- ✅ Helm deployment with --wait and stabilization delay
- ✅ Hosts file update when running as Administrator
- ✅ Deployment verification with pod status and health checks
- ✅ Access URL summary with all endpoints
- ✅ Error handling with proper exit codes

### Phase 12 Status
- 7 of 10 plans complete
- Remaining: Test suite, documentation, production hardening

### Outstanding Items
None - deployment automation complete.

## Notes

- **Registry Port-Forward**: Background job pattern ensures registry is accessible during build
- **Stabilization Delay**: 30s wait after Helm --wait ensures pods fully ready before health checks
- **Administrator Check**: Uses WindowsPrincipal to detect privilege level
- **Clean Flag**: Enables reproducible deployments by deleting existing cluster
- **Context Safety**: Uses --kube-context flag for all kubectl/helm commands
- **Exit Codes**: Standard 0/1 pattern for scripting integration
