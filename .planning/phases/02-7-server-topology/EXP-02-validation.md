# EXP-02: Runtime Subdirectory Creation Validation

**Date:** 2026-01-29
**Phase:** 02-7-server-topology
**Plan:** 02-03

## Test Objective

Verify that subdirectories created at runtime via kubectl exec persist across pod restarts, or document the actual behavior.

## Test Procedure

### 1. Runtime Directory Creation

Created a directory and file inside a running nas-input-1 pod:

```bash
POD="file-sim-file-simulator-nas-input-1-7585bc76b5-dp7sx"
kubectl exec -n file-simulator $POD -- sh -c 'mkdir /data/runtime-dir && echo "Created at runtime" > /data/runtime-dir/runtime-file.txt'
```

Result: **SUCCESS** - Directory and file created successfully.

### 2. Verification Before Restart

```bash
kubectl exec -n file-simulator $POD -- ls //data//runtime-dir//
# Output: runtime-file.txt
```

Result: **SUCCESS** - File visible immediately after creation.

### 3. Pod Restart Test

```bash
kubectl delete pod -n file-simulator -l app.kubernetes.io/component=nas-input-1
kubectl wait --for=condition=Ready pod -l app.kubernetes.io/component=nas-input-1 -n file-simulator --timeout=60s
```

### 4. Check After Restart

```bash
POD=$(kubectl get pod -n file-simulator -l app.kubernetes.io/component=nas-input-1 -o jsonpath='{.items[0].metadata.name}')
kubectl exec -n file-simulator $POD -- ls //data//
# Output: README.txt, isolation-test-nas-input-1.txt, sub-1
# runtime-dir: NOT PRESENT
```

Result: **Runtime-created directory LOST on restart** (expected behavior).

### 5. Windows-Created Directory Test

Created directory on Windows host:

```powershell
New-Item -ItemType Directory -Force -Path "C:\simulator-data\nas-input-1\persistent-subdir"
Set-Content -Path "C:\simulator-data\nas-input-1\persistent-subdir\persisted.txt" -Value "This will persist"
```

Restarted pod and verified:

```bash
kubectl exec -n file-simulator $POD -- cat //data//persistent-subdir//persisted.txt
# Output: This will persist
```

Result: **SUCCESS** - Windows-created directories persist across pod restarts.

## Findings

### EXP-02 Behavior Documentation

**Current Architecture (Phase 2 - Input NAS):**

1. **Runtime-created directories in pod:** LOST on restart
   - Reason: Init container re-syncs from Windows hostPath to emptyDir
   - Init container overwrites entire /data volume on each pod start
   - This is expected and intentional for input NAS servers

2. **Windows-created directories:** PERSIST across restarts
   - Reason: Init container syncs from Windows hostPath
   - Windows filesystem is source of truth
   - All files/directories created on Windows side are synced to pod

### Production Implications

**For Input NAS (current Phase 2 topology):**
- Testers MUST create directories on Windows side (C:\simulator-data\nas-input-X\)
- Microservices reading from input NAS can rely on Windows-created structure
- Runtime modifications inside pods are ephemeral (lost on restart)

**For Output NAS (planned Phase 3):**
- Will need bidirectional sync pattern (sidecar or different approach)
- Microservice-generated files must persist even if Windows files are updated
- Two-way sync will preserve runtime-created directories

### Conclusion

EXP-02 validated: The system correctly implements the Phase 2 pattern where Windows directories are the source of truth for input NAS servers. Runtime modifications are intentionally ephemeral.

**Status:** ✅ VALIDATED - Behavior matches architectural design for Phase 2
