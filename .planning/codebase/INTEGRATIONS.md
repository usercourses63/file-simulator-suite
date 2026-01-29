# External Integrations

**Analysis Date:** 2026-01-29

## APIs & External Services

**File Transfer Protocols:**
- **FTP (File Transfer Protocol)**
  - SDK/Client: `FluentFTP` 50.0.1
  - Configuration: `FtpServerOptions` in `FileSimulator.Client`
  - Connection: `AsyncFtpClient` (async) via `FtpFileService` at `src/FileSimulator.Client/Services/FileProtocolServices.cs:79`
  - Env vars: `FILE_FTP_HOST`, `FILE_FTP_PORT`, `FILE_FTP_USERNAME`, `FILE_FTP_PASSWORD`
  - Default: `localhost:21` with credentials `ftpuser/ftppass123`

- **SFTP (SSH File Transfer Protocol)**
  - SDK/Client: `SSH.NET` 2024.1.0
  - Configuration: `SftpServerOptions` in `FileSimulator.Client`
  - Connection: `SftpClient` (sync-wrapped) via `SftpFileService` at `src/FileSimulator.Client/Services/FileProtocolServices.cs:237`
  - Env vars: `FILE_SFTP_HOST`, `FILE_SFTP_PORT`, `FILE_SFTP_USERNAME`, `FILE_SFTP_PASSWORD`
  - Default: `localhost:22` with credentials `sftpuser/sftppass123`

- **S3/MinIO (Object Storage)**
  - SDK/Client: `AWSSDK.S3` 3.7.305
  - Configuration: `S3ServerOptions` in `FileSimulator.Client`
  - Connection: `AmazonS3Client` via `S3FileService` at `src/FileSimulator.Client/Services/FileProtocolServices.cs:409`
  - Env vars: `FILE_S3_ENDPOINT`, `FILE_S3_ACCESS_KEY`, `FILE_S3_SECRET_KEY`
  - Default: `http://localhost:9000` with credentials `minioadmin/minioadmin123`
  - MinIO health check endpoint: `/minio/health/live`

- **HTTP/WebDAV (Web-based File Access)**
  - SDK/Client: `System.Net.Http.HttpClient` (.NET BCL)
  - Configuration: `HttpServerOptions` in `FileSimulator.Client`
  - Connection: `HttpClient` via `HttpFileService` at `src/FileSimulator.Client/Services/FileProtocolServices.cs:587`
  - Env vars: `FILE_HTTP_URL`, `FILE_HTTP_USERNAME`, `FILE_HTTP_PASSWORD`
  - Default: `http://localhost:80` with optional basic auth
  - Endpoints used:
    - `/health` - Health check
    - `/api/files{path}` - Directory listing (JSON response)
    - `/download{path}` - File download (GET)
    - `/webdav{path}` - WebDAV operations (PUT/DELETE)

- **SMB/CIFS (Windows File Sharing)**
  - SDK/Client: `SMBLibrary` 1.5.2
  - Configuration: `SmbServerOptions` in `FileSimulator.Client`
  - Connection: `SMB2Client` via `SmbFileService` at `src/FileSimulator.Client/Services/FileProtocolServices.cs:733`
  - Env vars: `FILE_SMB_HOST`, `FILE_SMB_SHARE`, `FILE_SMB_USERNAME`, `FILE_SMB_PASSWORD`
  - Default: `localhost:445` share `simulator` with credentials `smbuser/smbpass123`
  - Authentication: NTLMv2 (domain optional)
  - Note: NTLM auth fails through TCP proxies; requires direct NodePort or in-cluster DNS

- **NFS (Network File System)**
  - SDK/Client: Local filesystem operations (mounted NFS share)
  - Configuration: `NfsServerOptions` in `FileSimulator.Client`
  - Connection: Filesystem mount at configured path via `NfsFileService` at `src/FileSimulator.Client/Services/FileProtocolServices.cs:1028`
  - Env vars: `FILE_NFS_MOUNT_PATH`, `FILE_NFS_HOST`, `FILE_NFS_PORT`
  - Default: Mount at `/mnt/nfs`, server `localhost:2049`
  - Approach: File I/O operations on mounted filesystem (no client library)

