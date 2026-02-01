# Phase 4: Configuration Templates - Research

**Researched:** 2026-02-01
**Domain:** Kubernetes PV/PVC manifests, service discovery ConfigMaps, and integration documentation for NFS multi-mount scenarios
**Confidence:** HIGH

## Summary

This research investigates how to deliver ready-to-use configuration templates that allow systems under development to connect to the 7 NAS servers using production-identical Kubernetes patterns. The goal is to bridge the gap between deployed infrastructure (Phases 1-3) and consumer applications that need to mount these NAS servers.

The standard approach involves three components:

1. **Static PV/PVC manifests** for each NAS server, using manual provisioning with explicit NFS server addresses
2. **ConfigMap for service discovery** containing DNS names and connection details for all 7 NAS servers
3. **Example deployment manifests** showing multi-NFS mount patterns (3+ servers simultaneously)
4. **Windows setup automation** to create all 7 directories before deployment

Static provisioning is preferred over dynamic provisioning because: (a) the NAS infrastructure already exists (not provisioned on-demand), (b) production OCP environments typically use pre-defined PVs with specific server mappings, (c) static manifests provide explicit configuration matching production topology exactly.

**Primary recommendation:** Create 7 static PV YAML files (one per NAS server) with explicit NFS server addresses, 7 matching PVC files for claiming each PV, a ConfigMap with all service endpoints, example deployment showing volumeMounts for multiple NAS servers, and update setup-windows.ps1 to create all 7 nas-* directories automatically.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Kubernetes PersistentVolume | v1 | Storage abstraction layer | Core K8s API for storage provisioning |
| Kubernetes PersistentVolumeClaim | v1 | Storage consumption mechanism | Standard way pods request storage |
| Kubernetes ConfigMap | v1 | Service discovery configuration | Store non-sensitive connection details |
| Kubernetes Deployment | apps/v1 | Pod orchestration | Standard workload controller |
| NFS volume plugin | built-in | NFS mount support | Native K8s support, no CSI driver needed |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| PowerShell | 5.1+ | Windows automation | Directory creation, environment setup |
| kubectl | 1.25+ | Manifest application | Deploying PV/PVC to cluster |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Static PV/PVC | Dynamic provisioning with StorageClass | Dynamic requires NFS provisioner; static matches production patterns better |
| ConfigMap | Service objects only | ConfigMap provides additional metadata (paths, credentials) beyond DNS |
| Multiple PVCs | Single PVC with subdirectories | Production uses separate PVs per NAS device; dev must match |
| PowerShell setup | Manual directory creation | Manual error-prone; script ensures consistency |

**Installation:**
```bash
# No additional tools required beyond kubectl
kubectl version
```

## Architecture Patterns

### Recommended Project Structure
```
helm-chart/file-simulator/
├── examples/
│   ├── pv/
│   │   ├── nas-input-1-pv.yaml      # 7 PV files (one per NAS)
│   │   ├── nas-input-2-pv.yaml
│   │   ├── nas-input-3-pv.yaml
│   │   ├── nas-backup-pv.yaml
│   │   ├── nas-output-1-pv.yaml
│   │   ├── nas-output-2-pv.yaml
│   │   └── nas-output-3-pv.yaml
│   ├── pvc/
│   │   ├── nas-input-1-pvc.yaml     # 7 PVC files (matching PVs)
│   │   ├── ... (same naming pattern)
│   ├── configmap/
│   │   └── nas-endpoints-configmap.yaml  # Service discovery for all 7
│   ├── deployments/
│   │   ├── multi-mount-example.yaml      # Example: mount 3+ NAS simultaneously
│   │   └── README.md                     # Explains how to use examples
│   └── windows/
│       └── setup-nas-directories.ps1     # Creates all 7 Windows directories
├── docs/
│   └── NAS-INTEGRATION-GUIDE.md          # Integration documentation
```

### Pattern 1: Static PV with Explicit NFS Server
**What:** PersistentVolume definition pointing to specific NFS server by DNS name
**When to use:** Always for pre-existing NFS infrastructure (not dynamically provisioned)

