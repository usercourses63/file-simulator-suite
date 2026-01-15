# Client Cluster Configuration Guide

This guide shows how to configure your application in a separate Kubernetes cluster to access the File Simulator services.

## Prerequisites

1. **File Simulator deployed** in its own cluster (e.g., `file-simulator` minikube profile)
2. **Network connectivity** between clusters (both on same Hyper-V switch, or proper network routing)
3. **Node IP** of the file-simulator cluster: `minikube ip --profile file-simulator`

## Quick Start

```bash
# 1. Ensure minikube tunnel is running (for SMB LoadBalancer)
minikube tunnel --profile file-simulator &

# 2. Get the file-simulator cluster IP and SMB LoadBalancer IP
SIMULATOR_IP=$(minikube ip --profile file-simulator)
SMB_LB_IP=$(kubectl --context file-simulator -n file-simulator get svc file-sim-file-simulator-smb -o jsonpath='{.spec.clusterIP}')
echo "Simulator IP: $SIMULATOR_IP"
echo "SMB LoadBalancer IP: $SMB_LB_IP"

# 3. Update IPs in configmap (edit 02-configmap.yaml or use sed)
sed -i "s/172.23.17.71/$SIMULATOR_IP/g" *.yaml
sed -i "s/10.101.173.233/$SMB_LB_IP/g" *.yaml

# 4. Deploy to your application cluster
kubectl apply -k .

# 5. Add route for SMB LoadBalancer access (required for SMB)
minikube ssh --profile app-client -- "sudo ip route add 10.96.0.0/12 via $SIMULATOR_IP"

# 6. Verify connectivity (all 17 tests should pass!)
kubectl -n my-app exec deploy/file-test-console -- dotnet FileSimulator.TestConsole.dll --cross-protocol
```

## Architecture

```
┌──────────────────────────┐         ┌──────────────────────────────────────┐
│   Your App Cluster       │         │      File Simulator Cluster          │
│   (app-client)           │         │      (file-simulator)                │
├──────────────────────────┤         ├──────────────────────────────────────┤
│                          │         │                                      │
│  ┌─────────────────────┐ │   NFS   │  ┌─────┐ ┌──────┐ ┌──────┐         │
│  │   Your Application  │ │ ◄──────►│  │ NFS │ │ FTP  │ │ SFTP │         │
│  │                     │ │  :32149 │  │32149│ │30021 │ │30022 │         │
│  │  ┌───────────────┐  │ │         │  └──┬──┘ └──┬───┘ └──┬───┘         │
│  │  │ /mnt/nfs      │  │ │   FTP   │     │       │        │             │
│  │  │ (PVC mount)   │  │ │ ◄──────►│     └───────┴────────┘             │
│  │  └───────────────┘  │ │  :30021 │              │                      │
│  │                     │ │         │     ┌────────┴───────┐              │
│  │  ConfigMap with     │ │  SFTP   │     │  Shared PVC    │              │
│  │  service endpoints  │ │ ◄──────►│     │  (hostPath)    │              │
│  └─────────────────────┘ │  :30022 │     └────────────────┘              │
│                          │         │                                      │
└──────────────────────────┘         └──────────────────────────────────────┘
```

## Files

| File | Purpose |
|------|---------|
| `01-namespace.yaml` | Creates `my-app` namespace |
| `02-configmap.yaml` | Service endpoints & appsettings.json |
| `03-nfs-storage.yaml` | PV/PVC for NFS mount |
| `04-test-console-deployment.yaml` | Test console deployment |
| `05-example-app-deployment.yaml` | Template for your app |
| `kustomization.yaml` | Kustomize for easy deployment |

## Protocol Configuration

### NFS (Recommended for file sharing)

NFS provides direct filesystem access with the best performance for large files.

**PersistentVolume Configuration:**
```yaml
spec:
  nfs:
    server: 172.23.17.71    # File-simulator node IP
    path: /                  # NFSv4 root (fsid=0)
  mountOptions:
    - nfsvers=4              # NFSv4 only (single port)
    - port=32149             # NFS NodePort
    - nolock                 # Avoid lockd
    - soft                   # Better error handling
    - timeo=30               # 3 second timeout
```

**Why path is `/` not `/data`:**
The NFS server exports `/data` with `fsid=0`, making it the NFSv4 pseudo-root. Clients mount `/` to access the exported directory.

### FTP

```yaml
env:
  - name: FTP_HOST
    value: "172.23.17.71"
  - name: FTP_PORT
    value: "30021"
  - name: FTP_USERNAME
    value: "ftpuser"
  - name: FTP_PASSWORD
    value: "ftppass123"
```

