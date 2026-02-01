# Phase 3: Bidirectional Sync - Research

**Researched:** 2026-02-01
**Domain:** Kubernetes sidecar continuous synchronization patterns for bidirectional file sync
**Confidence:** HIGH

## Summary

This research investigates how to enable bidirectional file synchronization for output NAS servers (nas-output-1/2/3, nas-backup) so files written via NFS mount appear on Windows for tester retrieval. The existing architecture uses init container + unfs3 pattern which provides one-way sync (Windows -> NFS) at pod startup. Phase 3 requires adding continuous reverse sync (NFS -> Windows) via a sidecar container.

The standard approach is a **sidecar container running rsync in a continuous loop**, syncing from the emptyDir (NFS export) back to the hostPath (Windows directory). Key design decisions:

1. **Use interval-based rsync** (not inotify): Simple while-loop with configurable sleep interval (default 30s). inotify adds complexity and Alpine's inotify-tools support is limited for this use case.
2. **One-way sidecar sync direction**: NFS emptyDir -> Windows hostPath. The init container handles the reverse direction at pod start.
3. **Selective deployment**: Only output NAS servers (nas-output-1/2/3, nas-backup) get the sidecar; input NAS servers remain one-way sync only.
4. **Loop prevention via one-way design**: Each sync operation is strictly one-direction, preventing infinite sync loops.

The Kubernetes native sidecar feature (stable in v1.33, April 2025) is the recommended implementation mechanism, using `restartPolicy: Always` on an init container.

**Primary recommendation:** Add a sync-to-windows sidecar container to output NAS pods using Kubernetes native sidecar pattern (init container with restartPolicy: Always) running `rsync -av --delete /data/ /windows-mount/` in a configurable interval loop (default 30 seconds).

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| rsync | 3.x (Alpine apk) | File synchronization | Proven in Phase 1/2 init container; efficient delta sync, preserves permissions |
| Alpine Linux | latest | Sidecar container base | Already used in Phase 1/2; lightweight (5MB), includes rsync via apk |
| Kubernetes native sidecar | v1.29+ | Sidecar lifecycle management | Graduated stable in v1.33 (April 2025); proper startup/shutdown ordering |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| inotify-tools | 3.x | File change detection | Only if interval-based sync proves insufficient (v2 optimization) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| rsync loop | inotifywait + rsync | inotify is faster but adds complexity; not needed for 30s requirement |
| rsync loop | lsyncd | Heavier daemon; overkill for container-to-container sync |
| rsync loop | osync | Bidirectional tool; unnecessary since we have separate init + sidecar for each direction |
| rsync | rclone | rclone is for cloud storage; rsync is simpler for local filesystem sync |

**Installation:**
```dockerfile
# Sidecar container (same as existing init container)
FROM alpine:latest
RUN apk add --no-cache rsync
```

## Architecture Patterns

### Recommended Project Structure
```
helm-chart/file-simulator/
├── templates/
│   └── nas-multi.yaml      # Modified to add sidecar for output NAS
└── values-multi-nas.yaml   # Add sidecar configuration options
```

### Pattern 1: Kubernetes Native Sidecar for Continuous Sync
**What:** Use init container with `restartPolicy: Always` to create a sidecar that runs for the pod lifetime
**When to use:** Always for the sync sidecar - ensures proper startup ordering and graceful shutdown

**Example:**
```yaml
# Source: Kubernetes sidecar containers documentation
# https://kubernetes.io/docs/concepts/workloads/pods/sidecar-containers/
initContainers:
  # First: one-time init container (Windows -> emptyDir)
  - name: sync-windows-data
    image: alpine:latest
    # ... existing init container (unchanged)

  # Second: continuous sidecar (emptyDir -> Windows)
  - name: sync-to-windows
    image: alpine:latest
    restartPolicy: Always  # Makes this a sidecar
    command:
      - sh
      - -c
      - |
        apk add --no-cache rsync
        echo "Starting continuous sync sidecar (interval: ${SYNC_INTERVAL}s)"
        while true; do
          rsync -av --delete /nfs-data/ /windows-mount/
          sleep ${SYNC_INTERVAL:-30}
        done
    env:
      - name: SYNC_INTERVAL
        value: "{{ $nas.sidecar.syncInterval | default 30 }}"
    volumeMounts:
      - name: nfs-export
        mountPath: /nfs-data
        readOnly: true  # Sidecar only reads from NFS export
      - name: windows-data
        mountPath: /windows-mount
```