**Example:**
```yaml
# Source: Kubernetes Persistent Volumes official documentation
# https://kubernetes.io/docs/concepts/storage/persistent-volumes/
apiVersion: v1
kind: PersistentVolume
metadata:
  name: nas-input-1-pv
  labels:
    type: nfs
    nas-role: input
    nas-server: nas-input-1
spec:
  capacity:
    storage: 10Gi
  accessModes:
    - ReadWriteMany  # Multiple pods can mount simultaneously
  persistentVolumeReclaimPolicy: Retain  # Data persists after PVC deletion
  nfs:
    server: file-sim-nas-input-1.file-simulator.svc.cluster.local
    path: /data
  mountOptions:
    - nfsvers=3
    - tcp
    - hard
    - intr
```

**Key details:**
- `server`: DNS name of NFS service (stable across pod restarts)
- `path`: Always `/data` (unfs3 export path from Phases 1-3)
- `accessModes: ReadWriteMany`: NFS supports multiple readers/writers
- `persistentVolumeReclaimPolicy: Retain`: Matches production (data not auto-deleted)
- `mountOptions`: NFSv3, TCP-only, hard mount with interrupt support

### Pattern 2: PVC with Explicit Binding to PV
**What:** PersistentVolumeClaim that binds to specific PV by matching labels/selector
**When to use:** Always when static PVs exist (not using StorageClass)

**Example:**
```yaml
# Source: Kubernetes PVC binding documentation
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: nas-input-1-pvc
  namespace: default  # Adjust to application namespace
spec:
  accessModes:
    - ReadWriteMany
  resources:
    requests:
      storage: 10Gi
  selector:
    matchLabels:
      nas-server: nas-input-1  # Matches PV label
  # No storageClassName (uses static PV binding)
```

**Key details:**
- `selector.matchLabels`: Ensures PVC binds to correct PV
- No `storageClassName`: Indicates static provisioning
- `namespace`: Each application creates PVCs in its own namespace
- `resources.requests.storage`: Must match or be less than PV capacity

### Pattern 3: ConfigMap for Service Discovery
**What:** ConfigMap containing DNS names, ports, and metadata for all 7 NAS servers
**When to use:** Always for providing connection details to applications

**Example:**
```yaml
# Source: Kubernetes ConfigMap documentation
# https://kubernetes.io/docs/concepts/configuration/configmap/
apiVersion: v1
kind: ConfigMap
metadata:
  name: file-simulator-nas-endpoints
  namespace: default  # Deploy to application namespace
data:
  # NFS cluster-internal endpoints (DNS names)
  NAS_INPUT_1_HOST: "file-sim-nas-input-1.file-simulator.svc.cluster.local"
  NAS_INPUT_2_HOST: "file-sim-nas-input-2.file-simulator.svc.cluster.local"
  NAS_INPUT_3_HOST: "file-sim-nas-input-3.file-simulator.svc.cluster.local"
  NAS_BACKUP_HOST: "file-sim-nas-backup.file-simulator.svc.cluster.local"
  NAS_OUTPUT_1_HOST: "file-sim-nas-output-1.file-simulator.svc.cluster.local"
  NAS_OUTPUT_2_HOST: "file-sim-nas-output-2.file-simulator.svc.cluster.local"
  NAS_OUTPUT_3_HOST: "file-sim-nas-output-3.file-simulator.svc.cluster.local"

  # NFS ports (all use standard 2049)
  NAS_PORT: "2049"

  # Export paths (all use /data)
  NAS_EXPORT_PATH: "/data"

  # NodePort external access (from Windows/external hosts)
  NAS_INPUT_1_NODEPORT: "32150"
  NAS_INPUT_2_NODEPORT: "32151"
  NAS_INPUT_3_NODEPORT: "32152"
  NAS_BACKUP_NODEPORT: "32153"
  NAS_OUTPUT_1_NODEPORT: "32154"
  NAS_OUTPUT_2_NODEPORT: "32155"
  NAS_OUTPUT_3_NODEPORT: "32156"

  # Minikube IP (filled by setup script)
  MINIKUBE_IP: "<minikube-ip>"

  # PV/PVC names for reference
  NAS_INPUT_1_PVC: "nas-input-1-pvc"
  NAS_INPUT_2_PVC: "nas-input-2-pvc"
  NAS_INPUT_3_PVC: "nas-input-3-pvc"
  NAS_BACKUP_PVC: "nas-backup-pvc"
  NAS_OUTPUT_1_PVC: "nas-output-1-pvc"
  NAS_OUTPUT_2_PVC: "nas-output-2-pvc"
  NAS_OUTPUT_3_PVC: "nas-output-3-pvc"
```

