# Codebase Structure

**Analysis Date:** 2026-01-29

## Directory Layout

```
file-simulator-suite/
├── .planning/
│   └── codebase/                      # GSD planning artifacts
│       ├── ARCHITECTURE.md
│       ├── STRUCTURE.md
│       ├── CONVENTIONS.md
│       └── TESTING.md
├── helm-chart/
│   ├── file-simulator/                # Main Helm chart
│   │   ├── Chart.yaml                 # Chart metadata
│   │   ├── values.yaml                # Default configuration (all protocols)
│   │   ├── values-multi-instance.yaml # Multi-server overrides
│   │   ├── templates/
│   │   │   ├── _helpers.tpl           # Template helpers
│   │   │   ├── namespace.yaml         # Namespace definition
│   │   │   ├── storage.yaml           # PV/PVC for shared storage
│   │   │   ├── serviceaccount.yaml    # RBAC service account
│   │   │   ├── management.yaml        # FileBrowser UI deployment
│   │   │   ├── ftp.yaml               # FTP server (vsftpd)
│   │   │   ├── ftp-multi.yaml         # Multiple FTP instances
│   │   │   ├── sftp.yaml              # SFTP server (OpenSSH)
│   │   │   ├── sftp-multi.yaml        # Multiple SFTP instances
│   │   │   ├── http.yaml              # HTTP/WebDAV server (nginx)
│   │   │   ├── s3.yaml                # S3/MinIO server
│   │   │   ├── smb.yaml               # SMB/Samba server
│   │   │   ├── nas.yaml               # NFS server
│   │   │   └── dashboard-config.yaml  # Monitoring dashboard config
│   │   └── files/                     # Static file templates
│   └── samples/
│       ├── file-simulator-configmap.yaml    # Example ConfigMap for microservices
│       └── microservice-deployment.yaml     # Sample consumer deployment
├── src/
│   ├── FileSimulator.Client/          # NuGet package / class library
│   │   ├── FileSimulator.Client.csproj
│   │   ├── FileSimulatorClient.cs     # Legacy unified client (entry point)
│   │   ├── Services/
│   │   │   ├── FileProtocolServices.cs    # All 6 protocol service implementations
│   │   │   └── FilePollingService.cs      # File discovery/polling orchestrator
│   │   ├── Extensions/
│   │   │   └── ServiceCollectionExtensions.cs  # DI registration helpers
│   │   └── Examples/
│   │       ├── Program.example.cs         # Full microservice example (not compiled)
│   │       ├── CompleteExampleMicroservice.cs  # Reference implementation
│   │       └── MassTransitExample.cs      # Event-driven example with RabbitMQ
│   ├── FileSimulator.TestConsole/     # Console test application
│   │   ├── FileSimulator.TestConsole.csproj
│   │   ├── Program.cs                 # Main test orchestrator
│   │   ├── CrossProtocolTest.cs       # File sharing test between protocols
│   │   └── appsettings.json           # Test configuration
│   └── file-simulator-suite.sln       # Solution file
├── scripts/
│   ├── test-simulator.ps1             # PowerShell protocol tests
│   ├── test-simulator.sh              # Bash protocol tests
│   └── setup-windows.ps1              # Windows setup helper
├── examples/
│   └── client-cluster/                # Multi-instance cluster examples
├── docs/
│   ├── API.md                         # IFileProtocolService documentation
│   ├── DEPLOYMENT.md                  # Kubernetes deployment guide
│   ├── PROTOCOLS.md                   # Protocol-specific notes
│   └── CROSS-PROTOCOL.md              # File sharing between protocols
├── CLAUDE.md                          # Implementation plan (project instructions)
└── README.md
```

## Directory Purposes

**helm-chart/file-simulator/templates:**
- Purpose: Kubernetes manifests for all protocol servers
- Contains: Deployments, Services, StatefulSets, ConfigMaps, Secrets
- Key files: `namespace.yaml` (creates file-simulator namespace), `storage.yaml` (PV/PVC mount at /mnt/simulator-data), `ftp.yaml`/`sftp.yaml`/`http.yaml`/`s3.yaml`/`smb.yaml`/`nas.yaml` (individual protocol deployments)

