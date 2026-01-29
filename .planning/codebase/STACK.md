# Technology Stack

**Analysis Date:** 2026-01-29

## Languages

**Primary:**
- C# 12 (latest LangVersion) - Core client library and test console
- YAML - Kubernetes/Helm configuration

**Configuration:**
- JSON - Application settings and configuration files

## Runtime

**Environment:**
- .NET 9.0 - Primary runtime for all compiled applications

**Package Manager:**
- NuGet - Manages all .NET dependencies
- Lockfile: assets/project.assets.json (generated)

## Frameworks

**Core:**
- ASP.NET Core (minimal APIs compatible) - Foundation for integration into microservices via DI
- .NET 9.0 Base Class Library

**Testing & Validation:**
- No unit test framework currently present (test console exists: `FileSimulator.TestConsole`)

**Build/Dev:**
- .NET 9.0 SDK
- C# compiler (Roslyn)

## Key Dependencies

**Critical File Protocol Libraries:**
- `FluentFTP` 50.0.1 - FTP/FTPS client operations
- `SSH.NET` 2024.1.0 - SFTP client operations
- `AWSSDK.S3` 3.7.305 - S3/MinIO compatible object storage
- `SMBLibrary` 1.5.2 - SMB/CIFS client for Windows file sharing

**Scheduling & Job Processing:**
- `Quartz` 3.8.1 - Job scheduler for polling
- `Quartz.Extensions.Hosting` 3.8.1 - Quartz integration with .NET Host
- `Quartz.Extensions.DependencyInjection` 3.8.1 - Quartz DI integration

**Messaging & Event Distribution:**
- `MassTransit` 8.2.5 - Service bus abstraction
- `MassTransit.RabbitMQ` 8.2.5 - RabbitMQ transport for MassTransit

**Resilience & HTTP:**
- `Microsoft.Extensions.Http.Polly` 9.0.0 - Polly integration for retry/circuit breaker policies
- Built-in `HttpClient` for HTTP operations

**Dependency Injection & Configuration:**
- `Microsoft.Extensions.Configuration.Abstractions` 9.0.0 - Config abstractions
- `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.0 - DI abstractions
- `Microsoft.Extensions.Options` 9.0.0 - Options pattern for configuration binding

**Health Checks:**
- `AspNetCore.HealthChecks.Network` 8.0.1 - TCP connectivity health checks
- `AspNetCore.HealthChecks.Uris` 8.0.1 - HTTP endpoint health checks

**Console & UI:**
- `Spectre.Console` 0.49.1 - Rich console output for test console
- `Microsoft.Extensions.Hosting` 9.0.0 - Generic host for test console
- `Microsoft.Extensions.Configuration.Json` 9.0.0 - JSON config file support

## Configuration

**Environment:**
- Configuration sources (in order):
  1. `appsettings.json` - Base configuration
  2. `appsettings.{DOTNET_ENVIRONMENT}.json` - Environment-specific overrides
  3. Environment variables - Runtime overrides
  4. Command-line arguments - CLI parameter overrides

**Key Environment Variables:**
- `FILE_FTP_HOST`, `FILE_FTP_PORT`, `FILE_FTP_USERNAME`, `FILE_FTP_PASSWORD`
- `FILE_SFTP_HOST`, `FILE_SFTP_PORT`, `FILE_SFTP_USERNAME`, `FILE_SFTP_PASSWORD`
- `FILE_S3_ENDPOINT`, `FILE_S3_ACCESS_KEY`, `FILE_S3_SECRET_KEY`
- `FILE_HTTP_URL`, `FILE_HTTP_USERNAME`, `FILE_HTTP_PASSWORD`
- `FILE_SMB_HOST`, `FILE_SMB_SHARE`, `FILE_SMB_USERNAME`, `FILE_SMB_PASSWORD`
- `FILE_NFS_MOUNT_PATH`, `FILE_NFS_HOST`, `FILE_NFS_PORT`
- `DOTNET_ENVIRONMENT` - Controls which appsettings file is loaded

**Build:**
- No external build tools required - uses standard .NET CLI
- `dotnet build`, `dotnet publish` supported

## Platform Requirements

**Development:**
- .NET 9.0 SDK
- Windows, Linux, or macOS workstation
- Network connectivity to target protocol servers

**Production:**
- .NET 9.0 Runtime
- Kubernetes/OpenShift cluster (Minikube for local dev)
- Network access to file protocol services (FTP, SFTP, S3, HTTP, SMB, NFS)

**Deployment Targets:**
- Kubernetes via Helm (3.x required)
- Minikube for local development
- OpenShift Container Platform (OCP) for production
- Docker containers (base image: mcr.microsoft.com/dotnet/runtime:9.0)

---

*Stack analysis: 2026-01-29*
