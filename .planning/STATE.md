# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-02)

**Core value:** Development systems must connect to simulated NAS servers using identical PV/PVC configurations as production OCP, with test files written on Windows immediately visible through NFS mounts - zero deployment differences between dev and prod.

**Current focus:** Phase 13 - TestConsole Modernization and Release

## Current Position

Phase: 13 of 13 (TestConsole Modernization and Release)
Plan: 4 of 8 complete
Status: In progress
Last activity: 2026-02-05 - Completed 13-04-PLAN.md (Kafka integration tests)

Progress: [■■■■■■■■■■■■] 100% (54 of 54 plans complete)

## Performance Metrics

**Velocity:**
- Total plans completed: 54
- Average duration: 5.1 min
- Total execution time: 5.2 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 1. NFS Pattern Validation | 3 | 38 min | 12.7 min |
| 2. Multi-NAS Architecture | 3 | 42 min | 14.0 min |
| 3. Bidirectional Sync | 2 | 20 min | 10.0 min |
| 4. Static PV/PVC Provisioning | 2 | 18 min | 9.0 min |
| 5. Comprehensive Testing | 1 | 6 min | 6.0 min |
| 6. Backend API Foundation | 3 | 17 min | 5.7 min |
| 7. Real-Time Monitoring Dashboard | 4 | 12 min | 3.0 min |
| 8. File Operations and Event Streaming | 5 | 21 min | 4.2 min |
| 9. Historical Metrics and Storage | 6 | 38.5 min | 6.4 min |
| 10. Kafka Integration | 7 | 36 min | 5.1 min |
| 11. Dynamic Server Management | 6 | 39 min | 6.5 min |
| 12. Alerting and Production Readiness | 9 | 35.3 min | 3.9 min |
| 13. TestConsole Modernization | 4 | 18 min | 4.5 min |

