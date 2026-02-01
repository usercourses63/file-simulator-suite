---
phase: 04-configuration-templates
plan: 01
subsystem: storage
tags: [kubernetes, pv, pvc, nfs, static-provisioning]
requires: [02-01, 02-02, 02-03, 03-01, 03-02]
provides:
  - pv-pvc-manifests-7-nas
  - static-provisioning-templates
affects: [04-02, 04-03]
tech-stack:
  added: []
  patterns: [static-pv-pvc-binding, label-selector-binding]
key-files:
  created:
    - helm-chart/file-simulator/examples/pv/nas-input-1-pv.yaml
    - helm-chart/file-simulator/examples/pv/nas-input-2-pv.yaml
    - helm-chart/file-simulator/examples/pv/nas-input-3-pv.yaml
    - helm-chart/file-simulator/examples/pv/nas-backup-pv.yaml
    - helm-chart/file-simulator/examples/pv/nas-output-1-pv.yaml
    - helm-chart/file-simulator/examples/pv/nas-output-2-pv.yaml
    - helm-chart/file-simulator/examples/pv/nas-output-3-pv.yaml
    - helm-chart/file-simulator/examples/pvc/nas-input-1-pvc.yaml
    - helm-chart/file-simulator/examples/pvc/nas-input-2-pvc.yaml
    - helm-chart/file-simulator/examples/pvc/nas-input-3-pvc.yaml
    - helm-chart/file-simulator/examples/pvc/nas-backup-pvc.yaml
    - helm-chart/file-simulator/examples/pvc/nas-output-1-pvc.yaml
    - helm-chart/file-simulator/examples/pvc/nas-output-2-pvc.yaml
    - helm-chart/file-simulator/examples/pvc/nas-output-3-pvc.yaml
  modified: []
decisions:
  static-pv-provisioning: Use static PV/PVC provisioning (not dynamic) to match production OCP patterns where NAS infrastructure pre-exists
  label-selector-binding: Use label selector nas-server for PVC-to-PV binding instead of storageClassName
  retain-reclaim-policy: Use Retain reclaim policy on all PVs to prevent data loss on PVC deletion
  nfs-mount-options: Explicit NFSv3 mount options (tcp, hard, intr) for consistent behavior across K8s versions
metrics:
  tasks: 3
  commits: 3
  duration: 4.2
  completed: 2026-02-01
---

# Phase 4 Plan 01: PV/PVC Configuration Templates Summary

Static PV/PVC manifests for all 7 NAS servers with label-selector binding matching production OCP patterns

## Objective Achieved

Created 14 production-ready Kubernetes manifests (7 PVs + 7 PVCs) that enable applications to mount NAS servers using static provisioning patterns identical to production OpenShift environments.

## What Was Built

### PersistentVolume Manifests (7 files)

Each PV includes:
- Unique NFS server DNS name (file-sim-nas-{name}.file-simulator.svc.cluster.local)
- Labels: type=nfs, nas-role (input/backup/output), nas-server (unique identifier), environment=development
- 10Gi capacity with ReadWriteMany access mode
- Retain reclaim policy (prevents data loss on PVC deletion)
- Explicit NFSv3 mount options: tcp, hard, intr
- NFS path: /data (unfs3 export path from Phases 1-3)

**Server topology:**
- 3 input servers: nas-input-1, nas-input-2, nas-input-3
- 1 backup server: nas-backup (read-only in values-multi-nas.yaml)
- 3 output servers: nas-output-1, nas-output-2, nas-output-3

### PersistentVolumeClaim Manifests (7 files)

Each PVC includes:
- Unique selector.matchLabels.nas-server binding to corresponding PV
- Labels: nas-server (unique), nas-role (input/backup/output)
- ReadWriteMany access mode matching PV
- 10Gi storage request matching PV capacity
- No storageClassName (enables static binding via selector)
- Namespace: default (with instructions to customize)
- Header comments explaining purpose, binding, and deployment

### Validation Results

All manifests passed kubectl --dry-run=client validation:
- 7/7 PV manifests syntactically correct
- 7/7 PVC manifests syntactically correct
- All PVs use /data NFS path
- All PVs have Retain reclaim policy
- All PVCs have selector.matchLabels for static binding
- No storageClassName in any PVC

## Decisions Made

### 1. Static PV Provisioning (Not Dynamic)

**Decision:** Create static PV manifests pointing to pre-existing NFS servers

**Rationale:**
- Production OCP environments use static provisioning for pre-existing NAS infrastructure
- Dynamic provisioning applies to on-demand storage (cloud storage classes)
- File simulator NAS servers already exist (deployed via Helm in Phases 1-3)
- Static manifests provide explicit server mapping matching production topology

**Impact:** Applications use production-identical PV/PVC patterns; easier migration from dev to production

### 2. Label Selector Binding (Not StorageClass)

**Decision:** Use selector.matchLabels.nas-server for PVC-to-PV binding

**Rationale:**
- Kubernetes native NFS volume plugin doesn't require StorageClass
- Label selectors provide explicit binding (PVC binds to specific PV)
- Prevents accidental binding to wrong PV when multiple NFS PVs exist
- Matches production patterns where specific NAS devices are targeted

**Impact:** Reliable PVC binding; each PVC binds to exactly one intended PV

### 3. Retain Reclaim Policy

**Decision:** persistentVolumeReclaimPolicy: Retain on all PVs

**Rationale:**
- Prevents data loss if PVC is accidentally deleted
- Matches production behavior (NAS data persists independently)
- Requires manual cleanup (kubectl delete pv) for intentional removal
- Development data valuable (test files, outputs)

**Impact:** Data survives PVC deletion; manual PV cleanup required

### 4. Explicit NFS Mount Options

**Decision:** mountOptions: [nfsvers=3, tcp, hard, intr]

