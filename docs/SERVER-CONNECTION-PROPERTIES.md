# File Simulator Server Connection Properties

**Generated:** 2026-02-01
**Cluster IP:** 172.25.201.3
**Cluster Context:** file-simulator

## Multi-NAS Topology (v1.0) - 7 Servers

### NAS Input Servers (3)

#### nas-input-1
- **Purpose:** Input files for processing (server 1)
- **NodePort:** 32150
- **Cluster DNS:** `file-sim-nas-input-1.file-simulator.svc.cluster.local`
- **Cluster IP:** 10.101.141.127
- **NFS Port:** 2049
- **External Access:** `172.25.201.3:32150`
- **Windows Directory:** `C:\simulator-data\nas-input-1\`
- **PV Name:** `nas-input-1-pv`
- **PVC Name:** `nas-input-1-pvc`
- **Export Path:** `/data`
- **Export Options:** `rw,sync,no_root_squash`
- **Sync Pattern:** Init container only (Windows→NFS at pod start)

#### nas-input-2
- **Purpose:** Input files for processing (server 2)
- **NodePort:** 32151
- **Cluster DNS:** `file-sim-nas-input-2.file-simulator.svc.cluster.local`
- **Cluster IP:** 10.107.228.251
- **NFS Port:** 2049
- **External Access:** `172.25.201.3:32151`
- **Windows Directory:** `C:\simulator-data\nas-input-2\`
- **PV Name:** `nas-input-2-pv`
- **PVC Name:** `nas-input-2-pvc`
- **Export Path:** `/data`
- **Export Options:** `rw,sync,no_root_squash`
- **Sync Pattern:** Init container only (Windows→NFS at pod start)

#### nas-input-3
- **Purpose:** Input files for processing (server 3)
- **NodePort:** 32152
- **Cluster DNS:** `file-sim-nas-input-3.file-simulator.svc.cluster.local`
- **Cluster IP:** 10.103.103.161
- **NFS Port:** 2049
- **External Access:** `172.25.201.3:32152`
- **Windows Directory:** `C:\simulator-data\nas-input-3\`
- **PV Name:** `nas-input-3-pv`
- **PVC Name:** `nas-input-3-pvc`
- **Export Path:** `/data`
- **Export Options:** `rw,sync,no_root_squash`
- **Sync Pattern:** Init container only (Windows→NFS at pod start)

### NAS Backup Server (1)

#### nas-backup
- **Purpose:** Backup storage (read-only export)
- **NodePort:** 32153
- **Cluster DNS:** `file-sim-nas-backup.file-simulator.svc.cluster.local`
- **Cluster IP:** 10.100.61.39
- **NFS Port:** 2049
- **External Access:** `172.25.201.3:32153`
- **Windows Directory:** `C:\simulator-data\nas-backup\`
- **PV Name:** `nas-backup-pv`
- **PVC Name:** `nas-backup-pvc`
- **Export Path:** `/data`
- **Export Options:** `ro,sync,no_root_squash` (READ-ONLY)
- **Sync Pattern:** Init container only (Windows→NFS at pod start)

### NAS Output Servers (3)

#### nas-output-1
- **Purpose:** Output files from processing (server 1)
- **NodePort:** 32154
- **Cluster DNS:** `file-sim-nas-output-1.file-simulator.svc.cluster.local`
- **Cluster IP:** 10.101.167.247
- **NFS Port:** 2049
- **External Access:** `172.25.201.3:32154`
- **Windows Directory:** `C:\simulator-data\nas-output-1\`
- **PV Name:** `nas-output-1-pv`
- **PVC Name:** `nas-output-1-pvc`
- **Export Path:** `/data`
- **Export Options:** `rw,sync,no_root_squash`
- **Sync Pattern:** **Bidirectional** (Init: Windows→NFS at pod start, Sidecar: NFS→Windows every 30s)
- **Pod Containers:** 2 (nfs-server + sync-to-windows sidecar)

#### nas-output-2
- **Purpose:** Output files from processing (server 2)
- **NodePort:** 32155
- **Cluster DNS:** `file-sim-nas-output-2.file-simulator.svc.cluster.local`
- **Cluster IP:** 10.96.40.87
- **NFS Port:** 2049
- **External Access:** `172.25.201.3:32155`
- **Windows Directory:** `C:\simulator-data\nas-output-2\`
- **PV Name:** `nas-output-2-pv`
- **PVC Name:** `nas-output-2-pvc`
- **Export Path:** `/data`
- **Export Options:** `rw,sync,no_root_squash`
- **Sync Pattern:** **Bidirectional** (Init: Windows→NFS at pod start, Sidecar: NFS→Windows every 30s)
- **Pod Containers:** 2 (nfs-server + sync-to-windows sidecar)

#### nas-output-3
- **Purpose:** Output files from processing (server 3)
- **NodePort:** 32156
- **Cluster DNS:** `file-sim-nas-output-3.file-simulator.svc.cluster.local`
- **Cluster IP:** 10.99.98.255
- **NFS Port:** 2049
- **External Access:** `172.25.201.3:32156`
- **Windows Directory:** `C:\simulator-data\nas-output-3\`
- **PV Name:** `nas-output-3-pv`
- **PVC Name:** `nas-output-3-pvc`
- **Export Path:** `/data`
- **Export Options:** `rw,sync,no_root_squash`
- **Sync Pattern:** **Bidirectional** (Init: Windows→NFS at pod start, Sidecar: NFS→Windows every 30s)
- **Pod Containers:** 2 (nfs-server + sync-to-windows sidecar)

---

## Connection Summary Table

| Server | Type | NodePort | Cluster DNS | Cluster IP | Windows Path |
|--------|------|----------|-------------|------------|--------------|
| nas-input-1 | Input | 32150 | file-sim-nas-input-1.file-simulator.svc.cluster.local | 10.101.141.127 | C:\simulator-data\nas-input-1 |
| nas-input-2 | Input | 32151 | file-sim-nas-input-2.file-simulator.svc.cluster.local | 10.107.228.251 | C:\simulator-data\nas-input-2 |
| nas-input-3 | Input | 32152 | file-sim-nas-input-3.file-simulator.svc.cluster.local | 10.103.103.161 | C:\simulator-data\nas-input-3 |
| nas-backup | Backup (RO) | 32153 | file-sim-nas-backup.file-simulator.svc.cluster.local | 10.100.61.39 | C:\simulator-data\nas-backup |
| nas-output-1 | Output | 32154 | file-sim-nas-output-1.file-simulator.svc.cluster.local | 10.101.167.247 | C:\simulator-data\nas-output-1 |
| nas-output-2 | Output | 32155 | file-sim-nas-output-2.file-simulator.svc.cluster.local | 10.96.40.87 | C:\simulator-data\nas-output-2 |
| nas-output-3 | Output | 32156 | file-sim-nas-output-3.file-simulator.svc.cluster.local | 10.99.98.255 | C:\simulator-data\nas-output-3 |

---

## Connection Strings for appsettings.json

### From External Applications (Outside Kubernetes)

Use the Minikube IP with NodePort:

```json
{
  "FileSimulator": {
    "NasServers": [
      {
        "Name": "nas-input-1",
        "Type": "Input",
        "Host": "172.25.201.3",
        "Port": 32150,
        "ExportPath": "/data",
        "WindowsPath": "C:\\simulator-data\\nas-input-1",
        "ReadOnly": false
      },
      {
        "Name": "nas-input-2",
        "Type": "Input",
        "Host": "172.25.201.3",
        "Port": 32151,
        "ExportPath": "/data",
        "WindowsPath": "C:\\simulator-data\\nas-input-2",
        "ReadOnly": false
      },
      {
        "Name": "nas-input-3",
        "Type": "Input",
        "Host": "172.25.201.3",
        "Port": 32152,
        "ExportPath": "/data",
        "WindowsPath": "C:\\simulator-data\\nas-input-3",
        "ReadOnly": false
      },
      {
        "Name": "nas-backup",
        "Type": "Backup",
        "Host": "172.25.201.3",
        "Port": 32153,
        "ExportPath": "/data",
        "WindowsPath": "C:\\simulator-data\\nas-backup",
        "ReadOnly": true
      },
      {
        "Name": "nas-output-1",
        "Type": "Output",
        "Host": "172.25.201.3",
        "Port": 32154,
        "ExportPath": "/data",
        "WindowsPath": "C:\\simulator-data\\nas-output-1",
        "ReadOnly": false,
        "BidirectionalSync": true,
        "SyncInterval": 30
      },
      {
        "Name": "nas-output-2",
        "Type": "Output",
        "Host": "172.25.201.3",
        "Port": 32155,
        "ExportPath": "/data",
        "WindowsPath": "C:\\simulator-data\\nas-output-2",
        "ReadOnly": false,
        "BidirectionalSync": true,
        "SyncInterval": 30
      },
      {
        "Name": "nas-output-3",
        "Type": "Output",
        "Host": "172.25.201.3",
        "Port": 32156,
        "ExportPath": "/data",
        "WindowsPath": "C:\\simulator-data\\nas-output-3",
        "ReadOnly": false,
        "BidirectionalSync": true,
        "SyncInterval": 30
      }
    ]
  }
}
```

### From Inside Kubernetes Cluster

Use cluster DNS names (for pods running in any namespace):

```json
{
  "FileSimulator": {
    "NasServers": [
      {
        "Name": "nas-input-1",
        "Type": "Input",
        "Host": "file-sim-nas-input-1.file-simulator.svc.cluster.local",
        "Port": 2049,
        "ExportPath": "/data",
        "PvcName": "nas-input-1-pvc"
      },
      {
        "Name": "nas-input-2",
        "Type": "Input",
        "Host": "file-sim-nas-input-2.file-simulator.svc.cluster.local",
        "Port": 2049,
        "ExportPath": "/data",
        "PvcName": "nas-input-2-pvc"
      },
      {
        "Name": "nas-input-3",
        "Type": "Input",
        "Host": "file-sim-nas-input-3.file-simulator.svc.cluster.local",
        "Port": 2049,
        "ExportPath": "/data",
        "PvcName": "nas-input-3-pvc"
      },
      {
        "Name": "nas-backup",
        "Type": "Backup",
        "Host": "file-sim-nas-backup.file-simulator.svc.cluster.local",
        "Port": 2049,
        "ExportPath": "/data",
        "PvcName": "nas-backup-pvc",
        "ReadOnly": true
      },
      {
        "Name": "nas-output-1",
        "Type": "Output",
        "Host": "file-sim-nas-output-1.file-simulator.svc.cluster.local",
        "Port": 2049,
        "ExportPath": "/data",
        "PvcName": "nas-output-1-pvc",
        "BidirectionalSync": true
      },
      {
        "Name": "nas-output-2",
        "Type": "Output",
        "Host": "file-sim-nas-output-2.file-simulator.svc.cluster.local",
        "Port": 2049,
        "ExportPath": "/data",
        "PvcName": "nas-output-2-pvc",
        "BidirectionalSync": true
      },
      {
        "Name": "nas-output-3",
        "Type": "Output",
        "Host": "file-sim-nas-output-3.file-simulator.svc.cluster.local",
        "Port": 2049,
        "ExportPath": "/data",
        "PvcName": "nas-output-3-pvc",
        "BidirectionalSync": true
      }
    ]
  }
}
```

---

## ConfigMap for Service Discovery

**Available ConfigMap:** `file-simulator-nas-endpoints` (in examples/configmap/)

Contains all 7 NAS servers with environment variables:

```bash
NAS_INPUT_1_HOST=file-sim-nas-input-1.file-simulator.svc.cluster.local
NAS_INPUT_1_PORT=2049
NAS_INPUT_1_PATH=/data
NAS_INPUT_1_NODEPORT=32150
NAS_INPUT_1_PVC=nas-input-1-pvc

