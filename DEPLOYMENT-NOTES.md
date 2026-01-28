# File Simulator Suite - Deployment Notes

**Last Updated:** 2026-01-28
**Cluster:** file-simulator (Hyper-V)
**Status:** ✅ Production-ready, all 8 protocols tested

---

## Verified Deployment Configuration

### Cluster Specifications
- **Minikube Profile:** `file-simulator`
- **Driver:** Hyper-V (required for SMB LoadBalancer support)
- **IP Address:** 172.25.201.3 (example - varies per deployment)
- **Resources:** 8GB RAM, 4 CPUs, 20GB disk
- **Mount:** C:\simulator-data → /mnt/simulator-data

### Deployment Command (Tested ✅)
```powershell
minikube start `
    --profile file-simulator `
    --driver=hyperv `
    --memory=8192 `
    --cpus=4 `
    --disk-size=20g `
    --mount `
    --mount-string="C:\simulator-data:/mnt/simulator-data"
```

---

## Critical Multi-Profile Setup

### The Context Switching Problem

**Issue:** When running multiple Minikube profiles (e.g., ez-platform on Docker + file-simulator on Hyper-V), using `kubectl config use-context` to switch between clusters leads to:
- ❌ Accidental deletions on wrong cluster
- ❌ Confusion about which cluster is active
- ❌ Lost deployments from incorrect operations
- ❌ Resource cleanup in wrong namespace

### The Solution: Always Use --context Flag

**✅ SAFE PRACTICE:**
```powershell
# File simulator operations - ALWAYS include --context
kubectl --context=file-simulator get pods -n file-simulator
kubectl --context=file-simulator logs <pod> -n file-simulator
kubectl --context=file-simulator delete pod <name> -n file-simulator
kubectl --context=file-simulator apply -f deployment.yaml

# ez-platform operations - ALWAYS include --context
kubectl --context=minikube get pods -n ez-platform
kubectl --context=minikube logs <pod> -n ez-platform
kubectl --context=minikube delete pod <name> -n ez-platform
```

**❌ DANGEROUS PRACTICE:**
```powershell
# DO NOT switch contexts manually!
kubectl config use-context file-simulator  # ❌ Error-prone
kubectl delete pod <name>                  # ❌ Which cluster? Unknown!

kubectl config use-context minikube        # ❌ Easy to forget
kubectl delete namespace file-simulator    # ❌ DISASTER if wrong cluster!
```

### Helm Operations with --kube-context

```powershell
# Deploy to file-simulator cluster
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --kube-context=file-simulator `
    --namespace file-simulator

# Deploy to minikube cluster
helm upgrade --install my-app ./my-chart `
    --kube-context=minikube `
    --namespace ez-platform

# List releases (specify context!)
helm list --kube-context=file-simulator -A
helm list --kube-context=minikube -A
```

### PowerShell Helper Functions

Add these to your PowerShell profile (`notepad $PROFILE`):

```powershell
# File Simulator shortcuts (Hyper-V cluster)
function k-fs {
    kubectl --context=file-simulator --namespace=file-simulator $args
}
function helm-fs {
    helm --kube-context=file-simulator --namespace=file-simulator $args
}

# ez-platform shortcuts (Docker cluster)
function k-ez {
    kubectl --context=minikube --namespace=ez-platform $args
}
function helm-ez {
    helm --kube-context=minikube --namespace=ez-platform $args
}

# Minikube shortcuts
function m-fs {
    minikube -p file-simulator $args
}
function m-ez {
    minikube -p minikube $args
}

# Usage examples:
# k-fs get pods              → kubectl --context=file-simulator -n file-simulator get pods
# k-ez get svc               → kubectl --context=minikube -n ez-platform get svc
# m-fs ip                    → minikube ip -p file-simulator
# helm-fs list               → helm list --kube-context=file-simulator -n file-simulator
```

### Terminal Organization

**Recommended setup:**
1. **Terminal 1:** For file-simulator operations
   - Label: "FILE-SIMULATOR (Hyper-V)"
   - Always use: `k-fs` or `--context=file-simulator`

2. **Terminal 2:** For ez-platform operations
   - Label: "EZ-PLATFORM (Docker)"
   - Always use: `k-ez` or `--context=minikube`

3. **Terminal 3:** Administrator terminal for minikube tunnel
   - Run: `minikube tunnel -p file-simulator`
   - Keep running for SMB access