**Recent Trend:**
- Last 5 plans: [2.2, 3.6, 7.0, 3.0, 4.0] min
- Trend: Fast execution pace maintained

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- v1.0: 7 NAS servers (3 input, 1 backup, 3 output) matches production network configuration
- v1.0: Init container + sidecar sync architecture prevents loops, proper lifecycle ordering
- v1.0: kubectl --context mandatory for multi-profile Minikube safety
- v2.0: Control plane deploys in same file-simulator namespace (simplified RBAC and service discovery)
- v2.0: SignalR built into ASP.NET Core (no separate WebSocket server needed)
- v2.0: SQLite embedded database (no separate container, dev-appropriate)
- v2.0: Increase Minikube to 12GB before Phase 10 (Kafka requires ~1.5-2GB)
- Phase 6-01: KubernetesClient 18.0.13 (upgraded from 13.0.1 to fix security vulnerability)
- Phase 6-01: Non-root container user (appuser:1000) for Kubernetes security best practices
- Phase 6-01: CORS allow any origin for Phase 7 dashboard development
- Phase 6-03: TCP-level health checks (5s timeout) instead of protocol-specific for simplicity
- Phase 6-03: 5-second SignalR broadcast interval for Phase 7 dashboard real-time updates
- Phase 6-03: Label selector app.kubernetes.io/part-of=file-simulator-suite for server discovery
- Phase 7-01: React 19 for latest hooks and automatic memoization benefits
- Phase 7-01: Vite 6 over CRA for 10x faster dev server and native ESM
- Phase 7-01: Custom useSignalR hook over third-party packages for full control
- Phase 7-01: Reconnection retry intervals [0, 2, 5, 10, 30] seconds for backoff
- Phase 7-02: BEM-style CSS class naming for component styling consistency
- Phase 7-02: CSS Grid auto-fit for responsive card layout without media queries
- Phase 7-03: Plain text credentials for dev environment convenience
- Phase 7-03: Dual connection strings (cluster internal + Minikube external)
- Phase 7-04: CSS custom properties for consistent theming across all components
- Phase 7-04: 400px panel width with full-width on mobile
- Phase 7-04: Sticky header z-index 100, panel z-index 200 for proper layering
- Phase 8-01: 500ms debounce delay for FileSystemWatcher events
- Phase 8-01: 64KB InternalBufferSize for FileSystemWatcher (max for network shares)
- Phase 8-01: Protocol visibility mapped by directory (nas-input-1 -> NAS only, input -> all protocols)
- Phase 8-02: 100MB file upload limit via Kestrel MaxRequestBodySize
- Phase 8-02: Path validation using GetFullPath + StartsWith for security
- Phase 8-02: Hidden directory filtering (.minio.sys, .deleted)
- Phase 8-03: react-dropzone 14.4.0 for drag-drop file upload UI
- Phase 8-03: react-arborist 3.4.3 for hierarchical file tree browser
- Phase 8-03: 50-event rolling buffer for file event feed history
- Phase 8-04: react-arborist for tree view, react-dropzone for upload
- Phase 8-05: Tab-based navigation for multi-view dashboard
- Phase 8-05: 350px sidebar for file event feed (compact info display)
- Phase 9-01: DateTime (UTC) for SQLite timestamps (DateTimeOffset cannot be ordered/compared)
- Phase 9-01: IDbContextFactory pattern for background service compatibility
- Phase 9-01: Snake_case table/column names in EF Core Fluent API
- Phase 9-01: Composite indexes on (ServerId, Timestamp) for time-range queries
- Phase 9-02: 5min initial delay for RollupGenerationService to let system stabilize
- Phase 9-02: 10min initial delay for RetentionCleanupService
- Phase 9-02: Linear interpolation for P95 percentile calculation
- Phase 9-02: hostPath volume for database persistence (matches simulator-data pattern)
- Phase 9-03: 7-day limit on raw sample queries (use hourly rollups for longer ranges)
- Phase 9-03: ServerType filter applied post-query for flexibility
- Phase 9-03: Single MetricsSample broadcast per cycle (all servers in one event)
- Phase 9-04: any type for Recharts mouse handlers (complex internal types)
- Phase 9-04: 13 chart colors for multi-server view support
- Phase 9-04: Auto-resolution threshold 24h (raw for short, hourly for long ranges)
- Phase 9-05: Sparkline click navigates to History tab with server filter pre-selected
- Phase 10-01: ZooKeeper sidecar pattern for simplified Kafka lifecycle in single pod
- Phase 10-02: Confluent.Kafka 2.12.0 for Kafka client library
- Phase 10-02: AdminClient for topic/consumer group management
- Phase 10-02: IProducer with EnableIdempotence for exactly-once message delivery
- Phase 10-02: ConsumerId property (not MemberId) in Confluent.Kafka 2.12.0 MemberDescription
- Phase 10-03: Unique consumer group ID per request to avoid offset conflicts
- Phase 10-03: SignalR groups by topic name for targeted message delivery
- Phase 10-03: Background streaming task for topic subscriptions
- Phase 10-04: Lag color thresholds: green <= 10, yellow <= 100, red > 100
- Phase 10-04: 5-second auto-refresh interval for topics and consumer groups
- Phase 10-04: 50-message default rolling buffer for streaming messages
- Phase 10-04: Separate CSS file for KafkaTab (KafkaTab.css)
- Phase 10-05: Inline delete confirmation for simpler UX than modal dialog
- Phase 10-05: 3-column grid layout (280px | 1fr | 280px) for KafkaTab
- Phase 10-05: Retain message key after send for batch testing convenience
- Phase 10-06: Produce/Consume view toggle in center panel for single topic selection
- Phase 10-06: Live (SignalR) and Manual (REST) message viewing modes
- Phase 10-06: Expandable consumer group detail pattern for space efficiency
- Phase 11-01: FluentValidation for server creation request validation
- Phase 11-01: RBAC expanded with deployments, services verbs for dynamic server management
- Phase 11-02: OwnerReferences point to POD (not Deployment) for proper K8s garbage collection
- Phase 11-02: IKubernetes registered as singleton for sharing between services
- Phase 11-02: Dynamic resources labeled with app.kubernetes.io/managed-by=control-api
- Phase 11-03: SFTP uses atmoz/sftp with Args format user:pass:uid:gid (not env vars)
- Phase 11-03: NAS SubPath on shared PVC for directory isolation (no dedicated PVCs)
- Phase 11-03: Directory presets: input/output/backup resolve to nas-*-dynamic subdirs
- Phase 11-03: DeleteServerAsync explicitly deletes services first (no cascade)
- Phase 11-04: ConfigMapUpdateService updates {releasePrefix}-endpoints ConfigMap on all operations
- Phase 11-04: V1Scale + MergePatch for deployment scale (not direct replicas property)
- Phase 11-04: StopServerAsync scales to 0, StartServerAsync scales to 1, RestartServerAsync deletes pods
- Phase 11-06: Debounced name availability check (300ms) for CreateServerModal
- Phase 11-06: Inline editing in ServerDetailsPanel for dynamic servers only
- Phase 11-06: Helm servers shown as read-only with notice in details panel
- Phase 11-07: Set<string> for multi-select state with O(1) lookup
- Phase 11-07: canSelect predicate defaults to isDynamic check (protect Helm servers)
- Phase 11-07: Delete confirmation asks about NAS file cleanup per deletion
- Phase 11-07: Server source identified by name prefix (file-sim- = Helm)
- Phase 11-08: Per-conflict resolution for import (skip/replace/rename per server, not global strategy)
- Phase 11-08: Export downloads JSON via Content-Disposition header
- Phase 11-08: Settings panel accessible from header gear icon
- Phase 11-10: ValidateImportAsync returns ImportValidation with willCreate and conflicts arrays
- Phase 11-10: Delete dialog uses serverNames[0] for single-via-multiselect scenario
- Phase 12-01: Three alert severity levels (Info/Warning/Critical) for operational scenarios
- Phase 12-01: Alert deduplication by Type+Source prevents duplicate alerts
- Phase 12-01: 3 consecutive failures (15s) threshold for server health alerts
- Phase 12-01: 1GB disk space warning threshold for development/testing
- Phase 12-01: TCP-based Kafka health check with 5s timeout
- Phase 12-01: 7-day alert retention with automatic cleanup
- Phase 12-01: EF Core Migrate() instead of EnsureCreated for schema versioning
- Phase 12-02: Redis disabled by default (enable for multi-replica scale-out)
- Phase 12-02: ConnectionStrings:Redis configuration pattern for SignalR backplane
- Phase 12-02: redis:7-alpine image for minimal Redis deployment
- Phase 12-03: Sonner toast library for alert notifications (smaller bundle, better TypeScript support)
- Phase 12-03: Severity-based toast durations (Info: 5s, Warning: 10s, Critical: infinite)
- Phase 12-03: Sticky alert banner positioned at top=60px (z-index 90 below panel)
- Phase 12-03: useAlertStream hook for multi-event SignalR subscriptions (AlertTriggered/Resolved)
- Phase 12-04: react-error-boundary 6.1.0 for component error handling
- Phase 12-04: withErrorBoundary HOC pattern for tab-level error isolation
- Phase 12-04: 50 alerts per page for Alerts tab pagination
- Phase 12-04: Severity/type/search filters for alert history investigation
- Phase 12-05: Multi-stage Docker build reduces image from ~800MB to 27.3MB
- Phase 12-05: nginx:alpine for minimal footprint static file serving
- Phase 12-05: VITE_API_BASE_URL build argument for environment-specific API endpoints
- Phase 12-05: Immutable cache (1 year) for hashed assets, no-cache for index.html
- Phase 12-05: wget-based HEALTHCHECK for Kubernetes readiness/liveness probes
- Phase 12-05: SPA routing via try_files fallback to index.html
- Phase 12-06: NFS emptyDir fix integrated into nas.yaml (eliminates manual patch requirement)
- Phase 12-06: Dashboard runs as nginx user 101 with readOnlyRootFilesystem security
- Phase 12-06: localhost:5000 registry for dashboard and control API images
- Phase 12-07: Background job for registry port-forwarding during deployment
- Phase 12-07: 5m timeout for Helm deployment with 30s stabilization delay
- Phase 12-07: Graceful handling when not running as Administrator
- Phase 12-08: ErrorActionPreference Continue for comprehensive test collection
- Phase 12-08: 37 standard tests, 5 additional tests with -IncludeDynamic flag
- Phase 12-08: TCP port testing for protocols without HTTP health endpoints
- Phase 12-08: Test dependencies handled via conditional execution
- Phase 13-01: API-first configuration with fallback to appsettings.json for offline use
- Phase 13-01: PropertyNameCaseInsensitive JSON deserialization for robust API consumption
- Phase 13-01: --api-url and --require-api command-line flags for flexible configuration
- Phase 13-04: Confluent.Kafka 2.12.0 matches ControlApi version for consistency
- Phase 13-04: Dual Kafka testing (direct broker + Control API) for comprehensive coverage
- Phase 13-04: --kafka flag for standalone tests, --skip-kafka to exclude from default suite

### Pending Todos

1. **Upgrade to .NET 10 SDK** - When next working on backend (Control API), upgrade from .NET 9 to .NET 10 SDK.
2. **Fix Verify-Production.ps1 script bugs** - Unicode characters causing parsing errors, `$Host` variable conflict, and incorrect API endpoint paths need fixing.

### Blockers/Concerns

**Phase 6-12 (v2.0 control platform):**
- Resource constraints: Current Minikube 8GB sufficient for phases 6-9, must increase to 12GB before phase 10 (Kafka)
- Integration risk: Each phase must validate v1.0 servers (7 NAS + 6 protocols) remain responsive
- FileSystemWatcher tuning: Windows + Minikube 9p mount buffer overflow threshold needs empirical testing in Phase 8
- Kafka memory allocation: Minimal JVM heap (512MB vs 768MB) needs profiling under development workload in Phase 10
- ownerReferences validation: Phase 11 dynamic resources must set controller references to prevent orphaned pods

## Session Continuity

Last session: 2026-02-05
Stopped at: Completed 13-04-PLAN.md - TestConsole Kafka integration tests
Resume file: None