**Key details:**
- `restartPolicy: Always` makes init container a sidecar (runs entire pod lifetime)
- Sidecar starts AFTER regular init container completes (proper ordering)
- Sidecar stops AFTER main container terminates (graceful shutdown)
- readOnly mount on /nfs-data prevents sidecar from accidentally modifying NFS export

### Pattern 2: Selective Sidecar Deployment (Output NAS Only)
**What:** Only add sidecar to output NAS servers and nas-backup; input NAS servers remain unchanged
**When to use:** Always - input NAS servers don't need reverse sync (Windows is source of truth for inputs)

**Example:**
```yaml
# Source: Helm conditional logic for sidecar deployment
{{- if $nas.sidecar.enabled }}
  # Add sync-to-windows sidecar container
  - name: sync-to-windows
    restartPolicy: Always
    # ... sidecar configuration
{{- end }}
```

**Values configuration:**
```yaml
nasServers:
  - name: nas-input-1
    enabled: true
    sidecar:
      enabled: false  # No sidecar for input servers

  - name: nas-output-1
    enabled: true
    sidecar:
      enabled: true   # Sidecar for output servers
      syncInterval: 30
```

### Pattern 3: Loop Prevention via One-Way Design
**What:** Each sync operation is strictly one-direction; no bidirectional sync in single process
**When to use:** Always - prevents infinite sync loops that corrupt files

**Architecture:**
```
Windows hostPath                    emptyDir (NFS export)
     │                                     │
     │  ┌─────────────────────┐           │
     ├──│ Init Container      │───────────►  (pod start: Windows -> NFS)
     │  │ rsync Windows→NFS   │           │
     │  └─────────────────────┘           │
     │                                     │
     │  ┌─────────────────────┐           │
     ◄──│ Sidecar Container   │───────────┤  (continuous: NFS -> Windows)
     │  │ rsync NFS→Windows   │           │
     │  └─────────────────────┘           │
     │                                     │
     │  ┌─────────────────────┐           │
     │  │ Main Container      │───────────┤  (NFS server: serves emptyDir)
     │  │ unfs3 NFS server    │           │
     │  └─────────────────────┘           │
```

**Why this prevents loops:**
1. Init container runs ONCE at pod start (Windows -> NFS)
2. Sidecar runs continuously (NFS -> Windows only)
3. Init container does NOT run again after sidecar starts
4. No process reads AND writes both directions simultaneously
5. Next pod restart: init container syncs Windows -> NFS (captures any Windows changes made while pod was down)

### Pattern 4: Configurable Sync Interval via Helm Values
**What:** Allow sync interval to be configured per NAS server in values.yaml
**When to use:** Always - requirements specify "Bidirectional sync interval configurable via Helm values (default 30 seconds)"

**Example:**
```yaml
# values-multi-nas.yaml
nasServers:
  - name: nas-output-1
    sidecar:
      enabled: true
      syncInterval: 30        # Sync every 30 seconds (default)
      image:
        repository: alpine
        tag: latest
      resources:
        requests:
          memory: "32Mi"
          cpu: "25m"
        limits:
          memory: "64Mi"
          cpu: "100m"
```

### Pattern 5: emptyDir sizeLimit for Disk Protection
**What:** Add sizeLimit to emptyDir volumes to prevent pod eviction from disk exhaustion
**When to use:** Always - Kubernetes best practice for emptyDir volumes

**Example:**
```yaml
# Source: Kubernetes local storage capacity isolation (GA in v1.25)
# https://kubernetes.io/blog/2022/09/19/local-storage-capacity-isolation-ga/
volumes:
  - name: nfs-export
    emptyDir:
      sizeLimit: 500Mi  # Prevent disk exhaustion; pod evicted if exceeded
```