# ... repeated for all 7 servers
```

**Deploy to your namespace:**
```bash
# Substitute Minikube IP first
sed -i "s/<minikube-ip>/172.25.201.3/g" helm-chart/file-simulator/examples/configmap/nas-endpoints-configmap.yaml

# Apply to your application namespace
kubectl --context=<your-context> apply -f helm-chart/file-simulator/examples/configmap/nas-endpoints-configmap.yaml -n <your-namespace>
```

**Use in deployment:**
```yaml
envFrom:
  - configMapRef:
      name: file-simulator-nas-endpoints
```

---

## Quick Connection Test

### Test NFS Access from Windows

```powershell
# Test nas-input-1 connectivity
$testFile = "test-$(Get-Date -Format 'yyyyMMddHHmmss').txt"
Set-Content -Path "C:\simulator-data\nas-input-1\$testFile" -Value "Test from Windows"

# Restart pod to trigger init container sync
kubectl --context=file-simulator delete pod -n file-simulator -l app.kubernetes.io/component=nas-input-1

# Wait for pod ready
Start-Sleep -Seconds 10

# Verify file visible via NFS
$pod = kubectl --context=file-simulator get pod -n file-simulator -l app.kubernetes.io/component=nas-input-1 -o jsonpath='{.items[0].metadata.name}'
kubectl --context=file-simulator exec -n file-simulator $pod -c nfs-server -- ls -lh /data/

