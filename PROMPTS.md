# Claude Code Quick-Start Prompts
# Copy-paste these prompts into Claude Code in order

## ============================================
## SINGLE MEGA-PROMPT (Alternative to step-by-step)
## Use this if you want Claude Code to do everything at once
## ============================================

```
Implement the complete File Simulator Suite for Kubernetes.

## Context
I'm building a file access simulator that provides FTP, SFTP, HTTP/WebDAV, S3/MinIO, SMB, and NFS 
protocols in Minikube, allowing my microservices to work identically to OCP production while 
enabling Windows testers to supply input files and retrieve outputs.

## Project Structure (already created)
- helm-chart/file-simulator/ - Kubernetes Helm chart
- src/FileSimulator.Client/ - .NET 9.0 client library
- scripts/ - PowerShell setup and test scripts

## Requirements

### 1. Helm Chart
Create a complete Helm chart with:
- Namespace: file-simulator
- Shared PVC with hostPath /mnt/simulator-data
- Services (all with NodePort for Minikube access):
  - FileBrowser Management UI (:30080)
  - FTP Server - vsftpd (:30021)
  - SFTP Server - atmoz/sftp (:30022)
  - HTTP/WebDAV - nginx (:30088)
  - S3/MinIO (:30900 API, :30901 Console)
  - SMB/Samba (:30445)
  - NFS Server (:32049)

### 2. .NET Client Library
Create services implementing IFileProtocolService interface with methods:
- DiscoverFilesAsync(path, pattern) - for polling
- ReadFileAsync(remotePath) - download to memory
- DownloadFileAsync(remotePath, localPath) - download to disk
- WriteFileAsync(remotePath, content) - upload from memory
- UploadFileAsync(localPath, remotePath) - upload from disk
- DeleteFileAsync(remotePath)
- HealthCheckAsync()

Protocol implementations needed:
- FtpFileService (using FluentFTP)
- SftpFileService (using SSH.NET)
- S3FileService (using AWSSDK.S3)
- HttpFileService (using HttpClient)
- SmbFileService (using SMBLibrary)
- NfsFileService (using mounted filesystem)

Also create:
- FilePollingService with Quartz.NET for scheduled polling
- ServiceCollectionExtensions for ASP.NET Core DI
- Complete example microservice with all protocols

### 3. Scripts
- test-simulator.ps1 - Verify all protocols work
- setup-windows.ps1 - Create C:\simulator-data and configure mount

## Technology Stack
- .NET 9.0, ASP.NET Core Minimal API
- Quartz.NET for scheduling
- MassTransit with RabbitMQ for messaging
- Helm 3.x for Kubernetes deployment

Please implement all components, starting with the Helm chart, then the .NET library.
```

## ============================================
## STEP-BY-STEP PROMPTS (Recommended for control)
## ============================================

### Phase 1: Helm Chart

# Prompt 1 - Chart Base
```
Create the Helm chart base files for File Simulator Suite:

1. helm-chart/file-simulator/Chart.yaml
2. helm-chart/file-simulator/values.yaml - Include configs for all protocols
3. helm-chart/file-simulator/templates/_helpers.tpl

Protocols to support: FTP, SFTP, HTTP/WebDAV, S3/MinIO, SMB, NFS
Each should have configurable NodePort for Minikube access.
```

# Prompt 2 - Infrastructure
```
Create infrastructure templates:

1. helm-chart/file-simulator/templates/namespace.yaml
2. helm-chart/file-simulator/templates/storage.yaml - PV with hostPath /mnt/simulator-data, PVC
3. helm-chart/file-simulator/templates/serviceaccount.yaml
```

# Prompt 3 - All Services (combine to save time)
```
Create all service deployment templates:

1. templates/management.yaml - FileBrowser (filebrowser/filebrowser:v2.27.0, NodePort 30080)
2. templates/ftp.yaml - vsftpd (fauria/vsftpd, NodePort 30021)
3. templates/sftp.yaml - OpenSSH (atmoz/sftp:alpine, NodePort 30022)
4. templates/http.yaml - nginx with WebDAV (nginx:alpine, NodePort 30088)
5. templates/s3.yaml - MinIO (minio/minio, NodePort 30900/30901)
6. templates/smb.yaml - Samba (dperson/samba, NodePort 30445)
7. templates/nas.yaml - NFS (itsthenetwork/nfs-server-alpine, NodePort 32049)
8. templates/NOTES.txt - Post-install instructions

Each service should:
- Mount the shared PVC
- Have health checks where applicable
- Use credentials from values.yaml
```

### Phase 2: .NET Library

# Prompt 4 - Protocol Services
```
Create the file protocol service implementations:

File: src/FileSimulator.Client/Services/FileProtocolServices.cs

Create IFileProtocolService interface and implementations for:
1. FtpFileService - using FluentFTP
2. SftpFileService - using SSH.NET  
3. S3FileService - using AWSSDK.S3
4. HttpFileService - using HttpClient
5. SmbFileService - using SMBLibrary
6. NfsFileService - using System.IO (mounted filesystem)

Each must implement:
- DiscoverFilesAsync(path, pattern)
- ReadFileAsync(remotePath)
- DownloadFileAsync(remotePath, localPath)
- WriteFileAsync(remotePath, content)
- UploadFileAsync(localPath, remotePath)
- DeleteFileAsync(remotePath)
- HealthCheckAsync()

Include Options classes for each protocol.
```

# Prompt 5 - Polling Service
```
Create the file polling service:

File: src/FileSimulator.Client/Services/FilePollingService.cs

Create:
1. FilePollingEndpoint - config for each endpoint (Name, Protocol, Path, Pattern, CronSchedule)
2. FilePollingOptions - container for endpoints
3. FileDiscoveredEvent - event when file found
4. IFileDiscoveryHandler - interface for processing
5. FilePollingService - orchestrates polling
6. FilePollingJob - Quartz job for scheduled execution
7. Extension methods for DI registration
```

# Prompt 6 - DI Extensions
```
Create ASP.NET Core extensions:

File: src/FileSimulator.Client/Extensions/ServiceCollectionExtensions.cs

Methods:
- AddFileProtocolServices(configuration)
- AddFileSimulatorForMinikube(ip)
- AddFileSimulatorForCluster(namespace, release)
- AddFileSimulatorHealthChecks(options)
```

# Prompt 7 - Example Microservice
```
Create a complete example microservice:

File: src/FileSimulator.Client/Examples/CompleteExampleMicroservice.cs

Include:
- Service registration for all protocols
- Quartz configuration for polling
- MassTransit configuration
- API endpoints for each protocol (list, read, write)
- Health check and status endpoints
- Example IFileDiscoveryHandler implementation
- Example MassTransit consumer

Also create appsettings.complete.json with all configuration options.
```

### Phase 3: Scripts

# Prompt 8 - Test and Setup Scripts
```
Create PowerShell scripts:

1. scripts/test-simulator.ps1
   - Test all protocol connectivity
   - Verify health endpoints
   - Test cross-protocol file sharing
   - Output pass/fail summary

2. scripts/setup-windows.ps1
   - Create C:\simulator-data directory structure
   - Check prerequisites
   - Generate helper scripts for each protocol
```

## ============================================
## VERIFICATION PROMPTS
## ============================================

# After implementation, use these to verify:

```
Build the solution and check for errors:
dotnet build

Show any compilation errors and fix them.
```

```
Lint the Helm chart:
helm lint helm-chart/file-simulator

Fix any issues found.
```

```
Generate a test template to verify Helm rendering:
helm template file-sim helm-chart/file-simulator --debug > /tmp/rendered.yaml

Check for any template errors.
```