**Key details:**
- If emptyDir usage exceeds sizeLimit, pod is evicted (not the node)
- Protects against runaway file writes filling node disk
- Set based on expected data volume (500Mi reasonable for typical test files)

### Anti-Patterns to Avoid
- **Using osync or bidirectional sync tool:** Creates complexity; separate init + sidecar is simpler and prevents loops
- **Using inotify for <60s requirement:** 30s requirement doesn't justify inotify complexity
- **Running sidecar as regular container:** Will start BEFORE init container completes, causing race conditions
- **Not using restartPolicy: Always:** Container will terminate after first command, not run continuously
- **Sharing volume as read-write in sidecar:** Sidecar should NOT write to NFS export (only reads to sync)
- **Syncing in both directions from single container:** Creates sync loops and file corruption
- **Skipping sizeLimit on emptyDir:** Risk of disk exhaustion and node-wide impact

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Continuous sync | Custom file watcher daemon | rsync in while loop | rsync handles all edge cases; while loop is simpler than inotify |
| Sidecar lifecycle | Custom process manager | Kubernetes native sidecar (restartPolicy: Always) | K8s handles startup ordering, restart, graceful shutdown |
| Delta file transfer | Full file copy on each sync | rsync -av | rsync only transfers changed bytes; efficient for large files |
| File permission preservation | Custom chmod/chown | rsync -a (archive mode) | Archive mode preserves all metadata automatically |
| Bidirectional sync | osync, bsync, lsyncd | Separate init + sidecar | Simpler architecture, no loop detection needed |
| Sync loop prevention | Lock files, temp dirs | One-way sync design | Architecture prevents loops; no runtime detection needed |

**Key insight:** The combination of Kubernetes native sidecar + rsync while loop is the standard pattern for continuous file sync in containers. It's simpler and more reliable than event-driven alternatives for intervals >= 30 seconds.

## Common Pitfalls

### Pitfall 1: Sidecar Starts Before Init Container Completes
**What goes wrong:** Sidecar starts syncing empty NFS export to Windows, deleting existing Windows files
**Why it happens:** Using regular container instead of init container with restartPolicy: Always
**How to avoid:** Use Kubernetes native sidecar pattern (init container with restartPolicy: Always). Sidecar starts AFTER regular init containers complete.
**Warning signs:**
- Windows test files disappear after pod deployment
- Sidecar logs show "0 files synced" on first run
- NFS export empty despite Windows directory having files

### Pitfall 2: Sync Loop Creating Infinite Cycles
**What goes wrong:** Files keep syncing back and forth, timestamps change, rsync never settles
**Why it happens:** Using bidirectional sync tool or syncing both directions from same process
**How to avoid:** Separate one-way syncs: init container (Windows -> NFS), sidecar (NFS -> Windows). Never sync both directions in same process.
**Warning signs:**
- rsync runs continuously without sleeping
- File timestamps constantly updating
- CPU usage spikes in sync containers
- Files corrupted or truncated

### Pitfall 3: Sidecar Writes to NFS Export
**What goes wrong:** Sidecar accidentally creates files in /data, which then sync to Windows, then init re-syncs on restart
**Why it happens:** Volume mount without readOnly flag; sidecar command creates temp files
**How to avoid:** Mount NFS export as readOnly in sidecar: `readOnly: true`. Sidecar only reads from /nfs-data.
**Warning signs:**
- Unexpected files in NFS export
- Files appear on Windows that weren't written via NFS
- File permission conflicts between sidecar and NFS server

### Pitfall 4: rsync --delete Removes Windows Files During Init
**What goes wrong:** Init container's rsync --delete removes files from NFS export that were written via NFS since last pod start
**Why it happens:** rsync --delete removes files not in source; NFS-written files aren't in Windows source
**How to avoid:** For output NAS, init container should NOT use --delete flag (or use conditional: only --delete on first run)
**Warning signs:**
- Files written via NFS disappear on pod restart
- Sidecar synced files to Windows, but they're gone after restart
- Data loss after pod restart