# Should show your test file
```

### Test Bidirectional Sync on Output Server

```powershell
# Write file via NFS to nas-output-1
$pod = kubectl --context=file-simulator get pod -n file-simulator -l app.kubernetes.io/component=nas-output-1 -o jsonpath='{.items[0].metadata.name}'
kubectl --context=file-simulator exec -n file-simulator $pod -c nfs-server -- sh -c "echo 'Written via NFS' > /data/output-test.txt"

# Wait for sidecar to sync (30-60 seconds)
Start-Sleep -Seconds 35

# Check Windows directory
Get-Content C:\simulator-data\nas-output-1\output-test.txt
# Should show: "Written via NFS"
```

---

## Mount Options for NFS Clients

**Recommended mount options** (matching PV manifests):

```
nfsvers=3,tcp,hard,intr
```

**Explanation:**
- `nfsvers=3` - Use NFSv3 protocol (unfs3 server)
- `tcp` - TCP transport (not UDP)
- `hard` - Hard mount (retries on failure)
- `intr` - Allow interrupt on hung operations

---

## Legacy Services (Original Architecture)

**Single NFS Server:**
- **NodePort:** 32149
- **Cluster DNS:** `file-sim-file-simulator-nas.file-simulator.svc.cluster.local`
- **Export Path:** `/data` (emptyDir - separate from multi-NAS topology)

**Other Protocol Services:** (if deployed)
- FTP: 30021
- SFTP: 30022
- HTTP: 30088
- WebDAV: 30089
- S3 API: 30900
- S3 Console: 30901
- SMB: 445 (LoadBalancer, requires `minikube tunnel`)

---

## Notes

1. **Cluster DNS names** are stable across pod restarts
2. **NodePort values** are fixed in `values-multi-nas.yaml`
3. **Cluster IP addresses** may change on service recreation
4. **Minikube IP** may change on cluster restart (update your configs accordingly)
5. **Windows directories** must exist before pod deployment (run `setup-windows.ps1`)
6. **Output servers** have bidirectional sync with 30-second interval
7. **Input servers** sync Windows→NFS only at pod start (place files before pod starts or restart pod)

---

## Automation

Get current properties programmatically:

```powershell
# Get Minikube IP
$minikubeIp = minikube ip -p file-simulator

# Get all NAS services
kubectl --context=file-simulator get svc -n file-simulator -l simulator.protocol=nfs -o json | ConvertFrom-Json | ForEach-Object {
    $_.items | ForEach-Object {
        [PSCustomObject]@{
            Name = $_.metadata.labels.'app.kubernetes.io/component'
            ClusterDNS = "$($_.metadata.name).file-simulator.svc.cluster.local"
            ClusterIP = $_.spec.clusterIP
            NodePort = $_.spec.ports[0].nodePort
            ExternalAccess = "${minikubeIp}:$($_.spec.ports[0].nodePort)"
        }
    }
} | Format-Table -AutoSize
```

---

**For integration examples, see:** [`DOTNET-K8S-INTEGRATION.md`](DOTNET-K8S-INTEGRATION.md)
