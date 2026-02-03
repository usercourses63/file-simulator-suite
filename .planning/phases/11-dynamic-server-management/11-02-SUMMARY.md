---
phase: 11-dynamic-server-management
plan: 02
subsystem: api
tags: [kubernetes, k8s-client, ownerReferences, ftp, dynamic-servers]

# Dependency graph
requires:
  - phase: 11-dynamic-server-management/01
    provides: Request models, validators, RBAC permissions
provides:
  - IKubernetesManagementService interface with 9 methods
  - KubernetesManagementService with CreateFtpServerAsync
  - DiscoveredServer.IsDynamic and ManagedBy properties
  - IKubernetes registered in DI container
affects: [11-03, 11-04, 11-05, api, dashboard]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ownerReferences to control plane POD for automatic garbage collection"
    - "Label selector: app.kubernetes.io/managed-by=control-api for dynamic resources"

key-files:
  created:
    - src/FileSimulator.ControlApi/Services/IKubernetesManagementService.cs
    - src/FileSimulator.ControlApi/Services/KubernetesManagementService.cs
  modified:
    - src/FileSimulator.ControlApi/Models/DiscoveredServer.cs
    - src/FileSimulator.ControlApi/Program.cs

key-decisions:
  - "OwnerReferences point to POD (not Deployment) for proper Kubernetes garbage collection"
  - "Dynamic resources labeled with app.kubernetes.io/managed-by=control-api"
  - "IKubernetes registered as singleton in DI for sharing between services"

patterns-established:
  - "Dynamic resource creation: GetControlPlanePod -> Build labels+ownerRef -> Create Deployment -> Create Service"
  - "Instance naming: {releasePrefix}-{protocol}-{name} for resource uniqueness"

# Metrics
duration: 6min
completed: 2026-02-03
---

# Phase 11 Plan 02: IKubernetesManagementService Interface and FTP Creation Summary

**IKubernetesManagementService interface with 9 lifecycle methods and FTP server creation using ownerReferences to control plane pod**

## Performance

- **Duration:** 6 min
- **Started:** 2026-02-03T10:00:00Z
- **Completed:** 2026-02-03T10:06:00Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Created IKubernetesManagementService interface defining complete CRUD + lifecycle operations
- Implemented CreateFtpServerAsync with Deployment and NodePort Service creation
- Added ownerReferences pointing to control plane POD for automatic cleanup
- Extended DiscoveredServer with IsDynamic and ManagedBy properties to distinguish dynamic from static servers
- Registered IKubernetes as singleton for DI injection

## Task Commits

Each task was committed atomically:

1. **Task 1: Create IKubernetesManagementService interface and extend DiscoveredServer** - `9941a11` (feat)
2. **Task 2: Implement KubernetesManagementService with FTP creation** - `62c9486` (feat)

## Files Created/Modified
- `src/FileSimulator.ControlApi/Services/IKubernetesManagementService.cs` - Interface with 9 methods + request records
- `src/FileSimulator.ControlApi/Services/KubernetesManagementService.cs` - FTP creation implementation (298 lines)
- `src/FileSimulator.ControlApi/Models/DiscoveredServer.cs` - Added IsDynamic and ManagedBy properties
- `src/FileSimulator.ControlApi/Program.cs` - Registered IKubernetes and IKubernetesManagementService

## Decisions Made
- **OwnerReferences target POD not Deployment:** Kubernetes garbage collection requires ownerReferences to point to the actual Pod resource (v1/Pod) rather than the Deployment, ensuring proper cleanup when control plane pod restarts
- **IKubernetes shared via DI:** Rather than each service creating its own client, registered IKubernetes as singleton so both discovery and management services can share the connection
- **Placeholder implementations for future plans:** SFTP, NAS creation (plan 11-03) and stop/start/restart (plan 11-04) throw NotImplementedException to maintain interface contract

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- IKubernetesManagementService interface ready for SFTP and NAS implementations (plan 11-03)
- CreateFtpServerAsync pattern established for other protocols to follow
- Need to implement DeleteServerAsync, Stop/Start/Restart in subsequent plans

---
*Phase: 11-dynamic-server-management*
*Completed: 2026-02-03*
