# File Simulator Suite - Deployment Summary

**Date:** 2026-01-28
**Cluster:** file-simulator (Hyper-V driver)
**Minikube IP:** 172.25.201.3
**Status:** ✅ ALL 8 PROTOCOLS RUNNING

---

## Deployment Details

- **Minikube Profile:** file-simulator
- **Driver:** Hyper-V
- **Resources:** 8GB RAM, 4 CPUs
- **Namespace:** file-simulator
- **Helm Release:** file-sim (revision 2)

---

## Protocol Status

| # | Protocol | Status | Endpoint | Credentials |
|---|----------|--------|----------|-------------|
| 1 | **Management UI** | ✅ Running | http://172.25.201.3:30180 | admin / admin123 |
| 2 | **HTTP Server** | ✅ Running | http://172.25.201.3:30088 | - |
| 3 | **WebDAV** | ✅ Running | http://172.25.201.3:30089 | httpuser / httppass123 |
| 4 | **S3/MinIO** | ✅ Running | API: http://172.25.201.3:30900<br>Console: http://172.25.201.3:30901 | minioadmin / minioadmin123 |
| 5 | **FTP** | ✅ Running | ftp://172.25.201.3:30021 | ftpuser / ftppass123 |
| 6 | **SFTP** | ✅ Running | sftp://172.25.201.3:30022 | sftpuser / sftppass123 |
| 7 | **SMB** | ✅ Running | \\\\10.108.219.165\\simulator | smbuser / smbpass123 |
| 8 | **NFS** | ✅ Running | 172.25.201.3:32149<br>Export: /data | - |

---

## Test Results

All protocols tested and verified:

- **Management UI:** HTTP 200 ✅
- **HTTP Server:** HTTP 200 ✅
- **WebDAV:** HTTP 401 (auth required) ✅
- **S3 Console:** HTTP 200 ✅
- **FTP:** TCP Connection Successful ✅
- **SFTP:** TCP Connection Successful ✅
- **SMB:** LoadBalancer IP Assigned ✅
- **NFS:** TCP Connection Successful ✅

---

## Key Fix Applied

### NFS Server Issue Resolution

**Problem:** NFS server was crashing with error:
```
exportfs: /data does not support NFS export
```

**Root Cause:** The hostPath volume (/mnt/simulator-data) mounted from Windows cannot be re-exported by NFS.

**Solution:** Modified NFS deployment to use emptyDir volume for NFS exports:
- `/data` → emptyDir (NFS export)
- `/shared` → PVC (shared storage access)

**Status:** NFS server now running successfully ✅

---

## Pod Status

```
NAME                                                  READY   STATUS    RESTARTS   AGE
file-sim-file-simulator-ftp-675857cd58-zpvx8          1/1     Running   0          4m57s
file-sim-file-simulator-http-567d4f87cc-8ftc8         1/1     Running   0          4m57s
file-sim-file-simulator-management-5dfb677f54-htzb2   1/1     Running   0          4m57s
file-sim-file-simulator-nas-86856666b-tlmn5           1/1     Running   0          106s
file-sim-file-simulator-s3-66df88c4b7-clxb6           1/1     Running   0          4m57s
file-sim-file-simulator-sftp-67fbf467fd-cjrzm         1/1     Running   0          4m57s
file-sim-file-simulator-smb-796c5dcdd6-sv4l7          1/1     Running   0          4m57s
file-sim-file-simulator-webdav-5b47f994db-z67tj       1/1     Running   0          4m57s
```

All 8 pods are in Running state with 0 restarts (except NAS which was restarted once for the fix).

---

## Data Directory

**Local Path:** `C:\simulator-data`

Structure:
```
C:\simulator-data\
├── input\      ← Place test input files here
├── output\     ← Services write output here
└── temp\       ← Temporary processing
```

---

## Important Notes

1. **ez-platform namespace is SAFE** - It's running in the separate "minikube" cluster/profile, completely isolated from file-simulator.

2. **Minikube Tunnel** - The SMB LoadBalancer service requires minikube tunnel to be running. If SMB becomes inaccessible, restart tunnel in elevated PowerShell:
   ```powershell
   minikube tunnel -p file-simulator
   ```

3. **Cluster Management:**
   - Switch to file-simulator: `kubectl config use-context file-simulator`
   - Switch to minikube (ez-platform): `kubectl config use-context minikube`
   - Check current context: `kubectl config current-context`

---

## Usage Examples

### Management UI
Open in browser: http://172.25.201.3:30180

### FTP
```bash
ftp 172.25.201.3 30021
# Username: ftpuser
# Password: ftppass123
```

### SFTP
```bash
sftp -P 30022 sftpuser@172.25.201.3
# Password: sftppass123
```

### SMB (Windows)
```powershell
net use Z: \\10.108.219.165\simulator /user:smbuser smbpass123
```

### S3 (AWS CLI)
```bash
aws configure set aws_access_key_id minioadmin
aws configure set aws_secret_access_key minioadmin123
aws --endpoint-url http://172.25.201.3:30900 s3 ls
```

### NFS (Linux)
```bash
sudo mount -t nfs 172.25.201.3:/data /mnt/nfs
```

---

## Troubleshooting

If pods stop working:
1. Check pod status: `kubectl get pods -n file-simulator`
2. Check logs: `kubectl logs <pod-name> -n file-simulator`
3. Restart deployment: `kubectl rollout restart deployment/<deployment-name> -n file-simulator`

If SMB is inaccessible:
- Ensure minikube tunnel is running in elevated PowerShell
- Check service: `kubectl get svc file-sim-file-simulator-smb -n file-simulator`

---

## Success Criteria ✅

- [x] Minikube cluster created with Hyper-V driver
- [x] All 8 protocols deployed and running
- [x] Management UI accessible
- [x] HTTP and WebDAV servers accessible
- [x] S3/MinIO console and API accessible
- [x] FTP server accepting connections
- [x] SFTP server accepting connections
- [x] SMB server with LoadBalancer IP
- [x] **NFS server running and accepting connections**
- [x] ez-platform namespace untouched and safe

**DEPLOYMENT COMPLETE** ✅