### Pitfall 5: High Sync Frequency Causing CPU/IO Contention
**What goes wrong:** rsync running every 5 seconds causes I/O contention with NFS server
**Why it happens:** Over-aggressive sync interval; rsync scans all files on each run
**How to avoid:** Use reasonable interval (30s default). For large directories, consider rsync --checksum sparingly (disk I/O intensive).
**Warning signs:**
- NFS server response time increases
- Pod CPU usage higher than expected
- Disk I/O warnings in kubelet logs

### Pitfall 6: emptyDir Fills Disk Causing Node Problems
**What goes wrong:** Large files written via NFS fill emptyDir, exhausting node disk space
**Why it happens:** No sizeLimit set on emptyDir; Kubernetes defaults to unlimited
**How to avoid:** Always set sizeLimit on emptyDir: `sizeLimit: 500Mi`. Pod evicted if exceeded, protecting node.
**Warning signs:**
- DiskPressure condition on node
- Multiple pods evicted unexpectedly
- Node becomes unschedulable

### Pitfall 7: Windows File Permissions Lost on Sync
**What goes wrong:** Files synced to Windows have wrong ownership or permissions
**Why it happens:** Windows/Linux permission model mismatch; rsync -a preserves Linux permissions
**How to avoid:** Use rsync with --no-owner --no-group if ownership causes issues: `rsync -av --no-owner --no-group`. Test file access on Windows after sync.
**Warning signs:**
- "Access denied" when opening synced files on Windows
- Files show "unknown owner" in Windows
- Tester cannot modify synced files

## Code Examples

Verified patterns from official sources:

### Complete Output NAS Pod Spec with Sidecar
```yaml
# Source: Kubernetes sidecar containers documentation
# https://kubernetes.io/docs/concepts/workloads/pods/sidecar-containers/
# Phase 3 pattern: init container + sidecar + main container
apiVersion: apps/v1
kind: Deployment
metadata:
  name: file-sim-nas-output-1
spec:
  replicas: 1
  template:
    spec:
      initContainers:
        # First: One-time init container (Windows -> NFS)
        # NOTE: For output NAS, consider removing --delete to preserve NFS-written files
        - name: sync-windows-data
          image: alpine:latest
          command:
            - sh
            - -c
            - |
              set -e
              apk add --no-cache rsync
              mkdir -p /nfs-data
              # For output NAS: don't use --delete to preserve NFS-written files
              rsync -av /windows-mount/ /nfs-data/
              echo "Init sync complete"
          volumeMounts:
            - name: windows-data
              mountPath: /windows-mount
              readOnly: true
            - name: nfs-export
              mountPath: /nfs-data
          securityContext:
            runAsNonRoot: false
            allowPrivilegeEscalation: false

        # Second: Continuous sidecar (NFS -> Windows)
        - name: sync-to-windows
          image: alpine:latest
          restartPolicy: Always  # Makes this a sidecar
          command:
            - sh
            - -c
            - |
              set -e
              apk add --no-cache rsync
              INTERVAL=${SYNC_INTERVAL:-30}
              echo "=== Sync Sidecar Starting ==="
              echo "Sync interval: ${INTERVAL}s"
              echo "Direction: NFS -> Windows"

              while true; do
                SYNC_START=$(date +%s)
                rsync -av --delete /nfs-data/ /windows-mount/
                SYNC_END=$(date +%s)
                SYNC_DURATION=$((SYNC_END - SYNC_START))
                echo "[$(date +%H:%M:%S)] Synced in ${SYNC_DURATION}s"
                sleep ${INTERVAL}
              done
          env:
            - name: SYNC_INTERVAL
              value: "30"
          volumeMounts:
            - name: nfs-export
              mountPath: /nfs-data
              readOnly: true  # Sidecar only reads from NFS export
            - name: windows-data
              mountPath: /windows-mount
          securityContext:
            runAsNonRoot: false
            allowPrivilegeEscalation: false
          resources:
            requests:
              memory: "32Mi"
              cpu: "25m"
            limits:
              memory: "64Mi"
              cpu: "100m"

      containers:
        # Main container: unfs3 NFS server (unchanged from Phase 2)
        - name: nfs-server
          image: alpine:latest
          command:
            - sh
            - -c
            - |
              apk add --no-cache unfs3
              echo '/data 0.0.0.0/0(rw,sync,no_root_squash)' > /etc/exports
              unfsd -d -p -t -n 2049 -e /etc/exports
          ports:
            - name: nfs
              containerPort: 2049
              protocol: TCP
          volumeMounts:
            - name: nfs-export
              mountPath: /data
          securityContext:
            capabilities:
              add: [NET_BIND_SERVICE]
              drop: [ALL]
          resources:
            requests:
              memory: "64Mi"
              cpu: "50m"
            limits:
              memory: "256Mi"
              cpu: "200m"

      volumes:
        - name: windows-data
          hostPath:
            path: /mnt/simulator-data/nas-output-1
            type: DirectoryOrCreate
        - name: nfs-export
          emptyDir:
            sizeLimit: 500Mi  # Best practice: always set sizeLimit
```