---

## NFS Server Configuration & Fix

### The Problem

NFS server fails to start with error:
```
exportfs: /data does not support NFS export
----> ERROR: /usr/sbin/exportfs failed
```

### Root Cause

The NFS server container (erichough/nfs-server) cannot export a Windows-mounted hostPath volume:
- Windows mount: C:\simulator-data → /mnt/simulator-data (in Minikube VM)
- NFS tries to export: /data (mounted from /mnt/simulator-data via PVC)
- **Conflict:** NFS requires native filesystem to create export tables, lock files, and state data
- Windows-mounted CIFS/9p filesystems don't support NFS export features

### The Solution: Dual Volume Mount

**Patch file** (`nfs-fix-patch.yaml`):
```yaml
spec:
  template:
    spec:
      containers:
      - name: nfs-server
        volumeMounts:
        - name: nfs-data
          mountPath: /data          # NFS export directory (emptyDir)
        - name: shared-data
          mountPath: /shared        # Shared storage (PVC)
      volumes:
      - name: nfs-data
        emptyDir: {}               # Ephemeral storage for NFS daemon
      - name: shared-data
        persistentVolumeClaim:
          claimName: file-sim-file-simulator-pvc  # Windows mount
```

**How it works:**
1. **emptyDir at /data:** NFS server uses this for exports, state files, locks
2. **PVC at /shared:** Links to shared Windows storage for cross-protocol access
3. NFS clients mount `/data` and see the ephemeral storage
4. Applications can copy files between `/data` and `/shared` as needed

### Applying the Fix

```powershell
# Create patch file (run once)
@"
spec:
  template:
    spec:
      containers:
      - name: nfs-server
        volumeMounts:
        - name: nfs-data
          mountPath: /data
        - name: shared-data
          mountPath: /shared
      volumes:
      - name: nfs-data
        emptyDir: {}
      - name: shared-data
        persistentVolumeClaim:
          claimName: file-sim-file-simulator-pvc
"@ | Out-File -FilePath nfs-fix-patch.yaml -Encoding UTF8

# Apply patch to NFS deployment
kubectl --context=file-simulator patch deployment file-sim-file-simulator-nas `
    -n file-simulator `
    --patch-file nfs-fix-patch.yaml

# Wait for rollout
kubectl --context=file-simulator rollout status deployment/file-sim-file-simulator-nas -n file-simulator

# Verify NFS is running
kubectl --context=file-simulator logs -n file-simulator -l app.kubernetes.io/component=nas --tail=15
```

**Expected log output after fix:**
```
==================================================================
      SERVER STARTUP COMPLETE
==================================================================
----> list of enabled NFS protocol versions: 4.2, 4.1, 4
----> list of container exports:
---->   /data	*(rw,sync,no_root_squash,...)
----> list of container ports that should be exposed: 2049 (TCP)

==================================================================
      READY AND WAITING FOR NFS CLIENT CONNECTIONS
==================================================================
```

### NFS File Transfer Testing

**Verification test:**
```powershell
# Create test pod that mounts NFS and writes a file
kubectl --context=file-simulator exec -n file-simulator <nas-pod-name> -- sh -c "echo 'NFS Test' > /data/test.txt && cat /data/test.txt"

# Expected output: "NFS Test"
```

**Client-side test from another pod:**
```powershell
kubectl --context=file-simulator apply -f - <<EOF
apiVersion: v1
kind: Pod
metadata:
  name: nfs-client-test
  namespace: file-simulator
spec:
  containers:
  - name: test
    image: alpine
    command: ['sh', '-c', 'apk add nfs-utils && mkdir -p /mnt/nfs && mount -t nfs file-sim-file-simulator-nas.file-simulator.svc.cluster.local:/data /mnt/nfs && echo "Client test" > /mnt/nfs/client.txt && cat /mnt/nfs/client.txt && ls -lh /mnt/nfs/ && sleep 3600']
  restartPolicy: Never
EOF

# Check logs for success
kubectl --context=file-simulator logs nfs-client-test -n file-simulator

# Cleanup
kubectl --context=file-simulator delete pod nfs-client-test -n file-simulator
```

**Expected behavior:**
- ✅ File written to /data/client.txt
- ✅ File content readable
- ✅ Directory listing shows files
- ✅ NFS mount successful

### Cross-Protocol File Sharing Limitation