### SFTP

```yaml
env:
  - name: SFTP_HOST
    value: "172.23.17.71"
  - name: SFTP_PORT
    value: "30022"
  - name: SFTP_USERNAME
    value: "sftpuser"
  - name: SFTP_PASSWORD
    value: "sftppass123"
```

### HTTP/WebDAV

```yaml
env:
  - name: HTTP_BASE_URL
    value: "http://172.23.17.71:30088"
  - name: WEBDAV_BASE_URL
    value: "http://172.23.17.71:30089"
```

### S3/MinIO

```yaml
env:
  - name: S3_ENDPOINT
    value: "http://172.23.17.71:30900"
  - name: S3_ACCESS_KEY
    value: "minioadmin"
  - name: S3_SECRET_KEY
    value: "minioadmin123"
  - name: S3_BUCKET
    value: "simulator"
```

**Note:** Create the bucket first:
```bash
kubectl -n file-simulator exec deploy/file-sim-file-simulator-s3 -- \
  /bin/sh -c "mc alias set local http://localhost:9000 minioadmin minioadmin123 && mc mb local/simulator --ignore-existing"
```

### SMB (Requires LoadBalancer + Standard Port 445)

SMB requires port 445 (standard) - NodePort won't work with SMBLibrary. Use LoadBalancer with minikube tunnel.

**Prerequisites:**
1. `minikube tunnel --profile file-simulator` running on host
2. Route added in client cluster VM (see setup below)

**Setup for cross-cluster SMB:**
```bash
# 1. Ensure minikube tunnel is running on file-simulator cluster
minikube tunnel --profile file-simulator

# 2. Get the SMB LoadBalancer IP
SMB_IP=$(kubectl --context file-simulator -n file-simulator get svc file-sim-file-simulator-smb -o jsonpath='{.spec.clusterIP}')
echo "SMB LoadBalancer IP: $SMB_IP"

# 3. Add route in app-client VM to reach LoadBalancer IPs
SIMULATOR_IP=$(minikube ip --profile file-simulator)
minikube ssh --profile app-client -- "sudo ip route add 10.96.0.0/12 via $SIMULATOR_IP"
```

**Configuration:**
```yaml
env:
  - name: SMB_HOST
    value: "10.101.173.233"  # LoadBalancer IP (NOT node IP!)
  - name: SMB_PORT
    value: "445"             # Standard port (NOT NodePort!)
  - name: SMB_SHARE
    value: "simulator"       # Share name from Samba config
```

**Why this works:**
- LoadBalancer exposes SMB on standard port 445
- minikube tunnel routes traffic from host to LoadBalancer IPs
- Route in client VM enables cross-cluster access to LoadBalancer network

## .NET Application Configuration

For .NET applications, mount the ConfigMap's `appsettings.json`:

```yaml
volumeMounts:
  - name: config
    mountPath: /app/appsettings.Production.json
    subPath: appsettings.json

volumes:
  - name: config
    configMap:
      name: file-simulator-config
      items:
        - key: appsettings.json
          path: appsettings.json
```

Then in your `Program.cs`:
```csharp
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Production.json", optional: true);
```

## Test Results

From app-client cluster to file-simulator:

| Protocol | Status | Notes |
|----------|--------|-------|
| FTP | ✅ Working | NodePort 30021 |
| SFTP | ✅ Working | NodePort 30022 |
| HTTP | ✅ Working | NodePort 30088 |
| WebDAV | ✅ Working | NodePort 30089 |
| S3/MinIO | ✅ Working | NodePort 30900 |
| NFS | ✅ Working | NodePort 32149 (NFSv4) |
| SMB | ✅ Working | LoadBalancer IP + port 445 |

## Troubleshooting

### NFS mount fails with "access denied"
- Ensure you're using `path: /` not `path: /data`
- Verify `nfsvers=4` and correct `port=` in mount options

### S3 "bucket does not exist"
- Create the bucket first (see S3 section above)

### SMB connection fails
- **Use port 445** (standard), not NodePort 30445 - SMBLibrary requires standard port
- **Use LoadBalancer IP**, not node IP - SMB LoadBalancer routes to correct pod
- **Run minikube tunnel** on file-simulator cluster
- **Add route** in client VM: `minikube ssh --profile app-client -- "sudo ip route add 10.96.0.0/12 via <simulator-ip>"`
- **Share name is `simulator`**, not `data`

### Cannot reach file-simulator services
- Verify clusters are on same network: `ping <file-simulator-ip>`
- Check NodePort services are exposed: `kubectl -n file-simulator get svc`