### Helm Template Conditional for Sidecar
```yaml
# Source: Helm templating best practices
# Add sidecar only when $nas.sidecar.enabled is true

initContainers:
  # Always include: one-time init container
  - name: sync-windows-data
    # ... existing init container

{{- if $nas.sidecar.enabled }}
  # Conditional: continuous sidecar for output NAS servers
  - name: sync-to-windows
    image: {{ $nas.sidecar.image.repository }}:{{ $nas.sidecar.image.tag }}
    restartPolicy: Always
    command:
      - sh
      - -c
      - |
        apk add --no-cache rsync
        while true; do
          rsync -av --delete /nfs-data/ /windows-mount/
          sleep {{ $nas.sidecar.syncInterval | default 30 }}
        done
    volumeMounts:
      - name: nfs-export
        mountPath: /nfs-data
        readOnly: true
      - name: windows-data
        mountPath: /windows-mount
    resources:
      {{- toYaml $nas.sidecar.resources | nindent 6 }}
{{- end }}
```

### values-multi-nas.yaml Sidecar Configuration
```yaml
# Source: Phase 3 values configuration pattern
nasServers:
  # INPUT SERVERS - No sidecar (Windows is source of truth)
  - name: nas-input-1
    enabled: true
    sidecar:
      enabled: false  # No reverse sync needed
    # ... rest of config unchanged

  - name: nas-input-2
    enabled: true
    sidecar:
      enabled: false
    # ...

  - name: nas-input-3
    enabled: true
    sidecar:
      enabled: false
    # ...

  # BACKUP SERVER - Sidecar enabled (backup files sync to Windows)
  - name: nas-backup
    enabled: true
    sidecar:
      enabled: true
      syncInterval: 30
      image:
        repository: alpine
        tag: latest
        pullPolicy: IfNotPresent
      resources:
        requests:
          memory: "32Mi"
          cpu: "25m"
        limits:
          memory: "64Mi"
          cpu: "100m"
    # ... rest of config

  # OUTPUT SERVERS - Sidecar enabled (system outputs sync to Windows)
  - name: nas-output-1
    enabled: true
    sidecar:
      enabled: true
      syncInterval: 30
      image:
        repository: alpine
        tag: latest
        pullPolicy: IfNotPresent
      resources:
        requests:
          memory: "32Mi"
          cpu: "25m"
        limits:
          memory: "64Mi"
          cpu: "100m"
    # ...

  - name: nas-output-2
    enabled: true
    sidecar:
      enabled: true
      syncInterval: 30
    # ...

  - name: nas-output-3
    enabled: true
    sidecar:
      enabled: true
      syncInterval: 30
    # ...
```