**Important:** Due to the NFS fix, files are NOT automatically shared between NFS and other protocols:
- **NFS exports:** `/data` (emptyDir - ephemeral)
- **Other protocols:** `/data` (PVC - persistent via Windows mount)

**Workaround for shared access:**
If you need files accessible via both NFS AND other protocols, you must:
1. Copy files from `/shared` (PVC) to `/data` (NFS export) inside the NFS pod
2. Use a sidecar container or init container to sync directories
3. Or use NFS exclusively for that use case

**Alternative:** Use other protocols (FTP, SFTP, SMB, HTTP, S3) which all share the same PVC mount.

---

## Resource Requirements & Sizing

### Minimum Configuration (Tested)
- **Memory:** 8GB
- **CPUs:** 4
- **Disk:** 20GB
- **Status:** ✅ All 8 protocols stable

### Resource Breakdown by Service

| Service | CPU Request | CPU Limit | RAM Request | RAM Limit |
|---------|-------------|-----------|-------------|-----------|
| Management UI | 50m | 200m | 64Mi | 256Mi |
| FTP | 50m | 200m | 64Mi | 256Mi |
| SFTP | 50m | 200m | 64Mi | 256Mi |
| HTTP | 50m | 200m | 64Mi | 256Mi |
| WebDAV | 25m | 100m | 32Mi | 128Mi |
| S3/MinIO | 100m | 500m | 256Mi | 1Gi |
| SMB | 100m | 300m | 128Mi | 512Mi |
| NFS | 50m | 200m | 64Mi | 256Mi |
| **TOTAL** | **575m** | **1900m** | **706Mi** | **2.85Gi** |

**Analysis:**
- Uses ~15% of 4 CPUs (requests) to ~48% (limits)
- Uses ~9% of 8GB RAM (requests) to ~35% (limits)
- Comfortable headroom for Kubernetes system overhead
- Can coexist with other applications if resources planned carefully

---

## Deployment Checklist

### Pre-Deployment
- [ ] Hyper-V enabled on Windows host
- [ ] Minikube 1.32+ installed
- [ ] kubectl 1.28+ installed
- [ ] Helm 3.x installed
- [ ] C:\simulator-data directory created with permissions
- [ ] Unique Minikube profile name chosen (`file-simulator` recommended)

### Deployment Steps
- [ ] Create Minikube cluster with Hyper-V driver (8GB/4CPU minimum)
- [ ] Verify cluster is running: `minikube status -p file-simulator`
- [ ] Deploy Helm chart to file-simulator namespace
- [ ] **Apply NFS fix patch** (critical - will crash without this)
- [ ] Start minikube tunnel for SMB (in separate Admin terminal)
- [ ] Verify all 8 pods are Running (1/1 READY)

### Post-Deployment Verification
- [ ] Test Management UI: http://\<IP\>:30180
- [ ] Test HTTP server: http://\<IP\>:30088
- [ ] Test S3 console: http://\<IP\>:30901
- [ ] Test FTP port: 30021 (TCP connection)
- [ ] Test SFTP port: 30022 (TCP connection)
- [ ] Test NFS port: 32149 (TCP connection)
- [ ] Test SMB LoadBalancer IP assigned
- [ ] Test WebDAV auth: HTTP 401 on port 30089
- [ ] **Test NFS file operations** (write/read/list)

---

## Common Issues & Solutions

### Issue 1: NFS Pod in CrashLoopBackOff

**Symptom:**
```
pod/file-sim-file-simulator-nas-xxx   0/1   CrashLoopBackOff
```

**Cause:** NFS cannot export hostPath-mounted volume from Windows

**Fix:** Apply nfs-fix-patch.yaml (see NFS Server Configuration section above)

**Verification:**
```powershell
kubectl --context=file-simulator get pods -n file-simulator | Select-String nas
# Should show: 1/1 Running
```

---

### Issue 2: Pods Stuck in Pending (Insufficient Resources)

**Symptom:**
```
pod/file-sim-file-simulator-xxx   0/1   Pending
```

**Cause:** Cluster has insufficient memory or CPU

**Fix:** Recreate cluster with more resources:
```powershell
minikube delete -p file-simulator
minikube start -p file-simulator --driver=hyperv --memory=8192 --cpus=4 --disk-size=20g `
    --mount --mount-string="C:\simulator-data:/mnt/simulator-data"
