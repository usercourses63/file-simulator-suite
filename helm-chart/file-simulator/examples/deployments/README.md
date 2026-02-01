# Multi-NAS Mount Example Deployment

This example demonstrates how to mount multiple NAS servers simultaneously in a single Kubernetes pod, replicating production patterns where applications access 3+ NAS devices for input/output processing workflows.

## What This Example Demonstrates

- **Multi-mount pattern:** Mounting 6 NAS servers in one pod (3 input + 3 output)
- **PV/PVC integration:** Using PersistentVolumeClaims to access NFS storage
- **ConfigMap service discovery:** Loading NAS endpoints as environment variables
- **Production topology replication:** Matching multi-device NAS architecture from production OCP
- **Read/write testing:** Verifying file operations across all mounted NAS servers

## Prerequisites

Before deploying this example, ensure the following prerequisites are met:

### 1. File Simulator Suite Deployed

The NAS infrastructure must be running in the `file-simulator` namespace:

```bash
# Verify NAS pods are running
kubectl --context=file-simulator get pods -n file-simulator

# Expected output: 7 NAS pods (nas-input-1/2/3, nas-backup, nas-output-1/2/3)
# All should show STATUS=Running, READY=1/1
```

### 2. Windows Directories Created

All 7 NAS directories must exist on the Windows host before deployment:

```powershell
# Run the setup script
.\scripts\setup-windows.ps1

# Expected directories at C:\simulator-data\:
#   nas-input-1, nas-input-2, nas-input-3
#   nas-backup
#   nas-output-1, nas-output-2, nas-output-3
```

### 3. Minikube Mounted

The Windows directories must be mounted into Minikube:

```powershell
# Verify mount is active
minikube ssh -p file-simulator "ls /mnt/simulator-data"

# Expected: All 7 nas-* directories visible
```

---

## Deployment Steps

Follow these steps in order to deploy the multi-NAS example application.

### Step 1: Apply PersistentVolumes

PVs are cluster-scoped and point to the NFS servers in the `file-simulator` namespace.

```bash
cd helm-chart/file-simulator/examples

# Apply all 7 PVs
kubectl apply -f pv/

# Verify PVs are created
kubectl get pv -l type=nfs

# Expected output:
# NAME               CAPACITY   ACCESS MODES   RECLAIM POLICY   STATUS      CLAIM
# nas-input-1-pv     10Gi       RWX            Retain           Available
# nas-input-2-pv     10Gi       RWX            Retain           Available
# nas-input-3-pv     10Gi       RWX            Retain           Available
# nas-backup-pv      10Gi       RWX            Retain           Available
# nas-output-1-pv    10Gi       RWX            Retain           Available
# nas-output-2-pv    10Gi       RWX            Retain           Available
# nas-output-3-pv    10Gi       RWX            Retain           Available
#
# All should show STATUS=Available (not yet bound)
```

### Step 2: Apply PersistentVolumeClaims

PVCs are namespace-scoped. Apply them to the namespace where your application will run (typically `default`).

```bash
# Apply all 7 PVCs to default namespace
kubectl apply -f pvc/ -n default

# Verify PVCs are bound to PVs
kubectl get pvc -n default

# Expected output:
# NAME                STATUS   VOLUME             CAPACITY   ACCESS MODES
# nas-input-1-pvc     Bound    nas-input-1-pv     10Gi       RWX
# nas-input-2-pvc     Bound    nas-input-2-pv     10Gi       RWX
# nas-input-3-pvc     Bound    nas-input-3-pv     10Gi       RWX
# nas-backup-pvc      Bound    nas-backup-pv      10Gi       RWX
# nas-output-1-pvc    Bound    nas-output-1-pv    10Gi       RWX
# nas-output-2-pvc    Bound    nas-output-2-pv    10Gi       RWX
# nas-output-3-pvc    Bound    nas-output-3-pv    10Gi       RWX
#
# All should show STATUS=Bound
```

**Troubleshooting:** If PVCs remain `Pending`:
- Check that PV labels match PVC selectors: `kubectl get pv nas-input-1-pv -o yaml | grep nas-server`
- Verify NAS pods are running: `kubectl --context=file-simulator get pods -n file-simulator`
- Check PVC events: `kubectl describe pvc nas-input-1-pvc -n default`

### Step 3: Apply ConfigMap

The ConfigMap provides service discovery information (DNS names, ports, NodePorts).

**IMPORTANT:** Update the Minikube IP placeholder before applying!

```bash
# Get Minikube IP for the file-simulator profile
export MINIKUBE_IP=$(minikube ip -p file-simulator)
echo "Minikube IP: $MINIKUBE_IP"

# Substitute in ConfigMap (Linux/Mac)
sed -i "s/<minikube-ip>/$MINIKUBE_IP/g" configmap/nas-endpoints-configmap.yaml

# Or for Windows PowerShell:
# $MINIKUBE_IP = minikube ip -p file-simulator
# (Get-Content configmap\nas-endpoints-configmap.yaml) -replace '<minikube-ip>', $MINIKUBE_IP | Set-Content configmap\nas-endpoints-configmap.yaml

# Apply ConfigMap to application namespace
kubectl apply -f configmap/ -n default

# Verify ConfigMap created
kubectl get configmap file-simulator-nas-endpoints -n default

# Check contents
kubectl get configmap file-simulator-nas-endpoints -n default -o yaml | grep HOST
```