**Usage in deployment:**
```yaml
spec:
  containers:
    - name: app
      envFrom:
        - configMapRef:
            name: file-simulator-nas-endpoints
```

### Pattern 4: Multi-NFS Mount in Single Deployment
**What:** Deployment that mounts 3+ NAS servers simultaneously to demonstrate production pattern
**When to use:** Always as example showing real-world multi-mount scenarios

**Example:**
```yaml
# Source: Kubernetes multiple volume mounts pattern
apiVersion: apps/v1
kind: Deployment
metadata:
  name: multi-nas-example
spec:
  replicas: 1
  template:
    spec:
      containers:
        - name: app
          image: busybox:latest
          command: ["sh", "-c", "sleep 3600"]
          volumeMounts:
            # Mount input NAS servers
            - name: nas-input-1
              mountPath: /mnt/input-1
            - name: nas-input-2
              mountPath: /mnt/input-2
            - name: nas-input-3
              mountPath: /mnt/input-3
            # Mount output NAS servers
            - name: nas-output-1
              mountPath: /mnt/output-1
            - name: nas-output-2
              mountPath: /mnt/output-2
          env:
            # Service discovery via ConfigMap
            - name: NAS_INPUT_1_HOST
              valueFrom:
                configMapKeyRef:
                  name: file-simulator-nas-endpoints
                  key: NAS_INPUT_1_HOST
      volumes:
        # Claim PVCs for each NAS
        - name: nas-input-1
          persistentVolumeClaim:
            claimName: nas-input-1-pvc
        - name: nas-input-2
          persistentVolumeClaim:
            claimName: nas-input-2-pvc
        - name: nas-input-3
          persistentVolumeClaim:
            claimName: nas-input-3-pvc
        - name: nas-output-1
          persistentVolumeClaim:
            claimName: nas-output-1-pvc
        - name: nas-output-2
          persistentVolumeClaim:
            claimName: nas-output-2-pvc
```

**Key pattern:** Each NAS server gets:
1. PVC reference in `volumes` section
2. volumeMount in container with unique mountPath
3. Optional env var from ConfigMap for programmatic access

### Pattern 5: Windows Directory Automation Script
**What:** PowerShell script that creates all 7 NAS directories before cluster deployment
**When to use:** Always as part of Windows environment setup (before Minikube mount)

**Example:**
```powershell
# Source: Existing setup-windows.ps1 pattern
param(
    [string]$SimulatorPath = "C:\simulator-data"
)

Write-Host "Creating NAS server directories..."

$nasServers = @(
    "nas-input-1",
    "nas-input-2",
    "nas-input-3",
    "nas-backup",
    "nas-output-1",
    "nas-output-2",
    "nas-output-3"
)

foreach ($nas in $nasServers) {
    $dir = "$SimulatorPath\$nas"
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "  Created: $dir" -ForegroundColor Green
    } else {
        Write-Host "  Exists: $dir" -ForegroundColor Gray
    }
}

Write-Host "`nNAS directories ready for Minikube mount."
Write-Host "Start Minikube with:"
Write-Host "  minikube start --mount --mount-string='$SimulatorPath:/mnt/simulator-data'"
```

### Pattern 6: Production-Style PV Naming Convention
**What:** PV/PVC naming convention that matches production OpenShift patterns
**When to use:** Always to maintain dev/prod parity

**Naming pattern:**
```
PV:  {nas-name}-pv      (e.g., nas-input-1-pv)
PVC: {nas-name}-pvc     (e.g., nas-input-1-pvc)

Labels:
  type: nfs
  nas-role: input|backup|output
  nas-server: {nas-name}
  environment: development
