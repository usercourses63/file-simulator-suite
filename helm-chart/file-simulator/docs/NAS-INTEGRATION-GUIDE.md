# NAS Integration Guide

**Version:** 1.0
**Last Updated:** 2026-02-01
**Audience:** Developers integrating microservices with File Simulator Suite NAS infrastructure

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Quick Start](#quick-start)
4. [Configuration Templates Reference](#configuration-templates-reference)
5. [Production OCP Pattern Replication](#production-ocp-pattern-replication)
6. [Multi-NFS Mount Patterns](#multi-nfs-mount-patterns)
7. [Windows Directory Setup](#windows-directory-setup)
8. [Troubleshooting](#troubleshooting)
9. [Reference](#reference)

---

## Overview

### What the NAS Infrastructure Provides

The File Simulator Suite delivers a **7-server NAS infrastructure** that replicates production OpenShift Container Platform (OCP) multi-device storage topologies in local Kubernetes (Minikube) development environments.

**Key capabilities:**

- **7 independent NFS servers** (unfs3 userspace NFS)
  - 3 input servers: `nas-input-1`, `nas-input-2`, `nas-input-3`
  - 1 backup server: `nas-backup` (read-only configuration available)
  - 3 output servers: `nas-output-1`, `nas-output-2`, `nas-output-3`

- **Windows-backed storage** via Minikube mount
  - Test files created at `C:\simulator-data\nas-*\` visible in NFS exports
  - Bidirectional sync (Windows → NFS via init container, NFS → Windows via sidecar)
  - 15-30 second sync latency (well under 60s requirement)

- **Production-identical PV/PVC patterns**
  - Static provisioning (not dynamic)
  - Label-based binding (explicit PVC-to-PV mapping)
  - Retain reclaim policy (data persists beyond PVC lifetime)
  - NFSv3 with explicit mount options

- **Service discovery**
  - DNS-based cluster-internal access: `file-sim-nas-{name}.file-simulator.svc.cluster.local`
  - NodePort external access for Windows NFS clients (ports 32150-32156)
  - ConfigMap with all connection metadata

### How It Replicates Production OCP Patterns

The simulator matches production environments in these critical ways:

| Production Pattern | Simulator Implementation | Dev/Prod Parity |
|-------------------|-------------------------|-----------------|
| Multiple physical NAS devices | 7 independent NFS server pods | ✅ Topology matches |
| Static PV provisioning | Static PV YAML pointing to pre-existing NFS servers | ✅ Configuration identical |
| Pre-existing storage infrastructure | NAS servers deployed before applications | ✅ Deployment order matches |
| DNS-based service discovery | Kubernetes Services with stable DNS names | ✅ Resolution pattern identical |
| ReadWriteMany access mode | NFSv3 with multi-mount support | ✅ Concurrency semantics match |
| Retain reclaim policy | PVs configured with Retain | ✅ Data persistence behavior matches |

**What differs from production:**
- Underlying storage: Production uses physical NAS hardware; dev uses Windows directories
- Network: Production uses dedicated storage network; dev uses Minikube virtual network
- Scale: Production has TBs of capacity; dev has GB-scale for testing

**Why this matters:** Kubernetes manifests (PV/PVC YAML) work identically in both environments. Migration from dev to production requires only changing NFS server addresses.

### When to Use PV/PVC vs Direct NFS Mount

| Scenario | Recommended Approach | Rationale |
|----------|---------------------|-----------|
| Application pods need file access | **PV/PVC** | Kubernetes-native, portable, works in production |
| External tools (Windows, CI/CD) need file access | **Direct NFS mount via NodePort** | Bypasses K8s, uses standard NFS client |
| Programmatic NFS operations (custom logic) | **ConfigMap + NFS library** | Application controls mount/unmount |
| Testing NFS behavior | **Direct mount** | Isolates NFS layer from K8s abstractions |
| Production deployment | **Always PV/PVC** | Standard practice in OCP environments |

**General rule:** Use PV/PVC for application pods; use direct NFS for external access.

---

## Architecture

### NAS Server Topology

```
┌────────────────────────────────────────────────────────────────────────┐
│                        file-simulator namespace                         │
├────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Input Servers (3)          Backup Server (1)       Output Servers (3)  │
│  ┌──────────────────┐      ┌──────────────────┐   ┌──────────────────┐ │
│  │  nas-input-1     │      │   nas-backup     │   │  nas-output-1    │ │
│  │  Port: 2049      │      │   Port: 2049     │   │  Port: 2049      │ │
│  │  NodePort: 32150 │      │   NodePort: 32153│   │  NodePort: 32154 │ │
│  │  Export: /data   │      │   Export: /data  │   │  Export: /data   │ │
│  └────────┬─────────┘      └────────┬─────────┘   └────────┬─────────┘ │
│           │                         │                      │            │
│  ┌────────┴─────────┐      ┌────────┴─────────┐   ┌────────┴─────────┐ │
│  │  nas-input-2     │      │  (read-only in   │   │  nas-output-2    │ │
│  │  Port: 2049      │      │   production)    │   │  Port: 2049      │ │
│  │  NodePort: 32151 │      │                  │   │  NodePort: 32155 │ │
│  │  Export: /data   │      │                  │   │  Export: /data   │ │
│  └────────┬─────────┘      └──────────────────┘   └────────┬─────────┘ │
│           │                                                 │            │
│  ┌────────┴─────────┐                             ┌────────┴─────────┐ │
│  │  nas-input-3     │                             │  nas-output-3    │ │
│  │  Port: 2049      │                             │  Port: 2049      │ │
│  │  NodePort: 32152 │                             │  NodePort: 32156 │ │
│  │  Export: /data   │                             │  Export: /data   │ │
│  └──────────────────┘                             └──────────────────┘ │
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │              Kubernetes Services (ClusterIP + NodePort)         │   │
│  │  DNS: file-sim-nas-{name}.file-simulator.svc.cluster.local     │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
                        ┌───────────────────────┐
                        │   Windows Host        │
                        │   C:\simulator-data\  │
                        │   - nas-input-1/      │
                        │   - nas-input-2/      │
                        │   - nas-input-3/      │
                        │   - nas-backup/       │
                        │   - nas-output-1/     │
                        │   - nas-output-2/     │
                        │   - nas-output-3/     │
                        └───────────────────────┘
                                    ▲
                        Minikube mount (hostPath)
```

### Role of Each Server Type

**Input Servers (nas-input-1/2/3):**
- **Purpose:** Receive files for processing (ingestion, validation, transformation)
- **Direction:** Windows → NFS (one-way sync via init container)
- **Use case:** Place test files on Windows; applications read from NFS mount
- **Sync mechanism:** Init container runs rsync on pod start
- **Sync trigger:** Manual pod restart required after adding files on Windows

**Backup Server (nas-backup):**
- **Purpose:** Archive/backup storage (read-only in production configuration)
- **Direction:** Varies by configuration (can be bidirectional or read-only)
- **Use case:** Long-term retention, disaster recovery testing
- **Configuration note:** `values-multi-nas.yaml` supports `exportOptions: "ro"` for read-only

**Output Servers (nas-output-1/2/3):**
- **Purpose:** Store processed results (reports, transformed files, exports)
- **Direction:** NFS → Windows (bidirectional with sidecar sync)
- **Use case:** Applications write to NFS; testers retrieve from Windows
- **Sync mechanism:** Native sidecar container with inotify-based rsync
- **Sync latency:** 15-30 seconds (validated in Phase 3 testing)

### How Windows Directories Map to NFS Exports

Each NAS server pod follows this pattern:

1. **Windows filesystem:** `C:\simulator-data\nas-{name}\` (created by `setup-windows.ps1`)
2. **Minikube mount:** Windows directory mounted at `/mnt/simulator-data/nas-{name}` in Minikube node
3. **Init container:** Copies `/mnt/simulator-data/nas-{name}` → `/data` (emptyDir volume)
4. **NFS export:** unfs3 server exports `/data` via NFS protocol
5. **Kubernetes PV:** Points to NFS server DNS name and `/data` path
6. **Application PVC:** Binds to PV, mounts into pod

**Key insight:** Windows hostPath cannot be directly exported by NFS (kernel limitation). The init container pattern bridges this gap by copying Windows files into an emptyDir that unfs3 can export.

---

## Quick Start

### Minimal Steps to Mount a Single NAS Server

This example mounts only `nas-input-1` to get started quickly.

#### 1. Verify NAS Infrastructure Running

```bash
kubectl --context=file-simulator get pods -n file-simulator | grep nas-input-1

# Expected: file-sim-nas-input-1-xxxxxxxxxx-xxxxx   1/1     Running
```

#### 2. Create Test File on Windows

```powershell
# Create test file
"Hello from Windows" | Out-File C:\simulator-data\nas-input-1\test.txt

# Verify file exists
Get-Content C:\simulator-data\nas-input-1\test.txt
```

#### 3. Restart NAS Pod to Sync Files

```bash
# Restart pod to trigger init container sync
kubectl --context=file-simulator delete pod -n file-simulator -l app.kubernetes.io/name=nas,nas-server=nas-input-1

# Wait for pod to restart
kubectl --context=file-simulator wait --for=condition=Ready pod -n file-simulator -l nas-server=nas-input-1 --timeout=60s
```

#### 4. Apply PersistentVolume

```bash
cd helm-chart/file-simulator/examples

kubectl apply -f pv/nas-input-1-pv.yaml

# Verify: kubectl get pv nas-input-1-pv
```

#### 5. Apply PersistentVolumeClaim

```bash
kubectl apply -f pvc/nas-input-1-pvc.yaml -n default

# Verify binding: kubectl get pvc nas-input-1-pvc -n default
# Expected: STATUS=Bound, VOLUME=nas-input-1-pv
```

#### 6. Mount in Your Application

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: test-app
  namespace: default
spec:
  containers:
    - name: app
      image: busybox:latest
      command: ["sh", "-c", "cat /mnt/nas/test.txt && sleep 3600"]
      volumeMounts:
        - name: nas-input-1
          mountPath: /mnt/nas
  volumes:
    - name: nas-input-1
      persistentVolumeClaim:
        claimName: nas-input-1-pvc
```

```bash
kubectl apply -f test-app.yaml -n default

# Check output
kubectl logs test-app -n default
# Expected: "Hello from Windows"
```

### Full Multi-Mount Example

For complete multi-NAS mounting (6 servers), see:
- **Example deployment:** `examples/deployments/multi-mount-example.yaml`
- **Step-by-step guide:** `examples/deployments/README.md`

---

## Configuration Templates Reference

### PersistentVolumes

**Location:** `helm-chart/file-simulator/examples/pv/`

**What Each PV Provides:**

Each PV is a cluster-scoped resource that represents one NAS server:

| PV Name | NAS Server | Role | DNS Name | NodePort |
|---------|-----------|------|----------|----------|
| `nas-input-1-pv` | nas-input-1 | Input | `file-sim-nas-input-1.file-simulator.svc.cluster.local` | 32150 |
| `nas-input-2-pv` | nas-input-2 | Input | `file-sim-nas-input-2.file-simulator.svc.cluster.local` | 32151 |
| `nas-input-3-pv` | nas-input-3 | Input | `file-sim-nas-input-3.file-simulator.svc.cluster.local` | 32152 |
| `nas-backup-pv` | nas-backup | Backup | `file-sim-nas-backup.file-simulator.svc.cluster.local` | 32153 |
| `nas-output-1-pv` | nas-output-1 | Output | `file-sim-nas-output-1.file-simulator.svc.cluster.local` | 32154 |
| `nas-output-2-pv` | nas-output-2 | Output | `file-sim-nas-output-2.file-simulator.svc.cluster.local` | 32155 |
| `nas-output-3-pv` | nas-output-3 | Output | `file-sim-nas-output-3.file-simulator.svc.cluster.local` | 32156 |

**Label Schema Explanation:**

```yaml
metadata:
  labels:
    type: nfs                  # Volume type (for filtering: kubectl get pv -l type=nfs)
    nas-role: input            # Server role: input|backup|output
    nas-server: nas-input-1    # Unique server identifier (used for PVC binding)
    environment: development   # Environment marker
```

**Purpose of labels:**
- `type=nfs`: Filter all NFS PVs: `kubectl get pv -l type=nfs`
- `nas-role`: Filter by role: `kubectl get pv -l nas-role=input`
- `nas-server`: **Critical for PVC binding** - PVC selector must match this label
- `environment`: Distinguish dev/staging/prod PVs

**How to Customize Capacity:**

```yaml
spec:
  capacity:
    storage: 10Gi  # Change this value
```

**Important:** NFS doesn't enforce capacity limits. The actual limit is the Windows C:\ drive. The capacity value is symbolic for Kubernetes resource accounting.

**Reclaim Policy:**

```yaml
spec:
  persistentVolumeReclaimPolicy: Retain  # Do not change!
```

**Retain** means:
- PV survives PVC deletion
- Data remains on NFS server
- Manual cleanup required: `kubectl delete pv nas-input-1-pv`

**Why Retain:** Matches production behavior where NAS storage persists independently of Kubernetes.

**Mount Options:**

```yaml
spec:
  mountOptions:
    - nfsvers=3  # NFSv3 protocol (matches unfs3 server)
    - tcp        # Use TCP transport (not UDP)
    - hard       # Retry indefinitely on NFS failure (not soft)
    - intr       # Allow interrupt (Ctrl+C works)
```

These options ensure consistent behavior across Kubernetes versions and prevent "stale file handle" errors.

### PersistentVolumeClaims

**Location:** `helm-chart/file-simulator/examples/pvc/`

**Namespace Considerations:**

PVCs are **namespace-scoped**. Key points:

- PVs are cluster-wide (no namespace)
- PVCs must be in the same namespace as consuming pods
- Example PVCs use `namespace: default`
- **You must change the namespace** or apply to your app's namespace:

```bash
# Option 1: Edit YAML to change namespace
kubectl apply -f pvc/nas-input-1-pvc.yaml -n my-app-namespace

# Option 2: Use kustomize to override namespace
kustomize build . | kubectl apply -f -
```

**Selector Binding Mechanism:**

```yaml
spec:
  selector:
    matchLabels:
      nas-server: nas-input-1  # Must match PV label exactly
```

**How binding works:**
1. PVC created with selector `nas-server: nas-input-1`
2. Kubernetes searches for PVs with matching label
3. PV `nas-input-1-pv` has label `nas-server: nas-input-1`
4. Kubernetes binds PVC to PV (both show STATUS=Bound)

**Why selectors matter:**
- Without selector: PVC might bind to wrong PV (first available with matching capacity)
- With selector: Explicit binding to specific NAS server

**Storage Request:**

```yaml
spec:
  resources:
    requests:
      storage: 10Gi  # Must be <= PV capacity
```

**Important:** PVC request must not exceed PV capacity. If PV is 10Gi, PVC request can be 5Gi or 10Gi, but not 15Gi.

**Access Mode:**

```yaml
spec:
  accessModes:
    - ReadWriteMany  # Required for NFS
```

**ReadWriteMany (RWX)** means:
- Multiple pods can mount the PVC simultaneously
- All pods can read and write
- NFS protocol supports this natively

**Do not use:** `ReadWriteOnce` (RWO) - wastes NFS capability and prevents multi-pod mounting.

**No StorageClassName:**

```yaml
spec:
  # storageClassName field is ABSENT (not empty string, not null)
```

**Absence of `storageClassName`** enables static binding via label selectors. If present, Kubernetes expects dynamic provisioning.

### ConfigMap

**Location:** `helm-chart/file-simulator/examples/configmap/`

**Environment Variable Naming Convention:**

```
NAS_{SERVER}_{FIELD}
```

Examples:
- `NAS_INPUT_1_HOST`: DNS name for nas-input-1
- `NAS_INPUT_1_PORT`: NFS port (2049)
- `NAS_INPUT_1_PATH`: Export path (/data)
- `NAS_INPUT_1_NODEPORT`: External NodePort (32150)
- `NAS_INPUT_1_PVC`: PVC name (nas-input-1-pvc)

**Full list of fields:**
- `_HOST`: Cluster-internal DNS name
- `_PORT`: NFS port (always 2049)
- `_PATH`: Export path (always /data)
- `_NODEPORT`: External access port (32150-32156)
- `_PVC`: PVC name for reference

**How to Substitute MINIKUBE_IP:**

The ConfigMap includes a placeholder `<minikube-ip>` that must be replaced before applying:

**Bash:**
```bash
export MINIKUBE_IP=$(minikube ip -p file-simulator)
sed -i "s/<minikube-ip>/$MINIKUBE_IP/g" configmap/nas-endpoints-configmap.yaml
```

**PowerShell:**
```powershell
$MINIKUBE_IP = minikube ip -p file-simulator
(Get-Content configmap\nas-endpoints-configmap.yaml) -replace '<minikube-ip>', $MINIKUBE_IP | Set-Content configmap\nas-endpoints-configmap.yaml
```

**Why substitution needed:** Minikube IP changes on profile restart. Hardcoding would break after Minikube operations.

**Usage with envFrom:**

```yaml
spec:
  containers:
    - name: app
      envFrom:
        - configMapRef:
            name: file-simulator-nas-endpoints
```

All ConfigMap key-value pairs become environment variables in the container.

**Programmatic access example:**

```python
import os

# Access NAS endpoints from environment
nas_host = os.getenv("NAS_INPUT_1_HOST")
nas_port = os.getenv("NAS_INPUT_1_PORT")
nas_path = os.getenv("NAS_INPUT_1_PATH")

# Construct NFS URL
nfs_url = f"nfs://{nas_host}:{nas_port}{nas_path}"
```

### Example Deployment

**Location:** `helm-chart/file-simulator/examples/deployments/`

**What the Example Demonstrates:**

1. **Multi-mount pattern:** 6 NAS servers mounted in one pod
2. **Volume declaration:** PVC references in `spec.volumes`
3. **Volume mounting:** Mount paths in `spec.containers.volumeMounts`
4. **ConfigMap injection:** Environment variables via `envFrom`
5. **Verification script:** Listing all mounts and writing test files

**How to Adapt for Production:**

1. **Replace container image:**
   ```yaml
   containers:
     - name: app
       image: your-registry.com/your-app:v1.0  # Replace busybox
   ```

2. **Adjust mount paths:**
   ```yaml
   volumeMounts:
     - name: nas-input-1
       mountPath: /app/data/input-1  # Match your app's expected paths
   ```

3. **Remove unnecessary mounts:**
   If your app only needs 2 NAS servers, delete the other 4 from `volumes` and `volumeMounts`.

4. **Add resource limits:**
   ```yaml
   resources:
     requests:
       cpu: 100m
       memory: 256Mi
     limits:
       cpu: 500m
       memory: 512Mi
   ```

5. **Configure readOnly mounts:**
   ```yaml
   volumeMounts:
     - name: nas-input-1
       mountPath: /app/data/input-1
       readOnly: true  # Prevent accidental writes to input server
   ```

---

## Production OCP Pattern Replication

### Why Static Provisioning Matches OCP

**Production OCP environments typically:**

1. **Pre-provision storage:** NAS devices exist before application deployment
2. **Use static PVs:** Admins create PV YAML pointing to existing NAS servers
3. **Namespace isolation:** Each team/project gets PVCs in their namespace
4. **Explicit binding:** PVCs use selectors to bind to specific PVs (not first-available)

**Dynamic provisioning (StorageClass) applies when:**
- Storage is provisioned on-demand (cloud storage, CSI drivers)
- Infrastructure doesn't pre-exist
- Automated lifecycle management needed

**File Simulator Suite uses static provisioning because:**
- NAS servers deployed before applications (matches production)
- Explicit topology (7 specific servers, not dynamic pool)
- Configuration parity with OCP environments

### How Labels Enable Reliable Binding

**Problem without labels:**
- Multiple PVs with same capacity exist
- PVC binds to first available PV
- May bind to wrong NAS server

**Solution with label selectors:**

```yaml
# PV labels
metadata:
  name: nas-input-1-pv
  labels:
    nas-server: nas-input-1
---
# PVC selector
spec:
  selector:
    matchLabels:
      nas-server: nas-input-1  # Explicit binding
```

**Result:** PVC only binds to PV with matching `nas-server` label. Guaranteed correct server.

**Production benefit:** Teams can create PVCs knowing exactly which NAS device they'll get.

### Reclaim Policy Considerations

| Policy | Behavior on PVC Deletion | When to Use |
|--------|------------------------|-------------|
| **Retain** | PV remains; data preserved; manual cleanup | Production (data valuable) |
| **Delete** | PV deleted; data lost | Dynamic provisioning only |
| **Recycle** | PV scrubbed; data erased; PV Available | Deprecated (don't use) |

**File Simulator Suite uses Retain:**
- Matches production NAS behavior (storage persists)
- Prevents accidental data loss
- Requires manual PV cleanup: `kubectl delete pv nas-input-1-pv`

**Production pattern:**
```bash
# Application team deletes PVC (done with project)
kubectl delete pvc nas-input-1-pvc -n my-app-namespace

# PV remains in Released state
kubectl get pv nas-input-1-pv
# STATUS: Released (not Available, not Bound)

# Admin reviews data, then manually removes PV
kubectl delete pv nas-input-1-pv
```

### Capacity Planning Notes

**In production:**
- NAS capacity determined by physical hardware (TBs)
- PV capacity should match actual NAS device capacity
- Kubernetes doesn't enforce NFS capacity limits (relies on NFS server)

**In File Simulator Suite:**
- Capacity limited by Windows C:\ drive (GBs)
- PV capacity set to 10Gi (symbolic, not enforced)
- Actual limit: Windows filesystem

**Best practice:** Use realistic capacity values in PVs even though NFS doesn't enforce limits. Helps Kubernetes resource accounting.

---

## Multi-NFS Mount Patterns

### Mounting Multiple NAS Servers Simultaneously

Production applications often need 3+ NAS servers:
- Input server for raw files
- Working server for intermediate processing
- Output server for results

**Pattern:**

```yaml
spec:
  volumes:
    - name: nas-input-1
      persistentVolumeClaim:
        claimName: nas-input-1-pvc
    - name: nas-output-1
      persistentVolumeClaim:
        claimName: nas-output-1-pvc
    - name: nas-output-2
      persistentVolumeClaim:
        claimName: nas-output-2-pvc
  containers:
    - name: app
      volumeMounts:
        - name: nas-input-1
          mountPath: /mnt/input
        - name: nas-output-1
          mountPath: /mnt/output/primary
        - name: nas-output-2
          mountPath: /mnt/output/secondary
```

**Key points:**
- Each PVC becomes a separate volume
- Each volume gets unique name and mount path
- No limit on number of NFS mounts per pod

### Mount Path Conventions

**Recommended structure:**

```
/mnt/input-1    # Input NAS server 1
/mnt/input-2    # Input NAS server 2
/mnt/output-1   # Output NAS server 1
/mnt/output-2   # Output NAS server 2
/mnt/backup     # Backup NAS server
```

**Alternative (application-specific):**

```
/app/data/source        # Input
/app/data/destination   # Output
/app/data/archive       # Backup
```

**Avoid:**
- Overlapping mount paths: `/mnt/nas` and `/mnt/nas/subdir` (conflict)
- Root mounts: `/data` (risky, prefer subdirectory)

### readOnly Flag Usage

**When to use readOnly:**

```yaml
volumeMounts:
  - name: nas-input-1
    mountPath: /mnt/input
    readOnly: true  # Prevent accidental writes
```

**Use cases:**
- Input servers (read-only access pattern)
- Backup servers (prevent modification)
- Reference data (lookup tables, configs)

**When NOT to use readOnly:**
- Output servers (need write access)
- Working directories (intermediate files)
- Bidirectional workflows

**Enforcement:**
- Kubernetes enforces readOnly at container level
- Attempts to write fail with "Read-only file system" error

### Subdirectory Mounts

**Pattern:**

```yaml
volumeMounts:
  - name: nas-input-1
    mountPath: /mnt/input
    subPath: project-a  # Mounts /data/project-a, not /data root
```

**Use cases:**
- Multi-tenant NAS server (isolate projects)
- Organized file structure (separate by type/date)
- Production pattern replication (prod uses subdirs)

**Important:**
- `subPath` directory must exist in NFS export
- Create on Windows: `C:\simulator-data\nas-input-1\project-a\`
- Restart NAS pod to sync

**Example with multiple subdirs:**

```yaml
volumeMounts:
  - name: nas-input-1
    mountPath: /mnt/input/raw
    subPath: raw-files
  - name: nas-input-1
    mountPath: /mnt/input/processed
    subPath: processed-files
```

Mounts two subdirectories from same PVC to different paths.

---

## Windows Directory Setup

### Running setup-windows.ps1

**Location:** `scripts/setup-windows.ps1`

**What it does:**

1. Creates base directories: `input`, `output`, `temp`, `config`
2. **Creates NAS directories:** `nas-input-1/2/3`, `nas-backup`, `nas-output-1/2/3`
3. Generates `README.txt` in each NAS directory with purpose/role
4. Provides Minikube mount configuration
5. Generates environment file for helper scripts
6. Creates protocol-specific helper scripts (FTP, SFTP, S3, SMB)
7. Optionally deploys Helm chart

**Usage:**

```powershell
# Run with defaults (C:\simulator-data)
.\scripts\setup-windows.ps1

# Custom path
.\scripts\setup-windows.ps1 -SimulatorPath "D:\my-test-data"
```

**Output:**

```
[Step 1/6] Creating base directory structure...
  Created: C:\simulator-data
  Created: C:\simulator-data\input
  Created: C:\simulator-data\output
  Created: C:\simulator-data\temp
  Created: C:\simulator-data\config

[Step 2/6] Creating NAS server directories...
  Created: C:\simulator-data\nas-input-1
  Created: C:\simulator-data\nas-input-2
  Created: C:\simulator-data\nas-input-3
  Created: C:\simulator-data\nas-backup
  Created: C:\simulator-data\nas-output-1
  Created: C:\simulator-data\nas-output-2
  Created: C:\simulator-data\nas-output-3

[Step 3/6] Configuring Minikube mount...
  Minikube profile: file-simulator
  Mount command: minikube start --mount --mount-string="C:\simulator-data:/mnt/simulator-data" -p file-simulator

[Step 4/6] Generating environment file...
  Created: C:\simulator-data\config\.env

[Step 5/6] Creating helper scripts...
  Created: C:\simulator-data\config\test-ftp.ps1
  Created: C:\simulator-data\config\test-sftp.ps1
  Created: C:\simulator-data\config\test-s3.ps1
  Created: C:\simulator-data\config\test-smb.ps1

[Step 6/6] Helm deployment (optional)...
  Skipped (use -DeployHelm flag to enable)

Setup complete!
```

### Directory Structure Created

```
C:\simulator-data\
├── input\              # General files (legacy pattern)
├── output\             # General files (legacy pattern)
├── temp\               # Temporary files
├── config\             # Helper scripts
│   ├── .env
│   ├── test-ftp.ps1
│   ├── test-sftp.ps1
│   ├── test-s3.ps1
│   └── test-smb.ps1
├── nas-input-1\        # NAS server 1 (input role)
│   └── README.txt
├── nas-input-2\        # NAS server 2 (input role)
│   └── README.txt
├── nas-input-3\        # NAS server 3 (input role)
│   └── README.txt
├── nas-backup\         # Backup server
│   └── README.txt
├── nas-output-1\       # NAS server 1 (output role)
│   └── README.txt
├── nas-output-2\       # NAS server 2 (output role)
│   └── README.txt
└── nas-output-3\       # NAS server 3 (output role)
    └── README.txt
```

### How Init Containers Sync Files

**Input NAS servers (one-way sync):**

```yaml
initContainers:
  - name: sync-windows-to-nfs
    image: alpine:latest
    command:
      - sh
      - -c
      - |
        apk add --no-cache rsync
        rsync -av --delete /mnt/simulator-data/nas-input-1/ /data/
    volumeMounts:
      - name: windows-mount
        mountPath: /mnt/simulator-data/nas-input-1
        readOnly: true
      - name: nfs-data
        mountPath: /data
```

**Sync behavior:**
- Runs once at pod start
- Copies all files from Windows to emptyDir
- `--delete` flag removes files deleted on Windows
- **Requires pod restart to sync new files**

**Output NAS servers (bidirectional sync):**

```yaml
containers:
  - name: sync-nfs-to-windows
    image: alpine:latest
    restartPolicy: Always  # Native sidecar (K8s 1.28+)
    command:
      - sh
      - -c
      - |
        apk add --no-cache rsync inotify-tools
        while true; do
          inotifywait -r -e modify,create,delete,move /data || sleep 5
          rsync -av /data/ /mnt/simulator-data/nas-output-1/
          sleep 5
        done
    volumeMounts:
      - name: nfs-data
        mountPath: /data
      - name: windows-mount
        mountPath: /mnt/simulator-data/nas-output-1
```

**Sync behavior:**
- Runs continuously as sidecar
- Detects NFS changes via inotify
- Syncs to Windows within 15-30 seconds
- **No pod restart needed for NFS → Windows**

---

## Troubleshooting

### PVC Stuck in Pending

**Symptoms:**
```bash
kubectl get pvc nas-input-1-pvc -n default
# STATUS: Pending (not Bound)
```

**Diagnostics:**

```bash
# Check PVC events
kubectl describe pvc nas-input-1-pvc -n default

# Common error messages:
# - "no persistent volumes available"
# - "waiting for a volume to be created"
# - "volume node affinity conflict"
```

**Possible causes:**

1. **PV doesn't exist:**
   ```bash
   kubectl get pv nas-input-1-pv
   # If not found, apply PV first
   kubectl apply -f examples/pv/nas-input-1-pv.yaml
   ```

2. **Label selector mismatch:**
   ```bash
   # Check PV label
   kubectl get pv nas-input-1-pv -o yaml | grep -A3 "labels:"
   # Expected: nas-server: nas-input-1

   # Check PVC selector
   kubectl get pvc nas-input-1-pvc -n default -o yaml | grep -A3 "selector:"
   # Expected: matchLabels.nas-server: nas-input-1
   ```

3. **Capacity mismatch:**
   ```bash
   # PV capacity
   kubectl get pv nas-input-1-pv -o jsonpath='{.spec.capacity.storage}'
   # PVC request
   kubectl get pvc nas-input-1-pvc -n default -o jsonpath='{.spec.resources.requests.storage}'
   # PVC request must be <= PV capacity
   ```

4. **NAS server not running:**
   ```bash
   kubectl --context=file-simulator get pod -n file-simulator -l nas-server=nas-input-1
   # Expected: STATUS=Running
   ```

**Solutions:**
- Apply PV before PVC
- Ensure label `nas-server` matches between PV and PVC
- Reduce PVC storage request if exceeds PV capacity
- Verify NAS pod running

### Pod Mount Failures

**Symptoms:**
```bash
kubectl get pod -n default -l app=multi-nas-app
# STATUS: ContainerCreating (stuck)
```

**Diagnostics:**

```bash
# Check pod events
kubectl describe pod -n default -l app=multi-nas-app

# Common error messages:
# - "MountVolume.SetUp failed"
# - "Unable to attach or mount volumes"
# - "timed out waiting for the condition"
```

**Possible causes:**

1. **PVC not bound:**
   ```bash
   kubectl get pvc -n default
   # All PVCs should show STATUS=Bound
   ```

2. **NFS server unreachable:**
   ```bash
   # Test DNS resolution
   kubectl --context=file-simulator get svc -n file-simulator | grep nas-input-1
   # Expected: ClusterIP assigned

   # Test port connectivity
   minikube ssh -p file-simulator "nc -zv file-sim-nas-input-1.file-simulator.svc.cluster.local 2049"
   # Expected: succeeded
   ```

3. **Mount options incompatible:**
   ```bash
   # Check PV mount options
   kubectl get pv nas-input-1-pv -o jsonpath='{.spec.mountOptions}'
   # Expected: [nfsvers=3 tcp hard intr]
   ```

**Solutions:**
- Ensure all PVCs bound before deploying pod
- Verify NAS services have ClusterIP assigned
- Check NAS pods are Running
- Restart pod after fixing underlying issue

### Empty Mounts

**Symptoms:**
Pod runs but mounted directories are empty:

```bash
kubectl exec -n default -it deploy/multi-nas-app -- ls /mnt/input-1
# Output: (empty or only lost+found)
```

**Diagnostics:**

```bash
# Check Windows directory
minikube ssh -p file-simulator "ls /mnt/simulator-data/nas-input-1"
# Expected: Files visible

# Check NAS pod NFS export
kubectl --context=file-simulator exec -n file-simulator file-sim-nas-input-1 -- ls /data
# Expected: Files visible

# Check init container logs
kubectl --context=file-simulator logs -n file-simulator file-sim-nas-input-1 -c sync-windows-to-nfs
# Expected: rsync completed successfully
```

**Possible causes:**

1. **No files on Windows:**
   ```powershell
   # Place test file
   "Test content" | Out-File C:\simulator-data\nas-input-1\test.txt
   ```

2. **Init container hasn't run:**
   ```bash
   # Restart NAS pod to trigger init container
   kubectl --context=file-simulator delete pod -n file-simulator -l nas-server=nas-input-1
   ```

3. **Sync failed:**
   ```bash
   # Check init container logs for errors
   kubectl --context=file-simulator logs -n file-simulator file-sim-nas-input-1 -c sync-windows-to-nfs
   ```

**Solutions:**
- Create test files on Windows: `C:\simulator-data\nas-input-1\test.txt`
- Restart NAS pod to sync: `kubectl delete pod ...`
- Verify init container logs show successful rsync

### NFS Mount Performance Issues

**Symptoms:**
- Slow file access (reads take seconds)
- "Stale NFS file handle" errors
- Operations timeout

**Diagnostics:**

```bash
# Check mount options in pod
kubectl exec -n default -it deploy/multi-nas-app -- mount | grep nfs

# Expected mount options:
# file-sim-nas-input-1.file-simulator.svc.cluster.local:/data on /mnt/input-1 type nfs (rw,relatime,vers=3,...)
```

**Possible causes:**

1. **Missing mount options:**
   PV doesn't specify explicit mount options; defaults vary

2. **UDP instead of TCP:**
   Some K8s versions default to UDP which is slower

3. **NFSv2 instead of NFSv3:**
   Older protocol version

**Solutions:**

Ensure PV has explicit mount options:

```yaml
spec:
  mountOptions:
    - nfsvers=3  # Force NFSv3
    - tcp        # Force TCP transport
    - hard       # Retry on failure
    - intr       # Allow interrupts
```

Delete PVC/PV and reapply with correct mount options.

---

## Reference

### NAS Server DNS Names

| Server | DNS Name (Cluster-Internal) |
|--------|----------------------------|
| nas-input-1 | `file-sim-nas-input-1.file-simulator.svc.cluster.local` |
| nas-input-2 | `file-sim-nas-input-2.file-simulator.svc.cluster.local` |
| nas-input-3 | `file-sim-nas-input-3.file-simulator.svc.cluster.local` |
| nas-backup | `file-sim-nas-backup.file-simulator.svc.cluster.local` |
| nas-output-1 | `file-sim-nas-output-1.file-simulator.svc.cluster.local` |
| nas-output-2 | `file-sim-nas-output-2.file-simulator.svc.cluster.local` |
| nas-output-3 | `file-sim-nas-output-3.file-simulator.svc.cluster.local` |

### NodePort Mappings

| Server | NodePort | External Access (Windows) |
|--------|----------|--------------------------|
| nas-input-1 | 32150 | `<minikube-ip>:32150` |
| nas-input-2 | 32151 | `<minikube-ip>:32151` |
| nas-input-3 | 32152 | `<minikube-ip>:32152` |
| nas-backup | 32153 | `<minikube-ip>:32153` |
| nas-output-1 | 32154 | `<minikube-ip>:32154` |
| nas-output-2 | 32155 | `<minikube-ip>:32155` |
| nas-output-3 | 32156 | `<minikube-ip>:32156` |

**Get Minikube IP:**
```bash
minikube ip -p file-simulator
```

### File Paths

| Location | Path | Description |
|----------|------|-------------|
| Windows directories | `C:\simulator-data\nas-*\` | Test file source |
| Minikube mount | `/mnt/simulator-data/nas-*` | Inside Minikube node |
| Init container source | `/mnt/simulator-data/nas-*` | Read-only hostPath |
| NFS export | `/data` | unfs3 export path |
| Application mount | `/mnt/input-1` (example) | Pod volumeMount path |

### Key Commands

**Deploy PV/PVC:**
```bash
kubectl apply -f examples/pv/
kubectl apply -f examples/pvc/ -n <namespace>
```

**Verify binding:**
```bash
kubectl get pv -l type=nfs
kubectl get pvc -n <namespace>
```

**Check NAS pods:**
```bash
kubectl --context=file-simulator get pods -n file-simulator
```

**Restart NAS pod (sync files):**
```bash
kubectl --context=file-simulator delete pod -n file-simulator -l nas-server=nas-input-1
```

**Test NFS mount:**
```bash
kubectl --context=file-simulator exec -n file-simulator file-sim-nas-input-1 -- ls /data
```

**Mount from Windows (direct NFS):**
```powershell
$MINIKUBE_IP = minikube ip -p file-simulator
mount -o anon \\$MINIKUBE_IP\32150 Z:
```

---

## Additional Resources

- **Deployment example:** `examples/deployments/multi-mount-example.yaml`
- **Deployment guide:** `examples/deployments/README.md`
- **PV templates:** `examples/pv/`
- **PVC templates:** `examples/pvc/`
- **ConfigMap template:** `examples/configmap/`
- **Windows setup:** `scripts/setup-windows.ps1`
- **Helm values:** `values-multi-nas.yaml`
- **Phase 3 validation:** `.planning/phases/03-bidirectional-sync/03-02-SUMMARY.md`

---

**Document Version:** 1.0
**Last Updated:** 2026-02-01
**Maintained By:** File Simulator Suite Project