```

---

### Issue 3: Accidental Deletion from Wrong Cluster

**Symptom:** Resources disappear after kubectl delete command

**Cause:** Forgot to specify `--context` flag, operated on wrong cluster

**Prevention:**
- ✅ ALWAYS use `--context=<profile-name>` flag
- ✅ Use PowerShell helper functions (k-fs, k-ez)
- ✅ Never use `kubectl config use-context`

**Recovery:**
```powershell
# Redeploy file-simulator
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --kube-context=file-simulator `
    --namespace file-simulator

# Apply NFS fix
kubectl --context=file-simulator patch deployment file-sim-file-simulator-nas `
    -n file-simulator --patch-file nfs-fix-patch.yaml
```

---

### Issue 4: SMB Not Accessible

**Symptom:** `net use` fails or SMB service shows `<pending>` for EXTERNAL-IP

**Cause:** minikube tunnel not running

**Fix:**
```powershell
# Start tunnel in Administrator PowerShell (keep running)
minikube tunnel -p file-simulator

# Verify LoadBalancer IP assigned
kubectl --context=file-simulator get svc file-sim-file-simulator-smb -n file-simulator

# Should show EXTERNAL-IP column populated
```

---

### Issue 5: Mount Not Working - Files Not Visible

**Symptom:** Files placed in C:\simulator-data not visible in pods

**Cause:** Minikube mount not started or permissions issue

**Fix:**
```powershell
# Restart cluster with mount
minikube stop -p file-simulator
minikube start -p file-simulator --mount --mount-string="C:\simulator-data:/mnt/simulator-data"

# Verify mount from inside cluster
minikube ssh -p file-simulator -- ls -la /mnt/simulator-data

# Should show input/, output/, temp/ directories

# Check permissions on Windows
icacls C:\simulator-data /grant Everyone:F /T
```

---

## Deployment Recovery Procedure

If the deployment is lost or corrupted, follow this procedure:

```powershell
# 1. Ensure cluster is running
minikube status -p file-simulator
# If stopped: minikube start -p file-simulator

# 2. Remove old deployment (if exists)
helm uninstall file-sim --kube-context=file-simulator -n file-simulator 2>$null
kubectl --context=file-simulator delete namespace file-simulator 2>$null

# 3. Wait for cleanup
Start-Sleep -Seconds 10

# 4. Redeploy
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --kube-context=file-simulator `
    --namespace file-simulator `
    --create-namespace

# 5. Apply NFS fix (CRITICAL - do not skip!)
kubectl --context=file-simulator patch deployment file-sim-file-simulator-nas `
    -n file-simulator `
    --patch-file nfs-fix-patch.yaml

# 6. Wait for all pods
Start-Sleep -Seconds 30
kubectl --context=file-simulator get pods -n file-simulator

# 7. Start tunnel for SMB
# In separate Admin terminal: minikube tunnel -p file-simulator

# 8. Verify all services
$SIMULATOR_IP = minikube ip -p file-simulator
curl http://${SIMULATOR_IP}:30180  # Management UI
curl http://${SIMULATOR_IP}:30901  # S3 Console
```

---

## Service URLs Quick Reference

```powershell
# Get current IP
$IP = minikube ip -p file-simulator
Write-Host "Simulator IP: $IP"

# Service URLs
Write-Host @"

Management UI: http://${IP}:30180 (admin/admin123)
HTTP Server:   http://${IP}:30088
WebDAV:        http://${IP}:30089 (httpuser/httppass123)
S3 Console:    http://${IP}:30901 (minioadmin/minioadmin123)
S3 API:        http://${IP}:30900

FTP:           ftp://${IP}:30021 (ftpuser/ftppass123)
SFTP:          sftp://${IP}:30022 (sftpuser/sftppass123)
NFS:           ${IP}:32149 (mount -t nfs ${IP}:/data /mnt/nfs)
SMB:           \\<LoadBalancer-IP>\simulator (smbuser/smbpass123)

"@
```

---

## Monitoring & Health Checks

### Check Cluster Health

```powershell
# Overall cluster status
minikube status -p file-simulator

# All pods status
kubectl --context=file-simulator get pods -n file-simulator

# Service endpoints
kubectl --context=file-simulator get svc -n file-simulator

# Storage status
kubectl --context=file-simulator get pvc,pv -n file-simulator
```

### Check Logs

```powershell
# All pods
kubectl --context=file-simulator logs -n file-simulator -l app.kubernetes.io/part-of=file-simulator-suite --tail=20