```

**Why this matters:**
- Production OCP uses similar naming conventions
- Labels enable filtering: `kubectl get pv -l nas-role=input`
- Consistent naming simplifies troubleshooting
- Migration from dev to prod requires minimal config changes

### Anti-Patterns to Avoid
- **Using StorageClass for existing NFS servers:** Dynamic provisioning doesn't apply when servers already exist
- **Hardcoding Minikube IP in manifests:** Use DNS names; IP changes across Minikube restarts
- **Single PVC with subdirectories:** Production uses separate PVs per NAS; dev must match topology
- **Omitting mountOptions:** NFS behavior differs without explicit nfsvers=3, tcp flags
- **Not using ReadWriteMany:** NFS supports multiple mounts; using ReadWriteOnce wastes NFS capability
- **Empty ConfigMap:** Service discovery should include ports, paths, NodePort mappings
- **Not pre-creating Windows directories:** hostPath DirectoryOrCreate works in pod, but Minikube mount needs directories first

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| NFS CSI driver | Custom NFS provisioner | Built-in NFS volume plugin | K8s has native NFS support; CSI adds unnecessary complexity |
| Service discovery mechanism | Custom endpoint resolution | ConfigMap + DNS | DNS already provides service discovery; ConfigMap adds metadata |
| Directory creation logic | Manual mkdir commands | PowerShell script | Script ensures all 7 directories created consistently |
| Multi-mount validation | Custom pod spec | Validated example deployment | Example provides tested working configuration |
| PV/PVC binding | ClusterIP matching | Label selector | Labels provide reliable binding; IPs ephemeral |
| NFS mount options | Default mount behavior | Explicit mountOptions | Defaults vary by K8s version; explicit ensures consistency |

**Key insight:** Kubernetes PV/PVC is designed for exactly this use case - abstracting pre-existing storage and making it consumable by applications. Static provisioning with explicit NFS servers matches production OCP patterns better than dynamic provisioning.

## Common Pitfalls

### Pitfall 1: PVC Stuck in Pending Due to Missing Selector
**What goes wrong:** PVC remains Pending; never binds to PV despite capacity match
**Why it happens:** Multiple PVs match capacity/accessMode; PVC needs selector to choose specific PV
**How to avoid:** Always include `selector.matchLabels` in PVC referencing unique PV label
**Warning signs:**
- `kubectl describe pvc` shows "no persistent volumes available"
- Multiple PVs exist with same capacity
- PVC binds to wrong PV

### Pitfall 2: PV Reclaim Policy Delete Loses Data
**What goes wrong:** Deleting PVC deletes PV and data on NFS server
**Why it happens:** Default persistentVolumeReclaimPolicy may be Delete
**How to avoid:** Explicitly set `persistentVolumeReclaimPolicy: Retain` in all PV manifests
**Warning signs:**
- Data disappears after PVC deletion
- PV also deleted when PVC removed
- Files on Windows directory removed

### Pitfall 3: Wrong Namespace for PVC Causes Binding Failure
**What goes wrong:** PVC can't bind to PV even with correct selector
**Why it happens:** PVs are cluster-scoped; PVCs are namespace-scoped; PVC in wrong namespace can't see PV
**How to avoid:** PVs have no namespace (cluster-wide), but PVCs must be in application's namespace. Example uses `default` namespace.
**Warning signs:**
- PVC shows "no persistent volumes available"
- PV exists but PVC can't bind
- `kubectl get pv` shows PV Available despite matching PVC

### Pitfall 4: Mount Options Missing Causes Performance Issues
**What goes wrong:** NFS mounts work but have stale file handles or poor performance
**Why it happens:** Default NFS mount options vary by K8s version and node OS
**How to avoid:** Always specify explicit mountOptions: `nfsvers=3, tcp, hard, intr`
**Warning signs:**
- "Stale NFS file handle" errors in pod logs
- Slow file access compared to Windows hostPath
- Mount succeeds but operations timeout

### Pitfall 5: ConfigMap in Wrong Namespace Not Visible to Pods
**What goes wrong:** Pods can't load ConfigMap; envFrom fails with "not found"
**Why it happens:** ConfigMaps are namespace-scoped; pods can only reference ConfigMaps in same namespace
**How to avoid:** Deploy ConfigMap to same namespace as application deployment
**Warning signs:**
- Pod stuck in CreateContainerConfigError
- `kubectl describe pod` shows "configmap not found"
- ConfigMap exists in different namespace

### Pitfall 6: Windows Directories Not Created Before Minikube Mount
**What goes wrong:** Minikube mount fails or NAS pods crash with "directory not found"
**Why it happens:** Minikube mount requires source directory to exist; hostPath DirectoryOrCreate creates in pod, not on Windows
**How to avoid:** Run setup script BEFORE starting Minikube with mount
**Warning signs:**
- Minikube mount command fails
- Init container shows "no such file or directory"
- Pod stuck in Init:Error

### Pitfall 7: Storage Capacity Mismatch Between PV and PVC
**What goes wrong:** PVC can't bind to PV despite matching selector
**Why it happens:** PVC requests more storage than PV provides
**How to avoid:** PVC `resources.requests.storage` must be <= PV `capacity.storage`
**Warning signs:**
- PVC Pending with "no persistent volumes available"
- PV Available but not bound
- `kubectl describe pvc` shows capacity mismatch

## Code Examples

Verified patterns from official sources:

### Complete PV Manifest for nas-input-1
```yaml
# Source: Kubernetes NFS PV documentation
# https://kubernetes.io/docs/concepts/storage/persistent-volumes/
apiVersion: v1
kind: PersistentVolume
metadata:
  name: nas-input-1-pv
  labels:
    type: nfs
    nas-role: input
    nas-server: nas-input-1
    environment: development