### Step 4: Deploy Example Application

Now deploy the multi-NAS mount example:

```bash
# Apply the deployment
kubectl apply -f deployments/multi-mount-example.yaml -n default

# Verify pod is running
kubectl get pod -n default -l app=multi-nas-app

# Expected output:
# NAME                            READY   STATUS    RESTARTS   AGE
# multi-nas-app-xxxxxxxxxx-xxxxx  1/1     Running   0          10s
```

**Troubleshooting:** If pod is not `Running`:
- Check pod events: `kubectl describe pod -n default -l app=multi-nas-app`
- Check ConfigMap reference: Pod should show `Successfully pulled image` and `Started container`
- Check PVC mounts: Look for errors like "Volume mount failed"

### Step 5: Verify Multi-Mount

Check the logs to see all 6 NAS servers mounted and accessible:

```bash
# View pod logs
kubectl logs -n default -l app=multi-nas-app

# Expected output shows:
# ==========================================
#   Multi-NAS Mount Example
# ==========================================
#
# This pod demonstrates mounting 6 NAS servers simultaneously:
#   - 3 input servers: nas-input-1/2/3
#   - 3 output servers: nas-output-1/2/3
#
# ConfigMap environment variables loaded:
#   NAS_INPUT_1_HOST: file-sim-nas-input-1.file-simulator.svc.cluster.local
#   ...
#
# ==========================================
#   Input NAS Server Contents
# ==========================================
#
# --- nas-input-1 (/mnt/input-1) ---
# total 4
# -rw-r--r-- 1 root root 123 Feb 01 10:00 README.txt
# ...
# ✓ Written to /mnt/output-1/test-from-pod.txt
# ✓ Written to /mnt/output-2/test-from-pod.txt
# ✓ Written to /mnt/output-3/test-from-pod.txt
```

### Step 6: Interactive Exploration (Optional)

Exec into the pod to explore the mounted filesystems interactively:

```bash
# Open shell in running pod
kubectl exec -n default -it deploy/multi-nas-app -- sh

# Inside the pod:
ls /mnt/input-1
ls /mnt/output-1
cat /mnt/input-1/README.txt
echo "Test from kubectl exec" > /mnt/output-1/interactive-test.txt
exit
```

Verify the file appears on Windows:

```powershell
# Check Windows directory
Get-Content C:\simulator-data\nas-output-1\interactive-test.txt

# Expected: "Test from kubectl exec"
```

---

## Verification Checklist

Confirm the following to validate successful deployment:

- [ ] All 7 PVs show `STATUS=Available` (before PVC creation) or `STATUS=Bound` (after PVC creation)
- [ ] All 7 PVCs show `STATUS=Bound` with correct VOLUME names
- [ ] ConfigMap exists in application namespace with correct DNS names
- [ ] Pod shows `STATUS=Running` with `READY=1/1`
- [ ] Pod logs show all 6 NAS mount paths with content listings
- [ ] Test files written to output servers appear in pod logs
- [ ] Files written from pod visible on Windows at `C:\simulator-data\nas-output-*\`

---

## Troubleshooting Guide

### PVC Stuck in Pending

**Symptom:** `kubectl get pvc` shows `STATUS=Pending`

**Possible causes:**
1. PV labels don't match PVC selector
2. PV capacity smaller than PVC request
3. NAS server pods not running

**Diagnostics:**
```bash
# Check PVC events
kubectl describe pvc nas-input-1-pvc -n default

# Verify PV labels
kubectl get pv nas-input-1-pv -o yaml | grep -A5 labels

# Verify PVC selector
kubectl get pvc nas-input-1-pvc -n default -o yaml | grep -A5 selector

# Verify NAS pod running
kubectl --context=file-simulator get pod -n file-simulator | grep nas-input-1
```

**Solution:**
- Ensure PV label `nas-server: nas-input-1` matches PVC selector `matchLabels.nas-server: nas-input-1`
- Check PV capacity >= PVC request (both should be 10Gi)

### Pod Mount Failures

**Symptom:** Pod stuck in `ContainerCreating` or logs show "mount failed"

**Possible causes:**
1. PVC not bound
2. NFS server not accessible
3. Network policy blocking traffic

**Diagnostics:**
```bash
# Check pod events
kubectl describe pod -n default -l app=multi-nas-app

# Verify NAS service DNS resolves
kubectl --context=file-simulator get svc -n file-simulator | grep nas-input-1

# Test NFS connectivity from node
minikube ssh -p file-simulator "nc -zv file-sim-nas-input-1.file-simulator.svc.cluster.local 2049"
```

**Solution:**
- Ensure PVCs are `Bound` before deploying pod
- Verify all NAS pods show `Running` status
- Check NAS services have ClusterIP assigned

### Empty Mounts

**Symptom:** Pod runs but `ls /mnt/input-1` shows empty directory

**Possible causes:**
1. Windows directories empty (no files placed)
2. Init container not syncing files
3. Pod started before NAS pod ready

**Diagnostics:**
```bash
# Check Windows directories
minikube ssh -p file-simulator "ls /mnt/simulator-data/nas-input-1"