# Specific service
kubectl --context=file-simulator logs -n file-simulator -l app.kubernetes.io/component=nas --tail=50
kubectl --context=file-simulator logs -n file-simulator -l app.kubernetes.io/component=ftp --tail=50
kubectl --context=file-simulator logs -n file-simulator -l app.kubernetes.io/component=smb --tail=50
```

### Resource Usage

```powershell
# Enable metrics-server first
minikube addons enable metrics-server -p file-simulator

# Wait for metrics to be available
Start-Sleep -Seconds 30

# Check node resources
kubectl --context=file-simulator top nodes

# Check pod resources
kubectl --context=file-simulator top pods -n file-simulator
```

---

## Files Created During Setup

| File | Purpose |
|------|---------|
| `nfs-fix-patch.yaml` | NFS server fix (emptyDir volume patch) |
| `DEPLOYMENT-SUMMARY.md` | Initial deployment documentation |
| `VERIFICATION-COMPLETE.md` | Re-deployment verification report |
| `test-all-protocols.ps1` | PowerShell protocol testing script |
| `DEPLOYMENT-NOTES.md` | This file - comprehensive deployment guide |

---

## Integration with Other Systems

### From ez-platform (Docker cluster) to file-simulator (Hyper-V cluster)

**Get file-simulator IP:**
```powershell
$SIMULATOR_IP = minikube ip -p file-simulator
```

**Configure ez-platform services to use file-simulator:**
```powershell
# Create ConfigMap in ez-platform namespace (minikube cluster)
kubectl --context=minikube apply -f - <<EOF
apiVersion: v1
kind: ConfigMap
metadata:
  name: file-simulator-endpoints
  namespace: ez-platform
data:
  FTP_HOST: "$SIMULATOR_IP"
  FTP_PORT: "30021"
  SFTP_HOST: "$SIMULATOR_IP"
  SFTP_PORT: "30022"
  S3_ENDPOINT: "http://$SIMULATOR_IP:30900"
  HTTP_ENDPOINT: "http://$SIMULATOR_IP:30088"
  NFS_SERVER: "$SIMULATOR_IP"
  NFS_PORT: "32149"
EOF
```

**Test connectivity from ez-platform:**
```powershell
# Run test pod in ez-platform namespace
kubectl --context=minikube run test --rm -it --image=alpine -n ez-platform -- sh

# Inside pod:
apk add curl
curl http://172.25.201.3:30088/       # HTTP server
curl http://172.25.201.3:30901/       # S3 console

# Test TCP ports
nc -zv 172.25.201.3 30021             # FTP
nc -zv 172.25.201.3 30022             # SFTP
nc -zv 172.25.201.3 32149             # NFS
```

---

## Best Practices Summary

1. ✅ **Always use `--context` flag** in kubectl/helm commands
2. ✅ **Apply NFS fix immediately** after deployment
3. ✅ **Use 8GB/4CPU minimum** for stable operation
4. ✅ **Keep minikube tunnel running** for SMB access
5. ✅ **Use PowerShell helper functions** to enforce safety
6. ✅ **One namespace per profile** (file-simulator in Hyper-V, ez-platform in Docker)
7. ✅ **Label terminals** by cluster/namespace
8. ✅ **Test all 8 protocols** after deployment
9. ✅ **Document your cluster IPs** (they change on cluster recreation)
10. ✅ **Keep nfs-fix-patch.yaml** in source control

---

## Success Indicators

After deployment, you should see:

```powershell
kubectl --context=file-simulator get pods -n file-simulator
```

```
NAME                                                  READY   STATUS    RESTARTS   AGE
file-sim-file-simulator-ftp-xxx                       1/1     Running   0          XXm
file-sim-file-simulator-http-xxx                      1/1     Running   0          XXm
file-sim-file-simulator-management-xxx                1/1     Running   0          XXm
file-sim-file-simulator-nas-xxx                       1/1     Running   0          XXm
file-sim-file-simulator-s3-xxx                        1/1     Running   0          XXm
file-sim-file-simulator-sftp-xxx                      1/1     Running   0          XXm
file-sim-file-simulator-smb-xxx                       1/1     Running   0          XXm
file-sim-file-simulator-webdav-xxx                    1/1     Running   0          XXm
```

**All 8 pods: STATUS=Running, READY=1/1, RESTARTS=0** ✅

---

**End of Deployment Notes**