**src/FileSimulator.Client:**
- Purpose: Reusable NuGet package for file operations via 6 protocols
- Contains: Protocol services, DI extensions, options classes, legacy unified client
- Key files: `FileProtocolServices.cs` (main implementations), `ServiceCollectionExtensions.cs` (DI setup)

**src/FileSimulator.TestConsole:**
- Purpose: Standalone console app to validate all protocols
- Contains: Per-protocol test methods, cross-protocol tests, performance measurement
- Key files: `Program.cs` (entry point, status output), `CrossProtocolTest.cs` (file move between protocols)

**examples/client-cluster:**
- Purpose: Reference implementations for consuming applications
- Contains: Multi-instance test scenarios, cluster networking examples

**scripts:**
- Purpose: Helper automation for Windows and Linux environments
- Contains: Minikube validation, port forwarding tests, setup helpers

## Key File Locations

**Entry Points:**

- `src/FileSimulator.Client/FileSimulatorClient.cs`: Legacy single-instance client (main class users instantiate directly)
- `src/FileSimulator.TestConsole/Program.cs`: Test console main method
- `helm-chart/file-simulator/Chart.yaml`: Helm chart entry for deployment

**Configuration:**

- `helm-chart/file-simulator/values.yaml`: Helm values for all 6 protocol servers
- `helm-chart/file-simulator/values-multi-instance.yaml`: Multi-server instance overrides
- `src/FileSimulator.TestConsole/appsettings.json`: Test console configuration

**Core Logic:**

- `src/FileSimulator.Client/Services/FileProtocolServices.cs`: All protocol service implementations
- `src/FileSimulator.Client/Services/FilePollingService.cs`: File discovery and polling
- `src/FileSimulator.Client/FileSimulatorClient.cs`: Unified protocol operations

**Testing:**

- `src/FileSimulator.TestConsole/Program.cs`: Per-protocol test functions
- `src/FileSimulator.TestConsole/CrossProtocolTest.cs`: Cross-protocol file sharing

**Dependency Injection:**

- `src/FileSimulator.Client/Extensions/ServiceCollectionExtensions.cs`: All DI registration methods

## Naming Conventions

**Files:**

- Protocol services: `FileProtocolServices.cs` (single file, all protocols together)
- Configuration classes: `{ProtocolName}ServerOptions` (e.g., `FtpServerOptions`, `S3ServerOptions`)
- Entry classes: `FileSimulatorClient.cs`, `FilePollingService.cs`
- Extensions: `ServiceCollectionExtensions.cs`
- Example implementations: `CompleteExampleMicroservice.cs`, `MassTransitExample.cs`

**Classes:**

- Protocol services: `{Protocol}FileService` (e.g., `FtpFileService`, `SmbFileService`)
- Options: `{Protocol}ServerOptions` (e.g., `HttpServerOptions`, `NfsServerOptions`)
- Global options: `FileSimulatorOptions` (single class for all 6 protocols)
- Events/records: `FileDiscoveredEvent`, `RemoteFileInfo`, `FilePollingEndpoint`
- Handlers: `IFileDiscoveryHandler`, `IFileProtocolService`

**Methods:**

- Protocol-specific operations: `{OperationVia}{Protocol}Async` (e.g., `UploadViaFtpAsync`, `DownloadViaS3Async`)
- Generic operations: `{Operation}Async(remotePath, localPath, protocol)` (e.g., `UploadAsync`, `DownloadAsync`)
- Polling: `PollEndpointAsync`, `MarkAsProcessed`, `DownloadFileAsync`

## Where to Add New Code