# Check NAS pod logs for init container sync
kubectl --context=file-simulator logs -n file-simulator file-sim-nas-input-1 -c sync-windows-to-nfs

# List NFS export contents from NAS pod
kubectl --context=file-simulator exec -n file-simulator file-sim-nas-input-1 -- ls /data
```

**Solution:**
- Place files in `C:\simulator-data\nas-input-1\` on Windows
- Restart NAS pod to trigger init container sync: `kubectl --context=file-simulator delete pod -n file-simulator file-sim-nas-input-1`
- Wait for NAS pod to reach `Running` before applying PVCs

### ConfigMap Not Found

**Symptom:** Pod events show "configmap not found"

**Possible causes:**
1. ConfigMap not applied to correct namespace
2. ConfigMap name mismatch

**Diagnostics:**
```bash
# List ConfigMaps in application namespace
kubectl get configmap -n default

# Check deployment references correct name
kubectl get deployment multi-nas-app -n default -o yaml | grep configMapRef -A2
```

**Solution:**
- Apply ConfigMap to same namespace as deployment: `kubectl apply -f configmap/ -n default`
- Ensure ConfigMap name matches deployment reference: `file-simulator-nas-endpoints`

---

## Customization Tips

### Deploy to Different Namespace

To deploy the example to a namespace other than `default`:

```bash
# Create namespace
kubectl create namespace my-app-namespace

# Apply PVCs and ConfigMap to your namespace
kubectl apply -f pvc/ -n my-app-namespace
kubectl apply -f configmap/ -n my-app-namespace

# Update deployment namespace
sed 's/namespace: default/namespace: my-app-namespace/g' deployments/multi-mount-example.yaml | kubectl apply -f -
```

### Mount Subset of NAS Servers

If your application only needs 1-2 NAS servers (not all 6), edit the deployment:

```yaml
# Remove unwanted volumes from spec.volumes section
# Remove corresponding volumeMounts from spec.containers.volumeMounts
# Apply only the needed PVCs
```

Example: Mount only `nas-input-1` and `nas-output-1`:

```bash
# Apply only 2 PVCs
kubectl apply -f pvc/nas-input-1-pvc.yaml -n default
kubectl apply -f pvc/nas-output-1-pvc.yaml -n default

# Edit deployment to remove other 4 volume references
```

### Read-Only Mounts

To mount input NAS servers as read-only (prevent accidental writes):

```yaml
volumeMounts:
  - name: nas-input-1
    mountPath: /mnt/input-1
    readOnly: true  # Change to true
```

### Subdirectory Mounts

To mount a specific subdirectory instead of `/data` root:

```yaml
volumeMounts:
  - name: nas-input-1
    mountPath: /mnt/input-1
    subPath: subfolder-name  # Mounts /data/subfolder-name
```

**Note:** Subdirectory must exist in the NFS export.

### Add nas-backup Server

The example excludes `nas-backup` (typically read-only in production). To include it:

```yaml
volumes:
  - name: nas-backup
    persistentVolumeClaim:
      claimName: nas-backup-pvc
---
volumeMounts:
  - name: nas-backup
    mountPath: /mnt/backup
    readOnly: true  # Backup server often read-only
```

---

## Integration Patterns

This example demonstrates the complete integration stack:

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Storage** | NFS (unfs3) | File sharing protocol |
| **Infrastructure** | Kubernetes Services | DNS-based NAS server discovery |
| **Abstraction** | PersistentVolume | Cluster-wide storage resource |
| **Consumption** | PersistentVolumeClaim | Namespace-scoped storage request |
| **Service Discovery** | ConfigMap | Connection metadata (hosts, ports) |
| **Orchestration** | Deployment | Pod lifecycle management |

**Production equivalence:**
- Dev: Static PV pointing to Minikube NFS service
- Prod: Static PV pointing to physical NAS device IP/hostname

The configuration pattern remains identical between dev and production environments.

---

## Next Steps

After successfully deploying this example:

1. **Adapt for your application:** Use this deployment as a template for your microservice
2. **Add file processing logic:** Replace the busybox container with your application image
3. **Configure environment variables:** Leverage injected ConfigMap values for programmatic NFS access
4. **Review integration guide:** See `../docs/NAS-INTEGRATION-GUIDE.md` for advanced patterns
5. **Test production migration:** Verify PV/PVC manifests work in target OCP environment

---

## Reference

- **PV manifests:** `../pv/`
- **PVC manifests:** `../pvc/`
- **ConfigMap manifest:** `../configmap/`
- **Integration guide:** `../docs/NAS-INTEGRATION-GUIDE.md`
- **Windows setup script:** `../../../../scripts/setup-windows.ps1`
- **Helm values (NAS config):** `../../values-multi-nas.yaml`
