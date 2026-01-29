# Technology Stack - Multi-NAS Simulation

**Project:** File Simulator Suite - Multi-NAS Capability
**Researched:** 2026-01-29
**Confidence:** HIGH

## Executive Summary

The core challenge is **Linux NFS servers cannot export directories mounted from Windows via CIFS/9p** due to kernel limitations. This is a well-documented restriction that affects both traditional NFS servers (nfsd) and userspace alternatives. The current erichough/nfs-server crashes when attempting to export Minikube's 9p-mounted hostPath from `C:\simulator-data`.

**Recommended Solution:** Init container sync pattern with userspace NFS server. Use unfs3 instead of kernel nfsd to avoid privilege requirements, combined with init containers that copy Windows data into local emptyDir volumes before NFS export.

## The Core Problem

### Kernel Limitation (CONFIRMED)

Linux kernel NFS cannot reliably export mounted network filesystems:
- **CIFS mounts:** Explicitly unsupported by kernel nfsd and NFS Ganesha FSAL_VFS
- **9p mounts:** Minikube's default mount type, same limitations as CIFS
- **NFS re-export:** Only experimental support in kernel 5.10+, unstable and unsupported

**Technical reason:** The `encode_fh` filesystem function required by nfsd is not implemented for network filesystems. Attempting to export results in "does not support NFS export" errors.