spec:
  capacity:
    storage: 10Gi
  accessModes:
    - ReadWriteMany
  persistentVolumeReclaimPolicy: Retain
  nfs:
    server: file-sim-nas-input-1.file-simulator.svc.cluster.local
    path: /data
  mountOptions:
    - nfsvers=3
    - tcp
    - hard
    - intr
```

### Complete PVC Manifest for nas-input-1
```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: nas-input-1-pvc
  namespace: default
  labels:
    nas-server: nas-input-1
    nas-role: input
spec:
  accessModes:
    - ReadWriteMany
  resources:
    requests:
      storage: 10Gi
  selector:
    matchLabels:
      nas-server: nas-input-1
```

### Complete ConfigMap for All 7 NAS Servers
```yaml
# Source: ConfigMap service discovery pattern
apiVersion: v1
kind: ConfigMap
metadata:
  name: file-simulator-nas-endpoints
  namespace: default
data:
  # Input NAS servers
  NAS_INPUT_1_HOST: "file-sim-nas-input-1.file-simulator.svc.cluster.local"
  NAS_INPUT_1_PORT: "2049"
  NAS_INPUT_1_PATH: "/data"
  NAS_INPUT_1_NODEPORT: "32150"
  NAS_INPUT_1_PVC: "nas-input-1-pvc"

  NAS_INPUT_2_HOST: "file-sim-nas-input-2.file-simulator.svc.cluster.local"
  NAS_INPUT_2_PORT: "2049"
  NAS_INPUT_2_PATH: "/data"
  NAS_INPUT_2_NODEPORT: "32151"
  NAS_INPUT_2_PVC: "nas-input-2-pvc"

  NAS_INPUT_3_HOST: "file-sim-nas-input-3.file-simulator.svc.cluster.local"
  NAS_INPUT_3_PORT: "2049"
  NAS_INPUT_3_PATH: "/data"
  NAS_INPUT_3_NODEPORT: "32152"
  NAS_INPUT_3_PVC: "nas-input-3-pvc"

  # Backup NAS server
  NAS_BACKUP_HOST: "file-sim-nas-backup.file-simulator.svc.cluster.local"
  NAS_BACKUP_PORT: "2049"
  NAS_BACKUP_PATH: "/data"
  NAS_BACKUP_NODEPORT: "32153"
  NAS_BACKUP_PVC: "nas-backup-pvc"

  # Output NAS servers
  NAS_OUTPUT_1_HOST: "file-sim-nas-output-1.file-simulator.svc.cluster.local"
  NAS_OUTPUT_1_PORT: "2049"
  NAS_OUTPUT_1_PATH: "/data"
  NAS_OUTPUT_1_NODEPORT: "32154"
  NAS_OUTPUT_1_PVC: "nas-output-1-pvc"

  NAS_OUTPUT_2_HOST: "file-sim-nas-output-2.file-simulator.svc.cluster.local"
  NAS_OUTPUT_2_PORT: "2049"
  NAS_OUTPUT_2_PATH: "/data"
  NAS_OUTPUT_2_NODEPORT: "32155"
  NAS_OUTPUT_2_PVC: "nas-output-2-pvc"

  NAS_OUTPUT_3_HOST: "file-sim-nas-output-3.file-simulator.svc.cluster.local"
  NAS_OUTPUT_3_PORT: "2049"
  NAS_OUTPUT_3_PATH: "/data"
  NAS_OUTPUT_3_NODEPORT: "32156"
  NAS_OUTPUT_3_PVC: "nas-output-3-pvc"
