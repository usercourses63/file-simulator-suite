---
phase: 01-single-nas-validation
plan: 01
type: execute
completed: 2026-01-29
duration: 2.4min

subsystem: infrastructure/helm
tags: [helm, kubernetes, nfs, unfs3, security]

dependencies:
  requires: []
  provides:
    - "nas-test.yaml Helm template with init container pattern"
    - "nasTest values configuration section"
  affects:
    - "01-02 (deployment testing depends on this template)"
    - "02-* (multi-NAS templates will follow this pattern)"

tech_stack:
  added:
    - unfs3 (userspace NFSv3 server)
    - rsync (file synchronization in init container)
  patterns:
    - init-container-sync (Windows hostPath -> emptyDir pattern)
    - non-privileged-nfs (NET_BIND_SERVICE capability vs privileged mode)

files:
  created:
    - helm-chart/file-simulator/templates/nas-test.yaml
  modified:
    - helm-chart/file-simulator/values.yaml

decisions:
  - id: DEC-001
    title: "Use unfs3 instead of kernel NFS (erichough/nfs-server)"
    rationale: "Kernel NFS cannot export CIFS/9p filesystems from Windows mounts; userspace NFS (unfs3) works with any filesystem type"
    impact: "Entire NAS architecture pattern changed; removes need for privileged containers"
    alternatives: ["nfs-ganesha (too complex)", "erichough/nfs-server (doesn't work with Windows mounts)"]

  - id: DEC-002
    title: "Init container + emptyDir pattern for file sync"
    rationale: "NFS cannot directly export Windows hostPath; init container copies files to Linux-native emptyDir which can be exported"
    impact: "Two-container pod pattern; adds rsync dependency; files synced at pod start"
    alternatives: ["Direct hostPath export (kernel limitation)", "Sidecar continuous sync (more complex, phase 3 concern)"]

  - id: DEC-003
    title: "NET_BIND_SERVICE capability instead of privileged mode"
    rationale: "Port 2049 requires privileged port binding; capability is sufficient, privileged: true is overly permissive"
    impact: "Better security posture; meets non-privileged requirement; may need DAC_READ_SEARCH if permission issues arise"
    alternatives: ["privileged: true (rejected - security risk)", "unprivileged port like 20049 (breaks NFS standard)"]

  - id: DEC-004
    title: "Disk-backed emptyDir (not memory-backed)"
    rationale: "Memory-backed emptyDir (medium: Memory) would lose data on pod restart; disk-backed persists files"
    impact: "Files survive pod restarts; init container only re-syncs on restart, not data loss"
    alternatives: ["emptyDir {medium: Memory} (rejected - data loss)"]

metrics:
  tasks_completed: 2
  files_changed: 2
  lines_added: 196
  commits: 2
---

# Phase 1 Plan 01: Create NAS Test Helm Template Summary

**One-liner:** Implemented init container + unfs3 pattern for non-privileged NFS export of Windows directories in Kubernetes

## What Was Built

Created the foundational Helm template for single NAS instance testing using the init container + unfs3 architecture pattern. This is the make-or-break validation that Windows-mounted directories can be exposed via NFS in Kubernetes without privileged security contexts.

### Artifacts Created

1. **nas-test.yaml Helm Template** (153 lines)
   - Conditional deployment block (enabled via nasTest.enabled)
   - Init container: Alpine + rsync for Windows->emptyDir sync
   - Main container: Alpine + unfs3 for NFSv3 server
   - Two volumes: windows-data (hostPath) and nfs-export (emptyDir)
   - Service resource with ClusterIP/NodePort support
   - TCP liveness/readiness probes on port 2049

2. **nasTest Configuration Section** (43 lines in values.yaml)
   - Image configuration (initImage and image)
   - Instance settings (name, fsid, dataPath)
   - NFS export options
   - Service configuration (type, port, nodePort)
   - Resource limits

### Key Implementation Details

**Init Container Pattern:**
```yaml
initContainers:
  - name: sync-windows-data
    command: |
      apk add --no-cache rsync
      mkdir -p /nfs-data
      rsync -av --delete /windows-mount/ /nfs-data/
    volumeMounts:
      - name: windows-data (hostPath) -> /windows-mount (readOnly)
      - name: nfs-export (emptyDir) -> /nfs-data
```

**Main Container Pattern:**
```yaml
containers:
  - name: nfs-server
    command: |
      apk add --no-cache unfs3
      echo '/data *(rw,sync,no_root_squash),fsid=1' > /etc/exports
      unfsd -d -p -t -n 2049 -e /etc/exports
    securityContext:
      capabilities:
        add: [NET_BIND_SERVICE]
        drop: [ALL]
      allowPrivilegeEscalation: false
```

**Security Context:**
- NO `privileged: true` anywhere
- Uses `NET_BIND_SERVICE` capability for port 2049 binding
- `allowPrivilegeEscalation: false` on both containers
- Drops all other capabilities

**unfs3 Flags:**
- `-d`: Foreground mode (required for containers)
- `-p`: No portmapper registration (single-port operation)
- `-t`: TCP-only (no UDP, simplifies networking)
- `-n 2049`: Bind to port 2049

## Technical Decisions

### DEC-001: unfs3 vs Kernel NFS
**Context:** Existing nas.yaml uses erichough/nfs-server (kernel NFS) which fails with Windows mounts