**Sources:**
- [SUSE: exportfs error - does not support NFS export](https://www.suse.com/support/kb/doc/?id=000021721)
- [Linux Kernel: CONFIG_CIFS_NFSD_EXPORT (experimental)](https://cateee.net/lkddb/web-lkddb/CIFS_NFSD_EXPORT.html)
- [NFS Ganesha FSAL_VFS documentation](https://github.com/nfs-ganesha/nfs-ganesha/wiki/VFS)

### Minikube Mount Characteristics

Minikube uses **9p filesystem** (9p2000.L) for Windows directory mounts:
- No native CIFS support in Minikube
- 9p mounts suffer from same export limitations as CIFS
- Performance issues with >600 files
- Reliability concerns reported by community

**Source:** [Minikube mount documentation](https://minikube.sigs.k8s.io/docs/handbook/mount/)

## Recommended Stack

### Core Components

| Component | Technology | Version | Purpose | Why |
|-----------|-----------|---------|---------|-----|
| **NFS Server** | unfs3 | latest | Userspace NFSv3 server | No privileged mode required, avoids kernel limitations, works in containers |
| **Sync Mechanism** | Init Container + rsync | 3.2+ | Copy Windows data to local FS | Bypasses export limitation, ensures data locality |
| **Multi-Instance Pattern** | StatefulSet | K8s 1.14+ | Deploy 7 independent NAS servers | Stable identities, independent volumes |
| **Storage Backend** | emptyDir | - | NFS export source | Local filesystem (ext4), exportable by NFS |
| **Data Source** | hostPath (read-only) | - | Windows mount point | Single source of truth for test data |

### Architecture Pattern

```
Windows Host (C:\simulator-data\nas-input-1\)
    ↓ (Minikube 9p mount)
hostPath: /mnt/simulator-data/nas-input-1 (READ-ONLY)
    ↓ (Init container rsync)
emptyDir: /data (LOCAL FILESYSTEM)
    ↓ (unfs3 export)
NFS: nas-input-1.file-simulator.svc:2049:/data
```

**Key insight:** We copy FROM the problematic mount TO a local filesystem, THEN export the local filesystem via NFS.

## Solution Design

### Option A: Init Container Sync Pattern (RECOMMENDED)

**How it works:**
1. Init container runs rsync to copy hostPath → emptyDir
2. Main container (unfs3) exports emptyDir via NFS
3. Optional sidecar for continuous sync (if bidirectional needed)

**Advantages:**
- ✅ Works around kernel limitation completely
- ✅ No privileged mode needed (unfs3)
- ✅ Files on Windows immediately available (rsync interval)
- ✅ Each NAS independent (StatefulSet pattern)
- ✅ Standard Kubernetes primitives (no custom operators)

**Disadvantages:**
- ❌ Data duplication (hostPath + emptyDir)
- ❌ Sync delay (typically 5-30 seconds)
- ❌ Additional memory usage per NAS pod

**When to use:** **Recommended for this project** - balances complexity, reliability, and Windows integration requirement.

### Option B: Bindfs FUSE Layer (EXPERIMENTAL)

**How it works:**
1. Mount hostPath with bindfs to create FUSE layer
2. Export bindfs mount via unfs3

**Advantages:**
- ✅ No data duplication
- ✅ Near real-time synchronization

**Disadvantages:**
- ❌ Requires FUSE support in container (security implications)
- ❌ Additional layer of complexity
- ❌ bindfs doesn't work well with NFS crossmnt option
- ❌ Stability concerns with FUSE in containers

**When to use:** Only if sync delay is absolutely unacceptable and security team approves FUSE.

**Source:** [bindfs over NFS limitations](https://github.com/mpartel/bindfs/issues/78)

### Option C: NFS Ganesha with FSAL_VFS (NOT VIABLE)

**How it works:**
1. Use NFS Ganesha instead of kernel nfsd
2. Export mounted filesystem via FSAL_VFS

**Why not viable:**
- ❌ FSAL_VFS explicitly does NOT support NFS/CIFS mounted filesystems
- ❌ Requires CAP_DAC_READ_SEARCH (privileged container)
- ❌ Project still in alpha/experimental state

**Source:** [NFS Ganesha FSAL_VFS documentation](https://github.com/nfs-ganesha/nfs-ganesha/wiki/VFS)

### Option D: Keep Current emptyDir-only (STATUS QUO)

**Current state:** NFS exports emptyDir with no Windows visibility

**Disadvantages:**
- ❌ **Breaks core requirement:** Windows testers cannot supply input files
- ❌ Violates project purpose (dev identical to production with Windows integration)

**When to use:** Never - defeats entire purpose of multi-NAS milestone.

## Multi-Instance Deployment Pattern

### StatefulSet Architecture

Deploy 7 independent NFS servers using StatefulSet:

```yaml
StatefulSet: nas-servers
  Replicas: 7
  Pods: nas-servers-0, nas-servers-1, ..., nas-servers-6

  Each pod gets:
    - Unique name: nas-input-1, nas-input-2, nas-input-3, nas-backup, nas-output-1, nas-output-2, nas-output-3
    - Init container: rsync from hostPath subdirectory
    - Main container: unfs3 exporting emptyDir
    - Service: nas-input-1.file-simulator.svc:2049
```

**Naming pattern:**
- `nas-servers-0` → Service `nas-input-1`
- `nas-servers-1` → Service `nas-input-2`
- `nas-servers-2` → Service `nas-input-3`
- `nas-servers-3` → Service `nas-backup`
- `nas-servers-4` → Service `nas-output-1`
- `nas-servers-5` → Service `nas-output-2`
- `nas-servers-6` → Service `nas-output-3`

**Key features:**
- Stable pod identities for debugging
- Independent volume lifecycle
- Ordered startup/shutdown
- Individual service per NAS

**Source:** [Kubernetes StatefulSets](https://kubernetes.io/docs/concepts/workloads/controllers/statefulset/)

### Alternative: Separate Deployments

Create 7 separate Deployment resources instead of one StatefulSet.

**Advantages:**
- Simpler to understand
- Independent scaling (not needed here)
- Easier Helm templating with loop

**Disadvantages:**
- More YAML to maintain
- No guaranteed ordering
- Loses StatefulSet benefits

**Recommendation:** Use StatefulSet for cleaner architecture, but Deployments acceptable if Helm templating is easier.

## Detailed Implementation

### Userspace NFS Server: unfs3

**Container image:** `nimbix/docker-unfs3:latest` or `macadmins/unfs3:latest`

**Why unfs3 over erichough/nfs-server:**

| Feature | unfs3 | erichough/nfs-server |
|---------|-------|----------------------|
| **Privilege mode** | NOT required ✅ | Required (CAP_SYS_ADMIN) |
| **Security** | Userspace only | Needs privileged container |
| **Kernel modules** | NOT required ✅ | Requires nfs/nfsd modules |
| **File locking** | No (acceptable for simulator) | Yes |
| **Container-friendly** | Designed for containers ✅ | Designed for VMs |
| **Maturity** | Stable (NFSv3 spec) | Active but complex |

**Configuration:**
```dockerfile
ENTRYPOINT ["/usr/sbin/unfsd", "-d", "-e", "/exports.txt"]
```

**Export file (`/exports.txt`):**
```
/data *(rw,no_root_squash,sync,no_subtree_check)
```

**Sources:**
- [unfs3 GitHub](https://github.com/unfs3/unfs3)
- [Docker unfs3 advantages](https://github.com/nimbix/docker-unfs3)

### Init Container: Rsync

**Image:** `alpine:3.19` with rsync installed

**Purpose:** Copy Windows-mounted hostPath to local emptyDir before NFS export

**Script pattern:**
```bash
#!/bin/sh
# Install rsync if not present
apk add --no-cache rsync

# Initial sync from Windows mount to local filesystem
rsync -av --delete /mnt/source/ /data/

echo "Initial sync complete: $(ls -la /data | wc -l) items"
```

**Volume mounts:**
- Source: `hostPath:/mnt/simulator-data/nas-input-1` (read-only)
- Destination: `emptyDir:/data` (read-write)

**Sync behavior:**
- Init container: One-time sync on pod startup
- Optional sidecar: Continuous sync every 30s (add if bidirectional needed)

**Memory considerations:**
- emptyDir backed by node disk (not RAM)
- Each NAS pod duplicates its subset of data
- For 1GB per NAS × 7 = 7GB total additional storage

### Continuous Sync Sidecar (Optional)

Only add if output directories (nas-output-1/2/3) need to sync back to Windows:

**Container:** Alpine + rsync + inotify-tools

**Loop:**
```bash
while true; do
    rsync -av --delete /data/ /mnt/destination/
    sleep 30
done
```

**Direction:**
- **Input NAS (1-3):** Windows → emptyDir (one-way, init container only)
- **Output NAS (1-3):** emptyDir → Windows (bidirectional, needs sidecar)
- **Backup NAS:** emptyDir → Windows (bidirectional, needs sidecar)

**Note:** inotify does NOT work over 9p/CIFS mounts, so polling (sleep 30) is required.

**Source:** [inotify limitations with network filesystems](https://lwn.net/Articles/896055/)

## Storage Architecture

### Volume Types

```yaml
# Per-NAS pod volumes:
volumes:
  # Windows source (problematic to export directly)
  - name: source-data
    hostPath:
      path: /mnt/simulator-data/nas-input-1
      type: Directory

  # Local copy (exportable via NFS)
  - name: nfs-data
    emptyDir:
      sizeLimit: 2Gi  # Per-NAS limit

  # NFS export configuration
  - name: exports-config
    configMap:
      name: nfs-exports
```

### Subdirectory Structure

Each NAS exports `/data` with subdirectories:

```
Windows: C:\simulator-data\nas-input-1\
  ├── sub-1\
  ├── sub-2\
  └── sub-3\

hostPath: /mnt/simulator-data/nas-input-1/
  ├── sub-1/
  ├── sub-2/
  └── sub-3/

emptyDir: /data/  (exported via NFS)
  ├── sub-1/
  ├── sub-2/
  └── sub-3/

NFS mount: nas-input-1:/data/sub-1
```

**Client mount example:**
```yaml
volumes:
  - name: app-input
    nfs:
      server: nas-input-1.file-simulator.svc.cluster.local
      path: /data/sub-1
```

## Kubernetes Configuration

### StatefulSet Example (Condensed)

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: nas-servers
spec:
  serviceName: nas
  replicas: 7
  template:
    spec:
      initContainers:
        - name: sync-data
          image: alpine:3.19
          command: ['/scripts/sync.sh']
          volumeMounts:
            - name: source-data
              mountPath: /mnt/source
              readOnly: true
            - name: nfs-data
              mountPath: /data
      containers:
        - name: nfs-server
          image: nimbix/docker-unfs3:latest
          ports:
            - containerPort: 2049
          volumeMounts:
            - name: nfs-data
              mountPath: /data
            - name: exports-config
              mountPath: /etc/exports.txt
              subPath: exports.txt
```

### Service Per NAS

Each NAS needs a dedicated Service for stable DNS:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: nas-input-1
spec:
  selector:
    statefulset.kubernetes.io/pod-name: nas-servers-0
  ports:
    - port: 2049
      targetPort: 2049
      name: nfs
  type: ClusterIP
```

**DNS resolution:**
- `nas-input-1.file-simulator.svc.cluster.local:2049`
- `nas-output-2.file-simulator.svc.cluster.local:2049`

## Resource Requirements

### Per-NAS Pod

```yaml
resources:
  requests:
    memory: "128Mi"
    cpu: "50m"
  limits:
    memory: "512Mi"
    cpu: "200m"
```

**Total for 7 NAS pods:**
- Memory requests: 896Mi (~0.9GB)
- Memory limits: 3.5Gi (~3.5GB)
- CPU requests: 350m
- CPU limits: 1.4 CPU

**Existing cluster capacity (from CLAUDE.md):**
- Memory: 8GB total, 706Mi used by other protocols → **Sufficient**
- CPU: 4 CPUs, 575m used by other protocols → **Sufficient**

### emptyDir Storage

```yaml
emptyDir:
  sizeLimit: 2Gi  # Per-NAS limit
```

**Total storage:** 7 NAS × 2Gi = 14GB

**Minikube disk:** 20GB configured → **Sufficient** (6GB remaining for OS/containers)

## Alternative Technologies Considered

### NFS Ganesha + External Provisioner

**Official Helm chart:** `nfs-ganesha-server-and-external-provisioner`

**Why not used:**
- ❌ Designed for dynamic PVC provisioning (not our use case)
- ❌ Requires backing volume with "supported filesystem" (hostPath with 9p NOT supported)
- ❌ Project status: "alpha/experimental"
- ❌ Overkill for static NAS simulation

**Source:** [kubernetes-sigs/nfs-ganesha-server-and-external-provisioner](https://github.com/kubernetes-sigs/nfs-ganesha-server-and-external-provisioner)

### Lsyncd for Realtime Sync

**What it is:** Live syncing daemon using inotify + rsync

**Why not used:**
- ❌ inotify does NOT work over 9p/CIFS mounts (source filesystem limitation)
- ❌ Would need to run inside container watching emptyDir (inverted direction)
- ❌ Adds complexity without benefit (30s polling is acceptable)

**Source:** [Lsyncd GitHub](https://github.com/lsyncd/lsyncd)

### Unison for Bidirectional Sync

**What it is:** Two-way file synchronization tool

**Why not used:**
- ❌ Designed for conflict resolution between peers (not master-replica pattern)
- ❌ Requires running on both Windows and Linux (complicated setup)
- ❌ Overkill for one-way sync with occasional output

**Source:** [Unison on GitHub](https://github.com/bcpierce00/unison)

### OverlayFS for Copy-on-Write

**What it is:** Union mount filesystem with copy-on-write

**Why not used:**
- ❌ Cannot layer over 9p/CIFS mount (lower layer limitation)
- ❌ NFS export requires `nfs_export=on` mount option (experimental)
- ❌ Requires `CONFIG_OVERLAY_FS_NFS_EXPORT` kernel config (may not be enabled)

**Source:** [Linux Kernel OverlayFS documentation](https://docs.kernel.org/filesystems/overlayfs.html)

## Migration from Current Setup

### Current State (Single NAS with emptyDir)

```yaml
# Current nas.yaml (line 92-95)
volumes:
  - name: data
    persistentVolumeClaim:
      claimName: file-sim-file-simulator-pvc
```

**Current workaround (nfs-fix-patch.yaml):**
```yaml
volumes:
  - name: nfs-data
    emptyDir: {}
  - name: shared-data
    persistentVolumeClaim:
      claimName: file-sim-file-simulator-pvc
```

**Problem:** NFS exports emptyDir (isolated), shared-data not used by NFS.

### Proposed Multi-NAS Configuration

**Replace:** Single Deployment `nas.yaml`
**With:** StatefulSet `nas-multi.yaml` with 7 replicas

**Key changes:**
1. StatefulSet instead of Deployment
2. Init container for hostPath → emptyDir sync
3. Per-pod subdirectory from `C:\simulator-data\nas-{name}\`
4. 7 independent Services (nas-input-1, nas-input-2, etc.)
5. Use unfs3 instead of erichough/nfs-server

**Backward compatibility:**
- Keep existing single-NAS deployment as `nas.yaml` (disabled by default)
- New multi-NAS as `nas-multi.yaml` (enabled by `nas.multiInstance.enabled`)

## Validation Checklist

### Functional Requirements

- [ ] Windows file written to `C:\simulator-data\nas-input-1\sub-1\test.txt`
- [ ] Init container syncs file to pod emptyDir within 10 seconds
- [ ] NFS client can mount `nas-input-1:/data/sub-1`
- [ ] NFS client can read `test.txt` contents
- [ ] All 7 NAS servers accessible via separate DNS names
- [ ] Output NAS (nas-output-1) can write files visible on Windows

### Performance Requirements

- [ ] Init sync completes in <30 seconds for 1000 files
- [ ] NFS read performance acceptable (>10 MB/s)
- [ ] Pod restart re-syncs from Windows correctly

### Security Requirements

- [ ] unfs3 runs in non-privileged mode (no CAP_SYS_ADMIN)
- [ ] hostPath mounted read-only (no accidental writes from NAS pod)
- [ ] emptyDir isolated per pod (no cross-NAS contamination)

## Installation Instructions

### Helm Values Configuration

```yaml
nas:
  # Disable single-NAS deployment
  enabled: false

  multiInstance:
    enabled: true
    servers:
      - name: nas-input-1
        hostPath: /mnt/simulator-data/nas-input-1
        nodePort: 32049
      - name: nas-input-2
        hostPath: /mnt/simulator-data/nas-input-2
        nodePort: 32050
      - name: nas-input-3
        hostPath: /mnt/simulator-data/nas-input-3
        nodePort: 32051
      - name: nas-backup
        hostPath: /mnt/simulator-data/nas-backup
        nodePort: 32052
      - name: nas-output-1
        hostPath: /mnt/simulator-data/nas-output-1
        nodePort: 32053
        bidirectional: true  # Enable sidecar sync
      - name: nas-output-2
        hostPath: /mnt/simulator-data/nas-output-2
        nodePort: 32054
        bidirectional: true
      - name: nas-output-3
        hostPath: /mnt/simulator-data/nas-output-3
        nodePort: 32055
        bidirectional: true

    image:
      repository: nimbix/docker-unfs3
      tag: latest
      pullPolicy: IfNotPresent

    syncImage:
      repository: alpine
      tag: "3.19"

    resources:
      requests:
        memory: 128Mi
        cpu: 50m
      limits:
        memory: 512Mi
        cpu: 200m

    emptyDirSizeLimit: 2Gi
```

### Windows Directory Preparation

```powershell
# Create directory structure
$nasNames = @(
    "nas-input-1", "nas-input-2", "nas-input-3",
    "nas-backup",
    "nas-output-1", "nas-output-2", "nas-output-3"
)

foreach ($nas in $nasNames) {
    $basePath = "C:\simulator-data\$nas"
    New-Item -ItemType Directory -Force -Path "$basePath\sub-1"
    New-Item -ItemType Directory -Force -Path "$basePath\sub-2"
    New-Item -ItemType Directory -Force -Path "$basePath\sub-3"

    # Create test file
    "Test data for $nas" | Out-File "$basePath\sub-1\README.txt"
}
```

### Deployment Commands

```powershell
# Deploy with multi-NAS configuration
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --kube-context=file-simulator `
    --namespace file-simulator `
    --create-namespace `
    --values ./helm-chart/file-simulator/values-multi-nas.yaml

# Verify all NAS pods running
kubectl --context=file-simulator get pods -n file-simulator -l app.kubernetes.io/component=nas-multi

# Check NAS services
kubectl --context=file-simulator get svc -n file-simulator | Select-String "nas-"

# Test NFS mount from client pod
kubectl --context=file-simulator run -it --rm nfs-test `
    --image=alpine:3.19 `
    --restart=Never `
    -- sh -c "apk add nfs-utils && mkdir /mnt/test && mount -t nfs nas-input-1.file-simulator.svc.cluster.local:/data /mnt/test && ls -la /mnt/test/sub-1"
```

## Sources

### Primary Sources (HIGH Confidence)

- [SUSE Support: exportfs error - does not support NFS export](https://www.suse.com/support/kb/doc/?id=000021721)
- [NFS Ganesha FSAL_VFS documentation](https://github.com/nfs-ganesha/nfs-ganesha/wiki/VFS)
- [Docker NFS Server (erichough) requirements](https://github.com/ehough/docker-nfs-server)
- [unfs3 GitHub repository](https://github.com/unfs3/unfs3)
- [Kubernetes StatefulSets documentation](https://kubernetes.io/docs/concepts/workloads/controllers/statefulset/)
- [Minikube mount documentation](https://minikube.sigs.k8s.io/docs/handbook/mount/)

### Secondary Sources (MEDIUM Confidence)

- [Linux Kernel: CONFIG_CIFS_NFSD_EXPORT](https://cateee.net/lkddb/web-lkddb/CIFS_NFSD_EXPORT.html)
- [LWN: Change notifications for network filesystems](https://lwn.net/Articles/896055/)
- [Kubernetes NFS Ganesha External Provisioner](https://github.com/kubernetes-sigs/nfs-ganesha-server-and-external-provisioner)
- [Docker unfs3 advantages explanation](https://github.com/nimbix/docker-unfs3)
- [Kubernetes Init Containers documentation](https://kubernetes.io/docs/concepts/workloads/pods/init-containers/)
- [Linux Kernel OverlayFS NFS export](https://docs.kernel.org/filesystems/overlayfs.html)

### Community Sources (LOW Confidence - Informational)

- [Bindfs over NFS limitations](https://github.com/mpartel/bindfs/issues/78)
- [Lsyncd for live sync](https://github.com/lsyncd/lsyncd)
- [Unison file synchronizer](https://github.com/bcpierce00/unison)

---

## Confidence Assessment

| Area | Confidence | Rationale |
|------|------------|-----------|
| **Core Problem** | HIGH | Multiple authoritative sources confirm CIFS/9p export limitation |
| **unfs3 Solution** | HIGH | Official project, used in production by multiple organizations |
| **Init Container Pattern** | HIGH | Standard Kubernetes pattern, well-documented |
| **StatefulSet Deployment** | HIGH | Official Kubernetes primitive, extensively used for stateful workloads |
| **Sync Performance** | MEDIUM | Theoretical, needs validation with actual file counts |
| **Resource Estimates** | MEDIUM | Based on similar workloads, may need tuning |
| **Bidirectional Sync** | MEDIUM | Polling-based sync has inherent delay, acceptable for simulator use case |

---

## Next Steps

1. **Create Helm template:** `nas-multi.yaml` with StatefulSet and per-pod Services
2. **Create sync script:** Init container script for rsync with error handling
3. **Create values schema:** `values-multi-nas.yaml` with 7-server configuration
4. **Test in isolation:** Deploy single NAS with unfs3 + init container, verify Windows visibility
5. **Scale to 7:** Deploy full StatefulSet, verify all Services accessible
6. **Integration test:** Mount from client pod, read/write operations, Windows visibility
7. **Performance test:** Measure sync time with 1000+ files, adjust polling interval if needed
8. **Document limitations:** Sync delay (30s), storage overhead (2Gi × 7), no file locking

**Critical success criteria:** Windows tester writes file, it appears in microservice NFS mount within 60 seconds.