### Test Script for Bidirectional Sync Validation
```powershell
# Source: Phase 3 validation pattern (extends test-multi-nas.ps1)
# Test WIN-02: Files placed in Windows visible via NFS within 30 seconds
# Test WIN-03: Files written via NFS appear in Windows within 60 seconds

$context = "file-simulator"
$namespace = "file-simulator"

# Test 1: WIN-03 - Write via NFS, verify on Windows
function Test-NFSToWindows {
    param([string]$nasName)

    $testFile = "nfs-test-$(Get-Date -Format 'yyyyMMddHHmmss').txt"
    $testContent = "Written via NFS at $(Get-Date)"

    # Write file via kubectl exec to NFS export
    $pod = kubectl --context=$context get pod -n $namespace `
        -l "app.kubernetes.io/component=$nasName" `
        -o jsonpath='{.items[0].metadata.name}'

    kubectl --context=$context exec -n $namespace $pod -c nfs-server -- `
        sh -c "echo '$testContent' > /data/$testFile"

    # Wait up to 60 seconds for file to appear on Windows
    $windowsPath = "C:\simulator-data\$nasName\$testFile"
    $timeout = 60
    $elapsed = 0

    while ($elapsed -lt $timeout) {
        if (Test-Path $windowsPath) {
            $windowsContent = Get-Content $windowsPath -Raw
            if ($windowsContent -match $testContent) {
                Write-Host "PASS: File synced to Windows in ${elapsed}s"
                return $true
            }
        }
        Start-Sleep -Seconds 5
        $elapsed += 5
    }

    Write-Host "FAIL: File not synced to Windows within ${timeout}s"
    return $false
}

# Test 2: WIN-02 - Write to Windows, verify via NFS within 30 seconds
function Test-WindowsToNFS {
    param([string]$nasName)

    $testFile = "windows-test-$(Get-Date -Format 'yyyyMMddHHmmss').txt"
    $testContent = "Written on Windows at $(Get-Date)"

    # Write file to Windows directory
    $windowsPath = "C:\simulator-data\$nasName\$testFile"
    Set-Content -Path $windowsPath -Value $testContent

    # Wait up to 30 seconds for file to appear via NFS
    # Note: Requires sidecar to sync Windows -> NFS direction
    # WIN-02 requires continuous sidecar for this direction too
    $pod = kubectl --context=$context get pod -n $namespace `
        -l "app.kubernetes.io/component=$nasName" `
        -o jsonpath='{.items[0].metadata.name}'

    # ... validation logic
}