**New Protocol Support:**
1. Create service class implementing `IFileProtocolService` in `src/FileSimulator.Client/Services/FileProtocolServices.cs`
2. Create corresponding `{Protocol}ServerOptions` class (same file)
3. Add DI extension methods in `src/FileSimulator.Client/Extensions/ServiceCollectionExtensions.cs`:
   - `Add{Protocol}FileService(IConfiguration)` - config-based
   - `Add{Protocol}FileService(Action<Options>)` - lambda-based
4. Add test method in `src/FileSimulator.TestConsole/Program.cs` (`Test{Protocol}Async`)
5. Add Helm deployment template in `helm-chart/file-simulator/templates/{protocol}.yaml`

**New Consumer Application:**
1. Add project reference to `FileSimulator.Client.csproj`
2. Register services in startup:
   ```csharp
   builder.Services.AddAllFileProtocolServices(configuration);
   // or per-protocol:
   builder.Services.AddFtpFileService(configuration);
   builder.Services.AddS3FileService(configuration);
   ```
3. Inject `IEnumerable<IFileProtocolService>` or specific service
4. Use `FilePollingService` for discovery (optional)

**New Test Case:**
1. Add method to `src/FileSimulator.TestConsole/Program.cs` following pattern of `TestFtpAsync`
2. Add status update in `Main()` method
3. Add result to `results` list
4. Create corresponding Kubernetes test if needed

**Helm Customization:**
1. Edit `helm-chart/file-simulator/values.yaml` for defaults
2. Create environment-specific `values-{env}.yaml` overrides
3. Helm templating: Use `{{.Values.xxx}}` for values, `{{.Release.Name}}` for release name
4. Storage: All servers mount shared PVC at `/mnt/simulator-data` (or configured path)

## Special Directories

**helm-chart/file-simulator/files/:**
- Purpose: Static configuration files mounted into containers
- Generated: No (checked in)
- Committed: Yes
- Example use: SSH keys, nginx config, Samba config

**src/FileSimulator.Client/Examples/:**
- Purpose: Reference implementations, not compiled into library
- Generated: No
- Committed: Yes
- Note: Compile directive removes these from NuGet package build

**src/FileSimulator.TestConsole/bin/ and obj/:**
- Purpose: Build output
- Generated: Yes (during `dotnet build`)
- Committed: No (in .gitignore)

**.planning/codebase/:**
- Purpose: GSD-generated analysis documents
- Generated: Yes (by gsd:map-codebase)
- Committed: Yes (for team reference)

## Configuration File Locations

**appsettings.json files:**
- Development: `src/FileSimulator.TestConsole/appsettings.json` (localhost defaults)
- Environment-specific: `src/FileSimulator.TestConsole/appsettings.{Environment}.json` (Minikube, production)

**Helm configuration:**
- Default values: `helm-chart/file-simulator/values.yaml` (all protocols)
- Multi-instance: `helm-chart/file-simulator/values-multi-instance.yaml`
- Overrides at deploy time: `helm upgrade --install -f values-prod.yaml`

**Examples:**
- Sample Kubernetes: `helm-chart/samples/microservice-deployment.yaml`
- Sample ConfigMap: `helm-chart/samples/file-simulator-configmap.yaml`

## Project References and Dependencies

**Solution structure:**
- FileSimulator.Client - Class library (packaged as NuGet)
- FileSimulator.TestConsole - Console app (depends on Client)

**NuGet packages (by layer):**
- Protocol clients: FluentFTP, SSH.NET, AWSSDK.S3, SMBLibrary, HttpClient (built-in)
- DI/Configuration: Microsoft.Extensions.* (Abstractions, DependencyInjection, Options, Configuration)
- Scheduling: Quartz, Quartz.Extensions.Hosting
- Messaging: MassTransit, MassTransit.RabbitMQ (optional, examples only)
- Resilience: Polly, Microsoft.Extensions.Http.Polly
- Health checks: AspNetCore.HealthChecks.Network, AspNetCore.HealthChecks.Uris
- Console UI: Spectre.Console (TestConsole only)
