---
phase: 03-bidirectional-sync
plan: 01
type: implementation
subsystem: nfs-storage
tags: [kubernetes, helm, sidecar, rsync, bidirectional-sync, nfs]

dependencies:
  requires:
    - "02-01: Multi-NAS Helm template with range loop pattern"
    - "02-02: 7-server deployment validated with storage isolation"
  provides:
    - "Sidecar configuration infrastructure for continuous sync"
    - "Conditional sidecar deployment based on server role"
    - "Init container conditional --delete logic by server name pattern"
  affects:
    - "03-02: Bidirectional sync validation (deployment + testing)"
    - "Phase 4: External NFS mount testing (sidecar provides Windows file retrieval)"

tech-stack:
  added:
    - pattern: "Kubernetes native sidecar (restartPolicy: Always)"
      reason: "Continuous sync lifecycle management"
    - pattern: "emptyDir sizeLimit"
      reason: "Disk exhaustion prevention (K8s best practice)"
  patterns:
    - name: "Conditional Helm template for sidecar"
      location: "helm-chart/file-simulator/templates/nas-multi.yaml"
      description: "if $nas.sidecar.enabled block for selective deployment"
    - name: "Server name pattern matching for --delete logic"
      location: "helm-chart/file-simulator/templates/nas-multi.yaml"
      description: "contains function to check nas-input vs nas-output/backup"
    - name: "readOnly volume mount in sidecar"
      location: "helm-chart/file-simulator/templates/nas-multi.yaml"
      description: "Prevents sidecar from writing to NFS export (loop prevention)"

key-files:
  created: []
  modified:
    - path: "helm-chart/file-simulator/values-multi-nas.yaml"
      changes: "Added sidecar configuration blocks to all 7 NAS servers"
      impact: "Enables conditional sidecar deployment per server"
    - path: "helm-chart/file-simulator/templates/nas-multi.yaml"
      changes: "Added native sidecar init container, conditional --delete, emptyDir sizeLimit"
      impact: "Implements continuous NFS->Windows sync for output servers"

decisions:
  - id: "[03-01] Native sidecar over regular container"
    choice: "Use init container with restartPolicy: Always"
    rationale: "Ensures proper startup ordering (after init container completes) and graceful shutdown"
    alternatives: ["Regular container with lifecycle hooks", "DaemonSet pattern"]
    impact: "Requires Kubernetes 1.29+ for native sidecar support"

  - id: "[03-01] Init container --delete based on server NAME, not sidecar.enabled"
    choice: "Use contains function to check 'nas-input' in server name"
    rationale: "nas-backup has sidecar.enabled=false but is NOT an input server; decision is about preserving NFS-written files"
    alternatives: ["Check sidecar.enabled flag", "Add separate initDeleteOrphans flag"]
    impact: "Input servers delete orphans, output/backup preserve NFS-written files across restarts"

  - id: "[03-01] nas-backup gets sidecar.enabled: false"
    choice: "Disable sidecar for nas-backup (read-only export)"
    rationale: "Read-only NFS export (ro) cannot receive writes; sidecar would have nothing to sync back to Windows"
    alternatives: ["Enable sidecar anyway for consistency", "Make backup read-write"]
    impact: "Saves 32Mi memory request; backup server remains read-only demonstration"

  - id: "[03-01] emptyDir sizeLimit set to 500Mi"
    choice: "Add sizeLimit: 500Mi to all emptyDir volumes"
    rationale: "Kubernetes best practice from Phase 3 research; prevents disk exhaustion"
    alternatives: ["No limit (unbounded)", "1Gi limit", "Memory-backed emptyDir"]
    impact: "Pod evicted if NFS export exceeds 500Mi; protects node from disk fill"

metrics:
  duration: "3.6 minutes"
  completed: "2026-02-01"
  tasks: 2
  commits: 2
  files_modified: 2
---

# Phase 3 Plan 1: Add Sidecar Configuration for Bidirectional Sync Summary

**One-liner:** Added Kubernetes native sidecar configuration for continuous NFS-to-Windows sync on output servers, with conditional deployment via Helm and server name-based init container --delete logic.

## Objective

Add Kubernetes native sidecar configuration to output NAS servers (nas-output-1/2/3) for continuous NFS-to-Windows sync, enabling files written via NFS mount to appear on Windows within 60 seconds and completing the bidirectional sync pattern required for output NAS servers.

## Execution Details

### Tasks Completed

| Task | Description | Commit | Files Modified |
|------|-------------|--------|----------------|
| 1 | Add sidecar configuration to values-multi-nas.yaml | e6b7b83 | values-multi-nas.yaml |
| 2 | Add conditional sidecar to nas-multi.yaml template | e9da3b7 | nas-multi.yaml |

### What Was Built