# Run tests on output NAS servers
foreach ($nas in @("nas-output-1", "nas-output-2", "nas-output-3", "nas-backup")) {
    Write-Host "Testing $nas..."
    Test-NFSToWindows -nasName $nas
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Regular container sidecar | Kubernetes native sidecar (restartPolicy: Always) | K8s v1.29 beta, v1.33 stable (April 2025) | Proper startup/shutdown ordering without custom logic |
| inotify-based sync | Interval-based rsync loop | N/A (both valid) | inotify faster but complex; interval simpler for >= 30s requirements |
| osync/bsync bidirectional | Separate init + sidecar | N/A | Simpler architecture, inherent loop prevention |
| emptyDir without sizeLimit | emptyDir with sizeLimit | K8s v1.25 GA (Sept 2022) | Prevents disk exhaustion and node-wide impact |
| Manual sidecar lifecycle | Native sidecar termination | K8s v1.33 (April 2025) | Sidecars properly terminate after main container |

**Deprecated/outdated:**
- **Regular containers for sidecars:** Use native sidecar pattern (restartPolicy: Always) for proper lifecycle
- **Privileged containers for file sync:** rsync needs no special privileges; avoid privileged mode
- **Unbounded emptyDir:** Always set sizeLimit to prevent pod/node disk issues

## Open Questions

Things that couldn't be fully resolved:

1. **Should init container use --delete for output NAS servers?**
   - What we know: --delete removes files not in source; for output NAS, files written via NFS won't be in Windows source
   - What's unclear: Whether testers expect NFS-written files to persist across pod restarts
   - Recommendation: For output NAS, remove --delete from init container OR make it configurable. Files written via NFS would survive restart if --delete is removed.

2. **Does WIN-02 require bidirectional sidecar (both directions continuous)?**
   - What we know: WIN-02 states "Files placed in Windows directory visible via NFS mount within 30 seconds"
   - What's unclear: Does "within 30 seconds" mean continuous monitoring, or just that eventual sync is fast enough?
   - Recommendation: Current design has init container for Windows->NFS at pod start. If WIN-02 requires continuous Windows->NFS sync (testers add files while pod running), need second sidecar direction. Clarify requirement before implementation.

3. **Resource impact of 4 sidecars running continuously?**
   - What we know: 4 output NAS pods with sidecars: 4 x (32Mi request) = 128Mi additional
   - What's unclear: Real-world CPU usage during idle vs active sync
   - Recommendation: Monitor with `kubectl top pods` after deployment; adjust resources if needed

4. **File locking during rsync sync?**
   - What we know: rsync handles partial file transfers gracefully
   - What's unclear: Behavior when NFS client is actively writing during sync
   - Recommendation: rsync's atomic replacement should handle this; test with concurrent writes in validation

5. **Kubernetes version compatibility for native sidecar?**
   - What we know: restartPolicy: Always on init container requires K8s v1.29+
   - What's unclear: What version Minikube runs; whether feature gate enabled
   - Recommendation: Check `minikube kubectl version` during deployment; fall back to regular sidecar container if needed

## Sources

### Primary (HIGH confidence)
- [Kubernetes Sidecar Containers Documentation](https://kubernetes.io/docs/concepts/workloads/pods/sidecar-containers/) - Native sidecar pattern, lifecycle, configuration
- [Kubernetes Local Storage Capacity Isolation](https://kubernetes.io/blog/2022/09/19/local-storage-capacity-isolation-ga/) - emptyDir sizeLimit best practices
- [rsync man page](https://man7.org/linux/man-pages/man1/rsync.1.html) - Archive mode, --delete, --checksum options

### Secondary (MEDIUM confidence)
- [GitHub: toelke/docker-rsync](https://github.com/toelke/docker-rsync) - Distroless rsync sidecar pattern for Kubernetes
- [GitHub: deajan/osync](https://github.com/deajan/osync) - Bidirectional sync concepts, loop prevention via state tracking
- [GitHub: dooblem/bsync](https://github.com/dooblem/bsync) - Snapshot-based sync state, conflict detection patterns
- [Edstem: Kubernetes Sidecar Pattern for Data Sync](https://www.edstem.com/blog/leveraging-kubernetes-sidecar/) - emptyDir shared volume patterns
- [Spacelift: Kubernetes Sidecar Best Practices](https://spacelift.io/blog/kubernetes-sidecar-container) - Sidecar design patterns

### Tertiary (LOW confidence - general patterns)
- [Baeldung: Continuously Sync Files One-Way on Linux](https://www.baeldung.com/linux/sync-files-continuously-one-way) - inotifywait + rsync patterns
- [Medium: Avoiding EmptyDir in Kubernetes](https://medium.com/@pankajaswal888/avoiding-emptydir-in-kubernetes-best-practices-with-examples-including-sizelimit-f7f2f98b7e64) - sizeLimit eviction behavior

## Metadata

**Confidence breakdown:**
- Standard stack (rsync, native sidecar): HIGH - Official K8s docs, proven rsync
- Architecture (separate init + sidecar): HIGH - Inherent loop prevention, clear data flow
- Pitfalls (loop, permissions, sizeLimit): HIGH - Based on K8s docs and rsync behavior
- WIN-02 interpretation: MEDIUM - Requirement may need clarification for continuous Windows->NFS
- Resource estimates: MEDIUM - Based on Phase 2 measurements, may vary with actual usage

**Research date:** 2026-02-01
**Valid until:** 2026-03-15 (45 days) - Kubernetes sidecar pattern stable; rsync long-term stable

**Notes:**
- Phase 3 builds directly on Phase 1/2 validated patterns
- Main addition is sidecar container; no new dependencies
- Kubernetes native sidecar (v1.33 stable) is recommended but may require version check
- Loop prevention is architectural (separate directions) not runtime detection
- emptyDir sizeLimit is new best practice addition from Phase 3 research
