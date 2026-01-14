# Claude Code Implementation Guide - File Simulator Suite

## Table of Contents
1. [Prerequisites](#1-prerequisites)
2. [Project Initialization](#2-project-initialization)
3. [Claude Code Setup](#3-claude-code-setup)
4. [Implementation Steps](#4-implementation-steps)
5. [Verification & Testing](#5-verification--testing)
6. [Troubleshooting](#6-troubleshooting)

---

## 1. Prerequisites

### Required Software

```powershell
# Check all prerequisites
Write-Host "Checking prerequisites..."

# 1. Node.js (for Claude Code)
node --version  # Should be v18+ 

# 2. .NET 9.0 SDK
dotnet --version  # Should be 9.0.x

# 3. Docker Desktop
docker --version

# 4. Minikube
minikube version

# 5. Helm
helm version

# 6. kubectl
kubectl version --client

# 7. Git
git --version
```

### Install Claude Code (if not installed)

```powershell
# Install Claude Code globally
npm install -g @anthropic-ai/claude-code

# Verify installation
claude --version

# Authenticate (first time only)
claude auth
```

### Create Working Directory

```powershell
# Create project directory
mkdir C:\Projects\file-simulator-suite
cd C:\Projects\file-simulator-suite

# Initialize git repository
git init
```

---

## 2. Project Initialization

### Step 2.1: Create Directory Structure

```powershell
# Create the full directory structure
mkdir -p helm-chart/file-simulator/templates
mkdir -p helm-chart/file-simulator/files
mkdir -p helm-chart/samples
mkdir -p scripts
mkdir -p src/FileSimulator.Client/Services
mkdir -p src/FileSimulator.Client/Extensions
mkdir -p src/FileSimulator.Client/Examples
mkdir -p tests/FileSimulator.Client.Tests

# Create placeholder files
New-Item -ItemType File -Path "README.md"
New-Item -ItemType File -Path "CLAUDE.md"
New-Item -ItemType File -Path ".gitignore"
```

### Step 2.2: Create .gitignore

```powershell
@"
# .NET
bin/
obj/
*.user
*.suo
.vs/

# IDE
.idea/
.vscode/

# Build
publish/
artifacts/

# Secrets
appsettings.*.json
!appsettings.example.json
!appsettings.complete.json

# Helm
*.tgz

# Temp
*.tmp
*.log
"@ | Out-File -FilePath ".gitignore" -Encoding UTF8
```

### Step 2.3: Initialize .NET Solution

```powershell
# Create solution file
dotnet new sln -n FileSimulatorSuite

# Create class library project
dotnet new classlib -n FileSimulator.Client -o src/FileSimulator.Client -f net9.0

# Create test project
dotnet new xunit -n FileSimulator.Client.Tests -o tests/FileSimulator.Client.Tests -f net9.0

# Add projects to solution
dotnet sln add src/FileSimulator.Client/FileSimulator.Client.csproj
dotnet sln add tests/FileSimulator.Client.Tests/FileSimulator.Client.Tests.csproj

# Add project reference
dotnet add tests/FileSimulator.Client.Tests reference src/FileSimulator.Client
```

### Step 2.4: Add NuGet Packages

```powershell
cd src/FileSimulator.Client

# Core file protocols
dotnet add package AWSSDK.S3 --version 3.7.305
dotnet add package FluentFTP --version 50.0.1
dotnet add package SSH.NET --version 2024.1.0
dotnet add package SMBLibrary --version 1.5.2

# ASP.NET Core & DI
dotnet add package Microsoft.Extensions.Configuration.Abstractions --version 9.0.0
dotnet add package Microsoft.Extensions.DependencyInjection.Abstractions --version 9.0.0
dotnet add package Microsoft.Extensions.Options --version 9.0.0
dotnet add package Microsoft.Extensions.Http.Polly --version 9.0.0

# Scheduling
dotnet add package Quartz --version 3.8.1
dotnet add package Quartz.Extensions.Hosting --version 3.8.1
dotnet add package Quartz.Extensions.DependencyInjection --version 3.8.1

# Health checks
dotnet add package AspNetCore.HealthChecks.Network --version 8.0.1
dotnet add package AspNetCore.HealthChecks.Uris --version 8.0.1

# Messaging (optional)
dotnet add package MassTransit --version 8.2.5
dotnet add package MassTransit.RabbitMQ --version 8.2.5

cd ../..
```

---

## 3. Claude Code Setup

### Step 3.1: Create CLAUDE.md Configuration

The `CLAUDE.md` file is the most important file - it tells Claude Code how to implement the project. Copy the CLAUDE.md from the provided zip file, or create it with this command:

```powershell
# Download or copy CLAUDE.md to project root
# This file contains all implementation instructions for Claude Code
```

### Step 3.2: Claude Code Skills/Plugins

Claude Code doesn't require special plugins for this project. However, ensure these capabilities are available:

| Capability | Purpose | Built-in? |
|------------|---------|-----------|
| File operations | Create/edit source files | ✅ Yes |
| Terminal/Bash | Run dotnet, helm, kubectl commands | ✅ Yes |
| Code generation | Generate C# and YAML | ✅ Yes |

### Step 3.3: Start Claude Code Session

```powershell
# Navigate to project root
cd C:\Projects\file-simulator-suite

# Start Claude Code in interactive mode
claude

# Or start with specific context
claude --context "I'm implementing a File Simulator Suite for Kubernetes"
```

---

## 4. Implementation Steps

### Overview: Prompt Sequence

Execute these prompts in order. Each prompt builds on the previous one.

---

### PHASE 1: Helm Chart Infrastructure

#### Prompt 1.1: Create Helm Chart Base

```
Create the Helm chart structure for the File Simulator Suite. 

Read CLAUDE.md for the full project context.

Start by creating:
1. helm-chart/file-simulator/Chart.yaml
2. helm-chart/file-simulator/values.yaml with all protocol configurations
3. helm-chart/file-simulator/templates/_helpers.tpl

The chart should support FTP, SFTP, HTTP/WebDAV, S3/MinIO, SMB, and NFS protocols.
Each service should have NodePort access for Minikube development.
```

#### Prompt 1.2: Create Storage and Namespace

```
Create the Kubernetes storage and namespace templates:

1. helm-chart/file-simulator/templates/namespace.yaml - Create the file-simulator namespace
2. helm-chart/file-simulator/templates/storage.yaml - PersistentVolume and PVC with hostPath for Minikube
3. helm-chart/file-simulator/templates/serviceaccount.yaml

The storage should use hostPath /mnt/simulator-data which maps to C:\simulator-data on Windows.
```

#### Prompt 1.3: Create Management UI

```
Create the Management UI deployment using FileBrowser:

File: helm-chart/file-simulator/templates/management.yaml

Requirements:
- Use filebrowser/filebrowser:v2.27.0 image
- Expose on NodePort 30080
- Mount the shared PVC
- Include health checks
- Create ConfigMap for filebrowser configuration
```

#### Prompt 1.4: Create FTP Server

```
Create the FTP server deployment:

File: helm-chart/file-simulator/templates/ftp.yaml

Requirements:
- Use fauria/vsftpd image
- Expose on NodePort 30021
- Configure passive mode ports 21100-21110
- Mount shared PVC
- Environment variables for FTP_USER, FTP_PASS from values
```

#### Prompt 1.5: Create SFTP Server

```
Create the SFTP server deployment:

File: helm-chart/file-simulator/templates/sftp.yaml

Requirements:
- Use atmoz/sftp:alpine image
- Expose on NodePort 30022
- Configure users via container args
- Mount shared PVC to user's data directory
```

#### Prompt 1.6: Create HTTP/WebDAV Server

```
Create the HTTP file server with WebDAV support:

File: helm-chart/file-simulator/templates/http.yaml

Requirements:
- Use nginx:alpine image
- Expose on NodePort 30088
- Create ConfigMap with nginx.conf that enables:
  - Directory listing (autoindex)
  - WebDAV methods (PUT, DELETE, MKCOL, COPY, MOVE)
  - Basic authentication for WebDAV
  - Health check endpoint at /health
  - JSON file listing at /api/files
- Use htpasswd for authentication (init container)
```

#### Prompt 1.7: Create S3/MinIO Server

```
Create the MinIO S3-compatible storage deployment:

File: helm-chart/file-simulator/templates/s3.yaml

Requirements:
- Use minio/minio:latest image
- Expose API on NodePort 30900, Console on 30901
- Mount shared PVC
- Create Job to initialize default buckets (input, output, temp)
- Health checks for /minio/health/live and /minio/health/ready
```

#### Prompt 1.8: Create SMB Server

```
Create the Samba SMB server deployment:

File: helm-chart/file-simulator/templates/smb.yaml

Requirements:
- Use dperson/samba image
- Expose on NodePort 30445
- Configure share named "simulator"
- Mount shared PVC
```

#### Prompt 1.9: Create NFS Server

```
Create the NFS server deployment:

File: helm-chart/file-simulator/templates/nas.yaml

Requirements:
- Use itsthenetwork/nfs-server-alpine image
- Expose on NodePort 32049
- Configure export for /data
- Mount shared PVC
- Requires privileged security context
```

#### Prompt 1.10: Create NOTES.txt

```
Create the Helm post-install notes:

File: helm-chart/file-simulator/templates/NOTES.txt

Include:
- Instructions for getting Minikube IP
- URLs and credentials for each service
- Example commands for FTP, SFTP, S3 CLI
- Internal service URLs for pods
```

---

### PHASE 2: .NET Client Library

#### Prompt 2.1: Create Base Interface

```
Create the base interface and models for file protocol services:

File: src/FileSimulator.Client/Services/IFileProtocolService.cs

Create:
1. IFileProtocolService interface with methods:
   - DiscoverFilesAsync(path, pattern) - for polling
   - ReadFileAsync(remotePath) - download to memory
   - DownloadFileAsync(remotePath, localPath) - download to disk
   - WriteFileAsync(remotePath, content) - upload from memory
   - UploadFileAsync(localPath, remotePath) - upload from disk
   - DeleteFileAsync(remotePath)
   - HealthCheckAsync()

2. RemoteFileInfo record with: FullPath, Name, Size, ModifiedAt, IsDirectory
```

#### Prompt 2.2: Create FTP Service

```
Create the FTP file service implementation:

File: src/FileSimulator.Client/Services/FtpFileService.cs

Requirements:
- Implement IFileProtocolService
- Use FluentFTP's AsyncFtpClient
- Thread-safe connection management with SemaphoreSlim
- Automatic reconnection on failure
- Support wildcard pattern matching (*, ?)
- Comprehensive logging
- Create FtpServerOptions class
```

#### Prompt 2.3: Create SFTP Service

```
Create the SFTP file service implementation:

File: src/FileSimulator.Client/Services/SftpFileService.cs

Requirements:
- Implement IFileProtocolService
- Use SSH.NET's SftpClient
- Thread-safe connection management
- Wrap synchronous SSH.NET methods in Task.Run for async
- Support wildcard pattern matching
- Create SftpServerOptions class
```

#### Prompt 2.4: Create S3 Service

```
Create the S3/MinIO file service implementation:

File: src/FileSimulator.Client/Services/S3FileService.cs

Requirements:
- Implement IFileProtocolService
- Use AWSSDK.S3's AmazonS3Client
- Configure for MinIO (ForcePathStyle = true)
- Path format: "bucket/key" or "bucket/prefix/key"
- Support pagination for large buckets
- Create S3ServerOptions class
```

#### Prompt 2.5: Create HTTP Service

```
Create the HTTP/WebDAV file service implementation:

File: src/FileSimulator.Client/Services/HttpFileService.cs

Requirements:
- Implement IFileProtocolService
- Use HttpClient with Basic authentication
- Parse nginx JSON directory listing for discovery
- Use WebDAV endpoints for write/delete operations
- Create HttpServerOptions class
```

#### Prompt 2.6: Create SMB Service

```
Create the SMB file service implementation:

File: src/FileSimulator.Client/Services/SmbFileService.cs

Requirements:
- Implement IFileProtocolService
- Use SMBLibrary's SMB2Client
- Thread-safe connection management
- Handle SMB path format (backslashes)
- Chunked read/write for large files
- Create SmbServerOptions class
```

#### Prompt 2.7: Create NFS Service

```
Create the NFS file service implementation:

File: src/FileSimulator.Client/Services/NfsFileService.cs

Requirements:
- Implement IFileProtocolService
- Use mounted filesystem approach (System.IO)
- NFS is mounted as a PersistentVolume in Kubernetes
- MountPath configuration option
- Create NfsServerOptions class
```

#### Prompt 2.8: Create Polling Service

```
Create the file polling service with Quartz.NET integration:

File: src/FileSimulator.Client/Services/FilePollingService.cs

Create:
1. FilePollingEndpoint - configuration for each polling endpoint
2. FilePollingOptions - container for endpoints and temp directory
3. FileDiscoveredEvent - event raised when file found
4. IFileDiscoveryHandler - interface for processing discovered files
5. FilePollingService - orchestrates polling, tracks processed files
6. FilePollingJob - Quartz job for scheduled execution
7. Extension methods for DI registration and Quartz configuration
```

#### Prompt 2.9: Create DI Extensions

```
Create service collection extensions for ASP.NET Core:

File: src/FileSimulator.Client/Extensions/ServiceCollectionExtensions.cs

Create methods:
1. AddFileProtocolServices(configuration) - register all services from config
2. AddFileSimulatorForMinikube(ip) - configure for local development
3. AddFileSimulatorForCluster(namespace, release) - configure for K8s
4. AddFileSimulatorHealthChecks(options) - register health checks
5. AddFileSimulatorHttpClient(options) - HTTP client with Polly retry
```

---

### PHASE 3: Examples and Configuration

#### Prompt 3.1: Create Complete Example Microservice

```
Create a complete example microservice that demonstrates all protocols:

File: src/FileSimulator.Client/Examples/CompleteExampleMicroservice.cs

Include:
1. Service registration for all protocols
2. Quartz configuration for polling
3. MassTransit configuration
4. Minimal API endpoints for each protocol (list, read, write)
5. Health check endpoint
6. Status endpoint showing all protocol health
7. Example IFileDiscoveryHandler implementation
8. Example MassTransit consumer
```

#### Prompt 3.2: Create Configuration Files

```
Create configuration files:

1. src/FileSimulator.Client/Examples/appsettings.complete.json
   - All protocol connection settings (for in-cluster)
   - Polling endpoints configuration
   - RabbitMQ settings

2. src/FileSimulator.Client/Examples/appsettings.development.json  
   - Minikube settings (NodePort access)
```

---

### PHASE 4: Testing and Scripts

#### Prompt 4.1: Create PowerShell Test Script

```
Create a PowerShell test script to verify all protocols:

File: scripts/test-simulator.ps1

Test each protocol:
1. Kubernetes connectivity and pod health
2. Management UI health check
3. HTTP server operations
4. S3/MinIO health and bucket operations
5. FTP port connectivity
6. SFTP port connectivity
7. SMB port connectivity
8. NFS port connectivity
9. Cross-protocol file sharing test

Output: Summary of passed/failed tests
```

#### Prompt 4.2: Create Setup Script

```
Create a Windows setup script:

File: scripts/setup-windows.ps1

Actions:
1. Create directory structure (C:\simulator-data\input, output, temp)
2. Check Minikube installation and status
3. Configure minikube mount
4. Generate environment variables file
5. Create helper scripts for each protocol
```

---

### PHASE 5: Kubernetes Samples

#### Prompt 5.1: Create Sample ConfigMap

```
Create a sample ConfigMap for microservices to use:

File: helm-chart/samples/file-simulator-configmap.yaml

Include:
- ConfigMap with all service endpoints (internal DNS names)
- Secret with all credentials (base64 encoded)
- Example deployment snippet showing envFrom usage
```

#### Prompt 5.2: Create Sample Deployment

```
Create a sample microservice deployment:

File: helm-chart/samples/microservice-deployment.yaml

Include:
- Deployment with environment injection from ConfigMap/Secret
- Health check probes
- Resource limits
- Volume mount for temp storage
```

---

## 5. Verification & Testing

### Step 5.1: Build and Validate

```powershell
# Build the .NET solution
dotnet build

# Run tests
dotnet test

# Lint the Helm chart
helm lint helm-chart/file-simulator
```

### Step 5.2: Deploy to Minikube

```powershell
# Start Minikube with mount
minikube start --mount --mount-string="C:\simulator-data:/mnt/simulator-data"

# Deploy the simulator
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --namespace file-simulator --create-namespace

# Wait for pods
kubectl wait --for=condition=ready pod -l app.kubernetes.io/instance=file-sim `
    -n file-simulator --timeout=300s

# Get Minikube IP
$MINIKUBE_IP = minikube ip
Write-Host "Minikube IP: $MINIKUBE_IP"
```

### Step 5.3: Run Verification Tests

```powershell
# Run the test script
.\scripts\test-simulator.ps1 -MinikubeIp $MINIKUBE_IP

# Or run manually
# Management UI
Start-Process "http://${MINIKUBE_IP}:30080"

# MinIO Console
Start-Process "http://${MINIKUBE_IP}:30901"
```

---

## 6. Troubleshooting

### Common Issues

#### Claude Code not finding CLAUDE.md
```powershell
# Ensure you're in the project root
cd C:\Projects\file-simulator-suite

# Check CLAUDE.md exists
Test-Path CLAUDE.md

# Restart Claude Code
claude
```

#### Build errors
```powershell
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore

# Rebuild
dotnet build --no-incremental
```

#### Pods not starting
```powershell
# Check pod status
kubectl get pods -n file-simulator

# Check pod logs
kubectl logs -n file-simulator <pod-name>

# Describe pod for events
kubectl describe pod -n file-simulator <pod-name>
```

#### Mount not working
```powershell
# Check if mount exists
minikube ssh "ls -la /mnt/simulator-data"

# Restart with mount
minikube stop
minikube start --mount --mount-string="C:\simulator-data:/mnt/simulator-data"
```

---

## Quick Reference: Prompt Templates

### For new features:
```
Add [feature] to the File Simulator Suite.

Context: Read CLAUDE.md for project structure.
Location: [specify file path]
Requirements:
- [requirement 1]
- [requirement 2]
```

### For bug fixes:
```
Fix [issue description] in [file path].

The current behavior is: [describe]
The expected behavior is: [describe]
```

### For refactoring:
```
Refactor [component] to [improvement].

Current location: [file path]
Goals:
- [goal 1]
- [goal 2]
```

---

## Summary: Complete Prompt Sequence

```
# Phase 1: Helm Chart (10 prompts)
1.1  → Chart.yaml, values.yaml, _helpers.tpl
1.2  → namespace.yaml, storage.yaml, serviceaccount.yaml
1.3  → management.yaml (FileBrowser)
1.4  → ftp.yaml
1.5  → sftp.yaml
1.6  → http.yaml (nginx + WebDAV)
1.7  → s3.yaml (MinIO)
1.8  → smb.yaml (Samba)
1.9  → nas.yaml (NFS)
1.10 → NOTES.txt

# Phase 2: .NET Library (9 prompts)
2.1 → IFileProtocolService interface
2.2 → FtpFileService
2.3 → SftpFileService
2.4 → S3FileService
2.5 → HttpFileService
2.6 → SmbFileService
2.7 → NfsFileService
2.8 → FilePollingService + Quartz
2.9 → ServiceCollectionExtensions

# Phase 3: Examples (2 prompts)
3.1 → CompleteExampleMicroservice.cs
3.2 → appsettings.json files

# Phase 4: Testing (2 prompts)
4.1 → test-simulator.ps1
4.2 → setup-windows.ps1

# Phase 5: Samples (2 prompts)
5.1 → ConfigMap/Secret samples
5.2 → Deployment sample

Total: 25 prompts for complete implementation
```