**1. Sidecar Configuration in Values (Task 1)**

Added `sidecar` configuration blocks to all 7 NAS servers in values-multi-nas.yaml:

- **Input servers (nas-input-1/2/3):** sidecar.enabled: false (Windows is source of truth, no reverse sync needed)
- **Output servers (nas-output-1/2/3):** sidecar.enabled: true with full configuration:
  * syncInterval: 30 seconds (configurable per server)
  * Image: alpine:latest
  * Resources: 32Mi request / 64Mi limit memory, 25m/100m CPU
- **Backup server (nas-backup):** sidecar.enabled: false (read-only export cannot receive NFS writes)

Resource impact documented: 3 sidecars × 32Mi = 96Mi additional memory request (total system: 544Mi requests).

**2. Conditional Sidecar Template (Task 2)**

Modified nas-multi.yaml template to add:

- **Kubernetes native sidecar container** using `restartPolicy: Always` on init container
  * Runs continuous rsync loop: NFS export → Windows hostPath
  * Interval configurable via `$nas.sidecar.syncInterval` (default 30s)
  * Mounts NFS export as `readOnly: true` (prevents accidental writes, loop prevention)
  * Deploys only when `$nas.sidecar.enabled` is true (3 output servers)

- **Conditional init container --delete logic** based on server NAME pattern:
  * Input servers (contains "nas-input"): `rsync -av --delete` (Windows is source of truth)
  * Output/backup servers: `rsync -av` (preserve NFS-written files across restarts)
  * Uses Helm `contains` function to check server name, NOT sidecar.enabled flag

- **emptyDir sizeLimit: 500Mi** on all NFS export volumes (best practice, prevents disk exhaustion)

### Architecture Pattern

```
Windows hostPath                    emptyDir (NFS export)
     │                                     │
     │  ┌─────────────────────┐           │
     ├──│ Init Container      │───────────►  Pod start: Windows → NFS
     │  │ rsync Windows→NFS   │           │  (conditional --delete by server name)
     │  └─────────────────────┘           │
     │                                     │
     │  ┌─────────────────────┐           │
     ◄──│ Sidecar Container   │───────────┤  Continuous: NFS → Windows
     │  │ restartPolicy:      │           │  (30s interval, only output servers)
     │  │   Always            │           │
     │  └─────────────────────┘           │
     │                                     │
     │  ┌─────────────────────┐           │
     │  │ Main Container      │───────────┤  NFS server: serves emptyDir
     │  │ unfs3 NFS server    │           │
     │  └─────────────────────┘           │
```

**Loop prevention:** Separate one-way syncs prevent infinite loops. Init runs ONCE at pod start; sidecar runs continuously but only reads from NFS export (readOnly mount).

## Deviations from Plan

None - plan executed exactly as written.

## Decisions Made

**1. Native sidecar over regular container**
- **Decision:** Use init container with `restartPolicy: Always` for sidecar pattern
- **Rationale:** Kubernetes native sidecar (stable v1.33) ensures proper startup ordering (after init completes) and graceful shutdown (after main container)
- **Impact:** Requires K8s 1.29+ but provides cleaner lifecycle management than regular container

**2. Init container --delete based on server NAME, not sidecar.enabled**
- **Decision:** Use `contains "nas-input" $nas.name` to determine --delete flag
- **Rationale:** The --delete decision is about whether to preserve NFS-written files, not whether sidecar runs. nas-backup has sidecar.enabled=false (read-only export) but is NOT an input server, so should NOT use --delete.
- **Impact:** Clear separation of concerns: sidecar.enabled controls reverse sync, server name pattern controls Windows→NFS sync behavior

**3. nas-backup gets sidecar.enabled: false**
- **Decision:** Disable sidecar for nas-backup server
- **Rationale:** nas-backup has exportOptions "ro" (read-only). NFS clients cannot write to read-only exports, so there's nothing for the sidecar to sync back to Windows. Sidecar would be wasted resources.
- **Impact:** Saves 32Mi memory request; backup server remains read-only demonstration

**4. emptyDir sizeLimit set to 500Mi**
- **Decision:** Add `sizeLimit: 500Mi` to all emptyDir volumes
- **Rationale:** Kubernetes best practice (GA in v1.25); prevents runaway file writes from exhausting node disk
- **Impact:** Pod evicted if NFS export exceeds 500Mi (protects node, fail-safe for application)

## Validation Results

All verification checks passed:

```powershell
# Template renders without errors
helm template file-sim ./helm-chart/file-simulator -f ./helm-chart/file-simulator/values-multi-nas.yaml
# ✓ No errors, 1599 lines of output

# Sidecar appears only for output servers (3 total)
grep -c "sync-to-windows"
# ✓ 3 occurrences (nas-output-1, nas-output-2, nas-output-3)

# restartPolicy: Always on all sidecars
grep -A 2 "restartPolicy: Always"
# ✓ 3 occurrences with proper syntax

# emptyDir has sizeLimit on all 7 servers
grep -A 2 "emptyDir:" | grep "sizeLimit: 500Mi"
# ✓ 7 occurrences (all servers)

# Init container --delete logic correct
grep "rsync -av --delete /windows-mount/"
# ✓ 3 occurrences (nas-input-1/2/3 only)

grep "rsync -av /windows-mount/"
# ✓ 4 occurrences (nas-output-1/2/3, nas-backup)

# Input servers and nas-backup have NO sidecar
grep -A 3 "nas-input-1" | grep "sync-to-windows"
# ✓ 0 occurrences
grep -A 3 "nas-backup" | grep "sync-to-windows"
# ✓ 0 occurrences
```

## Technical Details

### Sidecar Resource Allocation

**Per sidecar (3 total):**
- Requests: 32Mi memory, 25m CPU
- Limits: 64Mi memory, 100m CPU

**Total system impact:**
- Previous: 7 NAS pods = 448Mi requests, 1.75Gi limits
- New: 7 NAS + 3 sidecars = 544Mi requests, 1.94Gi limits
- Fits comfortably in 8GB Minikube with room for microservices

### Sidecar Lifecycle

1. **Pod start:** Init container runs first, syncs Windows → NFS
2. **After init completes:** Sidecar starts (restartPolicy: Always triggers)
3. **Main container starts:** NFS server begins serving emptyDir
4. **Sidecar runs continuously:** Every 30 seconds, syncs NFS → Windows
5. **Pod shutdown:** Main container stops, THEN sidecar stops (graceful ordering)

### Loop Prevention Design

**Why this doesn't create infinite sync loops:**

1. Init container runs ONCE at pod start (not continuous)
2. Init runs BEFORE sidecar starts (proper ordering)
3. Sidecar only syncs one direction: NFS → Windows
4. Sidecar has readOnly mount on NFS export (cannot create files in /nfs-data)
5. Next pod restart: init container syncs fresh from Windows (captures any changes made while pod was down)

## Next Steps

**Immediate (Plan 03-02):**
1. Deploy with sidecar configuration to file-simulator cluster
2. Validate sidecar starts after init container completes
3. Test continuous sync: write file via NFS, verify appears on Windows within 60 seconds
4. Monitor sidecar CPU/memory usage during sync operations
5. Test pod restart: verify output NAS preserves NFS-written files (no --delete on init)

**Future phases:**
- Phase 4: External NFS mount testing will rely on sidecar to retrieve output files from Windows
- Phase 5: Production deployment with tuned sync intervals based on Phase 3 measurements

## Risks & Mitigations

**Risk 1: Kubernetes version < 1.29 doesn't support native sidecar**
- **Mitigation:** Check `kubectl version` during deployment; fall back to regular container pattern if needed
- **Impact:** Low (Minikube usually up-to-date)

**Risk 2: Sync interval too aggressive for large directories**
- **Mitigation:** rsync is efficient (only transfers deltas); configurable interval per server
- **Impact:** Low (30s interval proven reasonable in research)

**Risk 3: emptyDir exceeds 500Mi causing pod eviction**
- **Mitigation:** Intentional fail-safe; indicates runaway file creation in application
- **Impact:** Medium (requires investigation but protects node from disk fill)

## Performance Metrics

- **Execution time:** 3.6 minutes
- **Tasks:** 2/2 completed
- **Commits:** 2 (atomic per task)
- **Files modified:** 2 (values + template)
- **Lines changed:** ~110 lines added (61 values + 49 template)

## Links

- **Plan:** .planning/phases/03-bidirectional-sync/03-01-PLAN.md
- **Research:** .planning/phases/03-bidirectional-sync/03-RESEARCH.md
- **Previous phase:** .planning/phases/02-7-server-topology/02-03-SUMMARY.md
- **Next plan:** .planning/phases/03-bidirectional-sync/03-02-PLAN.md (validation)

## Success Criteria Met

- [x] values-multi-nas.yaml has sidecar config for all 7 servers
- [x] Only output servers (3) have sidecar.enabled: true
- [x] nas-backup has sidecar.enabled: false (read-only export rationale documented)
- [x] nas-multi.yaml has conditional sidecar using restartPolicy: Always
- [x] Init container uses conditional --delete based on server NAME pattern (not sidecar.enabled)
- [x] emptyDir has sizeLimit: 500Mi
- [x] helm template renders without errors
- [x] Sidecar appears only for output servers (3 total)

---
**Status:** ✅ Complete
**Phase:** 3 of 5 (Bidirectional Sync)
**Plan:** 1 of 2 (Add Sidecar Configuration)