```

### Multi-Mount Example Deployment
```yaml
# Source: Multi-volume Kubernetes deployment pattern
apiVersion: apps/v1
kind: Deployment
metadata:
  name: multi-nas-app
  namespace: default
spec:
  replicas: 1
  selector:
    matchLabels:
      app: multi-nas-app
  template:
    metadata:
      labels:
        app: multi-nas-app
    spec:
      containers:
        - name: app
          image: busybox:latest
          command:
            - sh
            - -c
            - |
              echo "=== Multi-NAS Mount Example ==="
              echo "Listing mounted NAS servers:"
              echo ""
              echo "Input servers:"
              ls -la /mnt/input-1 | head -5
              ls -la /mnt/input-2 | head -5
              ls -la /mnt/input-3 | head -5
              echo ""
              echo "Output servers:"
              ls -la /mnt/output-1 | head -5
              ls -la /mnt/output-2 | head -5
              ls -la /mnt/output-3 | head -5
              echo ""
              echo "All mounts ready. Sleeping..."
              sleep 3600
          volumeMounts:
            # Input NAS servers
            - name: nas-input-1
              mountPath: /mnt/input-1
              readOnly: false  # Can read and write
            - name: nas-input-2
              mountPath: /mnt/input-2
            - name: nas-input-3
              mountPath: /mnt/input-3
            # Output NAS servers
            - name: nas-output-1
              mountPath: /mnt/output-1
            - name: nas-output-2
              mountPath: /mnt/output-2
            - name: nas-output-3
              mountPath: /mnt/output-3
          envFrom:
            # Load all NAS endpoints as environment variables
            - configMapRef:
                name: file-simulator-nas-endpoints
      volumes:
        # Reference PVCs for each NAS server
        - name: nas-input-1
          persistentVolumeClaim:
            claimName: nas-input-1-pvc
        - name: nas-input-2
          persistentVolumeClaim:
            claimName: nas-input-2-pvc
        - name: nas-input-3
          persistentVolumeClaim:
            claimName: nas-input-3-pvc
        - name: nas-output-1
          persistentVolumeClaim:
            claimName: nas-output-1-pvc
        - name: nas-output-2
          persistentVolumeClaim:
            claimName: nas-output-2-pvc
        - name: nas-output-3
          persistentVolumeClaim:
            claimName: nas-output-3-pvc
```

### Windows Directory Setup Script Enhancement
```powershell
# Source: Extending existing setup-windows.ps1
# Add to setup-windows.ps1 after existing directory creation

Write-Host "`n[NAS] Creating NAS server directories..." -ForegroundColor Yellow

$nasServers = @(
    "nas-input-1",
    "nas-input-2",
    "nas-input-3",
    "nas-backup",
    "nas-output-1",
    "nas-output-2",
    "nas-output-3"
)

foreach ($nas in $nasServers) {
    $dir = "$SimulatorPath\$nas"
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "  Created: $dir" -ForegroundColor Green

        # Create sample file in each directory
        $sampleFile = "$dir\README.txt"
        $content = @"
NAS Server: $nas
Created: $(Get-Date)
Purpose: $(if ($nas -like '*input*') { 'Input files for processing' } elseif ($nas -like '*output*') { 'Output files from processing' } else { 'Backup files' })

Place test files in this directory to make them available via NFS mount.
"@
        Set-Content -Path $sampleFile -Value $content
    } else {
        Write-Host "  Exists: $dir" -ForegroundColor Gray
    }
}