**Decision:** Use unfs3 userspace NFS server

**Rationale:**
- Linux kernel NFS cannot export CIFS/9p filesystems (Windows mounts in Minikube)
- Userspace NFS works with any filesystem type
- Eliminates need for privileged containers

**Impact:** Complete architecture change for NAS servers; affects all future NAS deployments

### DEC-002: Init Container Sync Pattern
**Context:** NFS cannot directly export Windows hostPath due to kernel limitations

**Decision:** Use init container with rsync to copy Windows hostPath → emptyDir

**Rationale:**
- Decouples Windows mount from NFS export
- emptyDir is Linux-native filesystem that NFS can export
- Init container guarantees sync completes before NFS server starts

**Impact:**
- Two-container pod pattern (adds complexity)
- Files synced at pod start (one-time, not continuous)
- Phase 3 will add continuous sync if needed

### DEC-003: Minimal Capabilities
**Context:** Port 2049 is privileged (< 1024), typically requires root or privileged mode

**Decision:** Use NET_BIND_SERVICE capability instead of privileged: true

**Rationale:**
- Capability grants specific permission for privileged port binding
- Avoids overly permissive privileged mode
- Meets security requirement for non-privileged containers

**Impact:**
- Better security posture
- May need to add DAC_READ_SEARCH if permission issues arise (open question from research)

### DEC-004: Disk-Backed emptyDir
**Context:** emptyDir can be memory-backed (tmpfs) or disk-backed

**Decision:** Use default disk-backed emptyDir (not medium: Memory)

**Rationale:**
- Memory-backed loses data on pod restart
- Disk-backed persists across pod lifecycle
- Init container only needs to re-sync on restart, not every time

**Impact:** Files survive pod restarts; memory efficiency

## Verification Results

### Helm Lint
```
helm lint helm-chart/file-simulator
✓ 1 chart(s) linted, 0 chart(s) failed
```

### Helm Template Rendering
```
helm template file-sim helm-chart/file-simulator --set nasTest.enabled=true
✓ Renders complete YAML without errors
✓ Shows initContainers with rsync
✓ Shows containers with unfs3
✓ Shows NET_BIND_SERVICE capability
✓ NO privileged: true in nas-test-1 deployment
```

### Security Verification
- `allowPrivilegeEscalation: false` ✓
- `capabilities.add: [NET_BIND_SERVICE]` ✓
- `capabilities.drop: [ALL]` ✓
- NO `privileged: true` ✓

## Deviations from Plan

None - plan executed exactly as written.

## Commits

| Commit | Type | Description |
|--------|------|-------------|
| 298a645 | feat | Create nas-test.yaml Helm template with init container + unfs3 pattern |
| e33cad5 | feat | Add nasTest configuration section to values.yaml |

## Next Phase Readiness

### Blockers
None.

### Prerequisites for 01-02 (Deployment Testing)
- ✓ nas-test.yaml template exists and renders
- ✓ values.yaml has nasTest configuration
- ✓ Helm lint passes
- Ready for: Deploy to Minikube and validate NFS connectivity

### Open Questions from Research
1. **Does unfs3 need CAP_DAC_READ_SEARCH?** - Will discover during testing (01-02)
2. **Performance impact of rsync on pod restart?** - Will measure during testing
3. **File ownership mapping (Windows uid/gid)?** - Will verify with `ls -ln` during testing

### Impact on Future Plans
- **Phase 1 Plans 02-03:** Can proceed with deployment and testing
- **Phase 2 (Multi-NAS):** This pattern will be replicated 7 times with unique fsid values
- **Phase 3 (Continuous Sync):** May need to replace init container with sidecar if 30-second visibility requirement needs continuous sync

## Files Changed

### Created
- `helm-chart/file-simulator/templates/nas-test.yaml` (153 lines)
  - Deployment with init + main containers
  - Service with ClusterIP/NodePort
  - Complete volume configuration

### Modified
- `helm-chart/file-simulator/values.yaml` (+43 lines)
  - Added nasTest configuration section after nas section
  - All configuration parameterized via values

## Metrics

- **Duration:** 2.4 minutes (from 10:01:33 to 10:03:54 UTC)
- **Tasks completed:** 2/2 (100%)
- **Files created:** 1
- **Files modified:** 1
- **Lines added:** 196
- **Commits:** 2
- **Helm lint:** PASS
- **Template render:** PASS

## Success Criteria Met

- ✓ nas-test.yaml template renders without Helm errors
- ✓ Template implements init container + unfs3 pattern exactly as specified
- ✓ Security context uses capabilities, not privileged mode
- ✓ values.yaml has complete nasTest configuration section
- ✓ All configuration values parameterized via values

## Notes

This plan validates the core architectural assumption for the entire project: that Windows directories can be exposed via NFS in Kubernetes without privileged containers. The init container + emptyDir pattern works around the kernel limitation that NFS cannot directly export CIFS/9p filesystems.

The next step (01-02) will deploy this template to Minikube and verify:
1. Init container successfully syncs Windows files
2. unfs3 starts and serves NFS on port 2049
3. Client pods can mount and access files
4. No "stale file handle" or permission errors

If successful, Phase 2 will replicate this pattern 7 times for the full multi-NAS topology.