## Data Storage

**Databases:**
- Not detected - File Simulator is stateless; no persistent database required

**File Storage:**
- Local filesystem (via NFS mount or PersistentVolume in Kubernetes)
- S3/MinIO for object-oriented file storage
- FTP/SFTP/SMB/HTTP for file transfer
- All protocols share common underlying storage via Kubernetes PVC (HostPath)

**Caching:**
- Not detected - No explicit caching layer; connection pooling handled per protocol

## Authentication & Identity

**Auth Provider:**
- Custom per-protocol credentials (no centralized auth system)
- Each protocol service accepts options with protocol-specific credentials:
  - **FTP**: Username/Password
  - **SFTP**: Username/Password
  - **S3**: Access Key/Secret Key
  - **HTTP**: Basic auth (Username/Password), optional
  - **SMB**: Domain/Username/Password, NTLMv2
  - **NFS**: Filesystem-level permissions (no user/pass)

**Configuration Methods:**
- `FileSimulatorOptions.FromEnvironment()` - Read from env vars
- `FileSimulatorOptions.ForMinikube(ip)` - Preconfigured for Minikube with NodePorts
- `FileSimulatorOptions.ForCluster(namespace, releaseName)` - Preconfigured for in-cluster Kubernetes DNS

## Messaging & Event Distribution

**Event-Driven File Processing:**
- `MassTransit` 8.2.5 with `MassTransit.RabbitMQ` 8.2.5
- Not configured in core library but available for integration
- Example in `src/FileSimulator.Client/Examples/MassTransitExample.cs`

**File Discovery Events:**
- `FileDiscoveredEvent` record at `src/FileSimulator.Client/Services/FilePollingService.cs:40`
- Raised by `FilePollingService` when new files discovered via polling
- Handlers implement `IFileDiscoveryHandler` interface at `src/FileSimulator.Client/Services/FilePollingService.cs:51`

**Job Scheduling:**
- `Quartz` 3.8.1 for scheduled polling
- `FilePollingJob` (DisallowConcurrentExecution) at `src/FileSimulator.Client/Services/FilePollingService.cs:212`
- Cron-based scheduling per endpoint (`0/30 * * * * ?` default = every 30 seconds)

## Monitoring & Observability

**Error Tracking:**
- Not detected - No external error tracking service integrated

**Logs:**
- Microsoft.Extensions.Logging (built-in)
- Logged via `ILogger<T>` injected into all services
- Levels: Information (operations), Debug (file listings), Warning (failures)
- All protocol services log connection state, file operations, errors

**Health Checks:**
- `AspNetCore.HealthChecks.Network` 8.0.1 - TCP connectivity for FTP, SFTP, NFS, SMB
- `AspNetCore.HealthChecks.Uris` 8.0.1 - HTTP GET for S3 (`/minio/health/live`) and HTTP (`/health`)
- Method: `AddFileSimulatorHealthChecks(options)` in `ServiceCollectionExtensions.cs:66`
- Can be integrated into ASP.NET Core health check middleware

## Resilience & Retry Policies

**HTTP Client Polly Integration:**
- `Microsoft.Extensions.Http.Polly` 9.0.0
- Retry policy: 3 attempts with exponential backoff (2^n seconds)
- Circuit breaker: 5 failures trigger 30-second open state
- Configuration via `AddFileSimulatorHttpClient(options)` in `ServiceCollectionExtensions.cs:106`

## CI/CD & Deployment

**Hosting:**
- Kubernetes/Minikube/OpenShift Container Platform (OCP)
- Docker containers

**Deployment Approach:**
- Helm 3.x charts at `helm-chart/file-simulator/`
- Helm values: `values.yaml`, `values-multi-instance.yaml`
- Namespace: `file-simulator` (configurable)