Write-Host "  NAS directory structure ready" -ForegroundColor Green
```

### Deployment Instructions Document
```markdown
# Source: Integration guide pattern
# File: helm-chart/file-simulator/examples/deployments/README.md

## Deploying the Multi-NAS Example

### Prerequisites
1. File Simulator Suite deployed (Phases 1-3 complete)
2. All 7 NAS servers running in file-simulator namespace
3. Windows directories created at C:\simulator-data\nas-*

### Step 1: Apply PersistentVolumes
```bash
kubectl apply -f ../pv/
# Verify: kubectl get pv -l type=nfs
# Expected: 7 PVs in Available state
```

### Step 2: Apply PersistentVolumeClaims
```bash
kubectl apply -f ../pvc/ -n default
# Verify: kubectl get pvc -n default
# Expected: 7 PVCs in Bound state
```

### Step 3: Apply ConfigMap
```bash
# Update MINIKUBE_IP in configmap first
export MINIKUBE_IP=$(minikube ip -p file-simulator)
sed -i "s/<minikube-ip>/$MINIKUBE_IP/g" ../configmap/nas-endpoints-configmap.yaml

kubectl apply -f ../configmap/ -n default
# Verify: kubectl get configmap file-simulator-nas-endpoints -n default
```

### Step 4: Deploy Example Application
```bash
kubectl apply -f multi-mount-example.yaml -n default
# Verify: kubectl get pod -n default -l app=multi-nas-app
# Expected: Pod in Running state with 6 volumes mounted
```

### Step 5: Verify Multi-Mount
```bash
kubectl logs -n default -l app=multi-nas-app

# Expected output:
# === Multi-NAS Mount Example ===
# Listing mounted NAS servers:
# Input servers:
# total X
# -rw-r--r-- 1 root root ... README.txt
# ...
```

### Troubleshooting
- PVC Pending: Check selector matches PV labels
- Mount fails: Verify NAS pods running in file-simulator namespace
- Empty mounts: Verify Windows directories exist and have files
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Dynamic NFS provisioning | Static PV/PVC for existing servers | N/A | Static matches production patterns; dynamic for cloud storage |
| Hardcoded NFS IPs | DNS-based service discovery | K8s 1.0+ (2015) | Stable references despite pod restarts |
| Single storage class | Per-NAS PV/PVC | Production requirement | Matches multi-device topology |
| Environment variables only | ConfigMap + DNS | K8s 1.2+ (2016) | Centralized configuration, easier updates |
| Manual manifest creation | Template-based generation | Helm 2.0+ (2016) | Consistent manifests, reduced errors |
| ClusterIP matching | Label selectors | K8s 1.0+ | Reliable binding despite IP changes |

**Deprecated/outdated:**
- **Dynamic provisioning for static infrastructure:** Use static PV/PVC when NAS servers pre-exist
- **StorageClass for NFS without provisioner:** K8s native NFS plugin doesn't need StorageClass
- **Hardcoded IP addresses in PV:** DNS names survive pod/service restarts
- **ReadWriteOnce for NFS volumes:** NFS supports ReadWriteMany; using RWO wastes capability

## Open Questions

Things that couldn't be fully resolved:

1. **Should PVs be created by users or included in Helm chart?**
   - What we know: PVs are cluster-scoped; PVCs are namespace-scoped
   - What's unclear: Best practice for multi-tenant scenarios (each team creates own PVCs)
   - Recommendation: Provide PV manifests as examples; users apply to cluster. Helm chart focuses on NAS infrastructure (Phases 1-3), not consumer configuration.

2. **What storage capacity should PVs advertise?**
   - What we know: NFS doesn't enforce PV capacity limits; actual limit is NFS server disk space
   - What's unclear: Production capacity values; whether to match or use symbolic values
   - Recommendation: Use 10Gi as reasonable default; document that NFS doesn't enforce this limit