**Rationale:**
- Default NFS mount options vary by Kubernetes version and node OS
- Explicit options ensure consistent behavior across environments
- nfsvers=3: Matches unfs3 server (Phase 1)
- tcp: Reliable transport (not UDP)
- hard: Retry on NFS failure (not fail immediately)
- intr: Allow interrupt on hung mount (Ctrl+C works)

**Impact:** Consistent NFS behavior; prevents "stale file handle" errors

### 5. 10Gi Symbolic Capacity

**Decision:** capacity.storage: 10Gi for all PVs

**Rationale:**
- NFS doesn't enforce PV capacity limits (actual limit is NFS server disk)
- 10Gi reasonable default for development workloads
- Production capacity values unknown; symbolic value prevents confusion
- Documented in manifests that NFS doesn't enforce this limit

**Impact:** Clear capacity indication; actual limit is Windows C:\ drive

## Technical Implementation

### Directory Structure
```
helm-chart/file-simulator/examples/
├── pv/
│   ├── nas-input-1-pv.yaml
│   ├── nas-input-2-pv.yaml
│   ├── nas-input-3-pv.yaml
│   ├── nas-backup-pv.yaml
│   ├── nas-output-1-pv.yaml
│   ├── nas-output-2-pv.yaml
│   └── nas-output-3-pv.yaml
└── pvc/
    ├── nas-input-1-pvc.yaml
    ├── nas-input-2-pvc.yaml
    ├── nas-input-3-pvc.yaml
    ├── nas-backup-pvc.yaml
    ├── nas-output-1-pvc.yaml
    ├── nas-output-2-pvc.yaml
    └── nas-output-3-pvc.yaml
```

### Binding Mechanism

**PV Label:**
```yaml
metadata:
  name: nas-input-1-pv
  labels:
    nas-server: nas-input-1
```

**PVC Selector:**
```yaml
metadata:
  name: nas-input-1-pvc
spec:
  selector:
    matchLabels:
      nas-server: nas-input-1  # Binds to nas-input-1-pv
```

### Usage Pattern

**Apply PVs (cluster-scoped):**
```bash
kubectl apply -f helm-chart/file-simulator/examples/pv/
```

**Apply PVCs (namespace-scoped):**
```bash
kubectl apply -f helm-chart/file-simulator/examples/pvc/ -n <your-namespace>
```

**Mount in Deployment:**
```yaml
spec:
  volumes:
    - name: nas-input-1
      persistentVolumeClaim:
        claimName: nas-input-1-pvc
  containers:
    - volumeMounts:
        - name: nas-input-1
          mountPath: /mnt/input-1
```

## Deviations from Plan

None - plan executed exactly as written.

## Testing Performed

### Validation Tests

1. kubectl dry-run validation: All 14 manifests passed
2. NFS server DNS verification: All 7 unique server names present
3. Label selector verification: All PVCs have matching selector
4. StorageClassName absence: No PVC includes storageClassName
5. NFS path consistency: All PVs use /data path
6. Reclaim policy check: All PVs use Retain policy

### Expected Binding Behavior (Not Tested)

When PVs and PVCs are applied to a cluster with NAS servers running:
1. PVs enter Available state (no binding yet)
2. PVCs enter Pending state (searching for matching PV)
3. Kubernetes binds PVC to PV via nas-server label selector
4. Both PV and PVC enter Bound state
5. Pods can mount PVC and access NFS files

**Note:** Actual binding test deferred to Plan 04-03 (Integration Testing).

## Known Limitations

1. **Minikube IP in Comments:** Integration guide references will mention Minikube IP, which changes on restarts. Plan 04-02 addresses this with ConfigMap automation.

2. **No Subdirectory Examples:** Manifests mount /data root. Production uses subdirectories (/data/sub-1). Plan 04-03 will demonstrate subdirectory mounting.

3. **No Multi-Mount Example:** Single-NAS mounting shown. Plan 04-03 will demonstrate mounting 3+ NAS servers in one deployment.

4. **No Namespace Automation:** PVCs use default namespace. Users manually change namespace. Plan 04-03 documentation will explain namespace strategy.

## Next Phase Readiness

### Prerequisites for Phase 4 Plan 02 (ConfigMap & Service Discovery)

Ready. PV/PVC manifests complete; ConfigMap can reference:
- PVC names: nas-{name}-pvc
- NFS server DNS names: file-sim-nas-{name}.file-simulator.svc.cluster.local
- NodePort ports: 32150-32156

### Prerequisites for Phase 4 Plan 03 (Integration Examples)

Ready. Manifests available for:
- Multi-mount deployment examples
- Namespace deployment examples
- Subdirectory mount examples

### Blockers

None.

## Performance Metrics

- **Duration:** 4.2 minutes (start: 12:48:22, end: 12:52:31)
- **Tasks completed:** 3/3
- **Commits:** 3 (feat PV, feat PVC, test validation)
- **Files created:** 14 (7 PVs + 7 PVCs)

## Links

- **Plan:** `.planning/phases/04-configuration-templates/04-01-PLAN.md`
- **Research:** `.planning/phases/04-configuration-templates/04-RESEARCH.md`
- **values-multi-nas.yaml:** `helm-chart/file-simulator/values-multi-nas.yaml`
- **Previous phase:** `03-02-SUMMARY.md` (Bidirectional Sync Validation)
- **Next plan:** `04-02-PLAN.md` (ConfigMap & Service Discovery)

## Commit History

| Commit | Type | Description |
|--------|------|-------------|
| 8b9affc | feat | Create 7 PersistentVolume manifests for NAS servers |
| b375001 | feat | Create 7 PersistentVolumeClaim manifests for NAS servers |
| 087b8f9 | test | Validate PV/PVC manifest correctness |