**External Container Images (via Helm):**
- `filebrowser/filebrowser:v2.27.0` - Management UI (NodePort 30180)
- `fauria/vsftpd:latest` - FTP server (NodePort 30021)
- `atmoz/sftp:alpine` - SFTP server (NodePort 30022)
- `nginx:alpine` - HTTP file server (NodePort 30088)
- `ugeek/webdav:amd64-alpine` - WebDAV server (NodePort 30089)
- `minio/minio:latest` - S3-compatible storage (NodePort 30900)
- `samba/samba:latest` - SMB/CIFS server (NodePort 30445, or 445 in-cluster)
- `erichough/nfs-server:latest` - NFS server (NodePort 32149)

**Kubernetes Features Used:**
- PersistentVolume (HostPath or storage class)
- PersistentVolumeClaim (10Gi default)
- NodePort services for external access
- ConfigMaps for configuration
- Secrets for credentials (recommended)
- Service discovery (in-cluster DNS)

## Environment Configuration

**Required Environment Variables (Optional - Defaults Provided):**

**FTP:**
- `FILE_FTP_HOST` (default: localhost)
- `FILE_FTP_PORT` (default: 21)
- `FILE_FTP_USERNAME` (default: ftpuser)
- `FILE_FTP_PASSWORD` (default: ftppass123)

**SFTP:**
- `FILE_SFTP_HOST` (default: localhost)
- `FILE_SFTP_PORT` (default: 22)
- `FILE_SFTP_USERNAME` (default: sftpuser)
- `FILE_SFTP_PASSWORD` (default: sftppass123)

**S3:**
- `FILE_S3_ENDPOINT` (default: http://localhost:9000)
- `FILE_S3_ACCESS_KEY` (default: minioadmin)
- `FILE_S3_SECRET_KEY` (default: minioadmin123)

**HTTP:**
- `FILE_HTTP_URL` (default: http://localhost:80)
- `FILE_HTTP_USERNAME` (optional)
- `FILE_HTTP_PASSWORD` (optional)

**SMB:**
- `FILE_SMB_HOST` (default: localhost)
- `FILE_SMB_SHARE` (default: simulator)
- `FILE_SMB_USERNAME` (default: smbuser)
- `FILE_SMB_PASSWORD` (default: smbpass123)

**NFS:**
- `FILE_NFS_MOUNT_PATH` (default: /mnt/nfs)
- `FILE_NFS_HOST` (default: localhost)
- `FILE_NFS_PORT` (default: 2049)

**Secrets Location:**
- Hardcoded defaults in `FileSimulatorOptions` class at `FileSimulator.Client/FileSimulatorClient.cs:337`
- Overridable via configuration binding or environment variables
- In Kubernetes: Use Secrets instead of environment variables for sensitive data

## Webhooks & Callbacks

**Incoming:**
- File discovery polling (not webhook-based, pull model via Quartz scheduling)
- `FilePollingEndpoint` configuration drives polling schedule

**Outgoing:**
- Event handlers via `IFileDiscoveryHandler` interface
- MassTransit integration available for distributed event publishing
- No webhook infrastructure in place; event distribution is in-process or via message bus

## Cross-Protocol File Sharing

**Unified Operations:**
- `FileSimulatorClient` class at `FileSimulator.Client/FileSimulatorClient.cs:16` provides unified interface
- Methods: `UploadAsync()`, `DownloadAsync()` accept `FileProtocol` enum parameter
- Enables transparent protocol switching for file operations
- Example: Upload via HTTP, download via S3

**Protocol Services:**
- All services implement `IFileProtocolService` interface at `FileSimulator.Client/Services/FileProtocolServices.cs:20`
- Unified methods: `DiscoverFilesAsync()`, `ReadFileAsync()`, `WriteFileAsync()`, `DeleteFileAsync()`, `HealthCheckAsync()`
- Can be injected as `IEnumerable<IFileProtocolService>` for multi-protocol support

---

*Integration audit: 2026-01-29*