3. **Should ConfigMap include credentials?**
   - What we know: NFS doesn't use authentication in this setup (no_root_squash)
   - What's unclear: Whether to include placeholder credentials for future authentication
   - Recommendation: ConfigMap for non-sensitive data only; no credentials needed for NFS in dev environment

4. **How to handle Minikube IP in ConfigMap?**
   - What we know: Minikube IP needed for external access via NodePort; changes on restart
   - What's unclear: Best automation approach (manual edit vs script substitution)
   - Recommendation: ConfigMap template with `<minikube-ip>` placeholder; setup script or user performs substitution

5. **Should example include subdirectory mounts?**
   - What we know: Production mounts subdirectories (e.g., /data/sub-1); Phases 2-3 validated this works
   - What's unclear: Whether to show subdirectory pattern in example or keep simple
   - Recommendation: Main example uses /data root mount; add note about subdirectory support with example command

## Sources

### Primary (HIGH confidence)
- [Kubernetes Persistent Volumes Documentation](https://kubernetes.io/docs/concepts/storage/persistent-volumes/) - PV/PVC patterns, static vs dynamic provisioning
- [Kubernetes ConfigMaps Documentation](https://kubernetes.io/docs/concepts/configuration/configmap/) - Service discovery configuration patterns
- [Kubernetes Volumes Guide - NFS Examples](https://matthewpalmer.net/kubernetes-app-developer/articles/kubernetes-volumes-example-nfs-persistent-volume.html) - NFS volume configuration
- [OpenShift Persistent Storage NFS Documentation](https://docs.okd.io/latest/storage/persistent_storage/persistent-storage-nfs.html) - OCP-specific patterns for NFS PV/PVC
- file-simulator-suite codebase: values-multi-nas.yaml, existing integration patterns

### Secondary (MEDIUM confidence)
- [NetApp: Static vs Dynamic Storage Provisioning](https://www.netapp.com/blog/cvo-blg-static-vs-dynamic-storage-provisioning-a-look-under-the-hood/) - When to use static vs dynamic
- [Baeldung: Kubernetes PV and PVC](https://www.baeldung.com/ops/kubernetes-persistent-volume-pv-claim-pvc) - Binding mechanisms, reclaim policies
- [DigitalOcean: Use NFS Storage with Kubernetes](https://docs.digitalocean.com/products/kubernetes/how-to/use-nfs-storage/) - Multiple NFS mounts pattern
- [Spring Cloud Kubernetes Service Discovery](https://docs.spring.io/spring-cloud-kubernetes/docs/current/reference/html/) - ConfigMap-based service discovery
- [Last9: Kubernetes Service Discovery](https://last9.io/blog/kubernetes-service-discovery/) - DNS and ConfigMap patterns

### Tertiary (LOW confidence - general patterns)
- [Red Hat: NFS FSID Configuration](https://access.redhat.com/solutions/548083) - Production NFS patterns (may not apply to dev)
- [vCluster: Kubernetes Persistent Volume Best Practices](https://www.vcluster.com/blog/kubernetes-persistent-volume) - General PV/PVC guidance

## Metadata

**Confidence breakdown:**
- Standard stack (PV/PVC, ConfigMap): HIGH - Core Kubernetes APIs, well-documented
- Architecture (static provisioning, multi-mount): HIGH - Official docs verify patterns
- Pitfalls (binding, namespaces, mount options): HIGH - Based on K8s behavior and documentation
- ConfigMap structure: MEDIUM - Pattern is flexible; suggested structure based on conventions
- Production OCP patterns: MEDIUM - Inferred from OpenShift docs; actual production may vary

**Research date:** 2026-02-01
**Valid until:** 2026-03-15 (45 days) - Kubernetes APIs stable; PV/PVC patterns unlikely to change

**Notes:**
- Phase 4 bridges infrastructure (Phases 1-3) to consumer applications
- Static provisioning chosen to match production pre-existing NAS servers
- All 7 NAS servers get identical PV/PVC structure (only server names differ)
- ConfigMap provides discovery for applications that can't use PV/PVC (e.g., programmatic NFS access)
- Windows directory automation ensures consistent setup before Minikube deployment
