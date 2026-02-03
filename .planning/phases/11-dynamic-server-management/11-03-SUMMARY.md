---
phase: 11-dynamic-server-management
plan: 03
subsystem: api
tags: [kubernetes, sftp, nas, nfs, delete, dynamic-servers]

# Dependency graph
requires:
  - phase: 11-dynamic-server-management/02
    provides: IKubernetesManagementService interface, FTP creation pattern
provides:
  - CreateSftpServerAsync with atmoz/sftp and user:pass:uid:gid format
  - CreateNasServerAsync with erichough/nfs-server and SubPath isolation
  - DeleteServerAsync with explicit service cleanup
  - Dynamic server detection in discovery service
affects: [11-04, 11-05, api, dashboard]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "SFTP user format: username:password:uid:gid as container Args"
    - "NAS directory presets: input->nas-input-dynamic, output->nas-output-dynamic"
    - "SubPath on shared PVC for NAS directory isolation"
    - "Explicit service deletion before deployment deletion"

key-files:
  created: []
  modified:
    - src/FileSimulator.ControlApi/Services/IKubernetesManagementService.cs
    - src/FileSimulator.ControlApi/Services/KubernetesManagementService.cs
    - src/FileSimulator.ControlApi/Services/KubernetesDiscoveryService.cs

key-decisions:
  - "SFTP uses atmoz/sftp with Args format user:pass:uid:gid (not env vars)"
  - "NAS uses SubPath on shared PVC for directory isolation (no dedicated PVCs)"
  - "Directory presets: input, output, backup resolve to nas-*-dynamic subdirectories"
  - "DeleteServerAsync explicitly deletes services first (no cascade from deployment)"
  - "Helm-managed servers protected from deletion (must have managed-by=control-api)"

patterns-established:
  - "Protocol-specific container configuration based on Helm template blueprints"
  - "Explicit resource cleanup order: services -> deployments -> optional PVC"
  - "IsDynamic property set from managed-by label in discovery service"

# Metrics
duration: 7min
completed: 2026-02-03
---

# Phase 11 Plan 03: SFTP and NAS Server Creation with Deletion Summary

**SFTP and NAS dynamic server creation plus explicit deletion with service cleanup for complete CRUD operations**

## Performance

- **Duration:** 7 min
- **Started:** 2026-02-03T10:30:00Z
- **Completed:** 2026-02-03T10:37:00Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Implemented CreateSftpServerAsync using atmoz/sftp image with user:pass:uid:gid format
- Implemented CreateNasServerAsync using erichough/nfs-server with SubPath directory isolation
- Implemented DeleteServerAsync with explicit service cleanup and Helm protection
- Extended CreateSftpServerRequest with Uid and Gid properties
- Extended CreateNasServerRequest with Directory and ExportOptions properties
- Updated KubernetesDiscoveryService to set IsDynamic based on managed-by label

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement SFTP and NAS server creation** - `22909e8` (feat)
2. **Task 2: Add dynamic server detection in discovery service** - `2d5d0f1` (feat)

## Files Created/Modified
- `src/FileSimulator.ControlApi/Services/IKubernetesManagementService.cs` - Added Uid/Gid to SFTP request, Directory/ExportOptions to NAS request
- `src/FileSimulator.ControlApi/Services/KubernetesManagementService.cs` - Complete SFTP, NAS creation and deletion (748 lines)
- `src/FileSimulator.ControlApi/Services/KubernetesDiscoveryService.cs` - Added IsDynamic and ManagedBy from labels

## Decisions Made
- **SFTP Args format:** The atmoz/sftp image uses container Args (not env vars) for user configuration in format `username:password:uid:gid`
- **NAS SubPath isolation:** Dynamic NAS servers use SubPath on the shared PVC rather than dedicated PVCs to maintain file visibility across servers
- **Directory presets:** "input", "output", "backup" resolve to standard subdirectory names (nas-input-dynamic, etc.)
- **Explicit service deletion:** Services must be deleted explicitly before deployments since Kubernetes doesn't cascade delete them
- **Helm protection:** DeleteServerAsync only works on servers with `managed-by=control-api` label to protect static Helm-managed resources

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All three server types (FTP, SFTP, NAS) can now be created dynamically
- DeleteServerAsync ready for wiring to DELETE /api/servers/{name} endpoint
- Stop/Start/Restart placeholder implementations remain for plan 11-04
- Discovery service properly distinguishes dynamic from static servers

---
*Phase: 11-dynamic-server-management*
*Completed: 2026-02-03*
