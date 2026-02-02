# Phase 6: Backend API Foundation - Context

**Gathered:** 2026-02-02
**Status:** Ready for planning

<domain>
## Phase Boundary

Establish the backend control plane infrastructure (ASP.NET Core REST API + SignalR WebSocket hub + Kubernetes API integration) that provides the foundation for Phase 7's React dashboard. This phase delivers backend services only - no UI or user-facing features.

**Scope:**
- REST API for configuration and control operations
- SignalR hub for real-time event broadcasting to connected clients
- Kubernetes API integration to discover and query existing protocol servers
- RBAC ServiceAccount with appropriate permissions
- Health status collection and broadcasting system

**Not in scope (other phases):**
- React dashboard UI (Phase 7)
- File operations (Phase 8)
- Historical data storage (Phase 9)
- Dynamic server creation (Phase 11)

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion

User has full confidence in Claude's implementation approach. All backend architecture decisions are delegated:

- **API design patterns**: REST endpoint structure, versioning approach, authentication/authorization strategy
- **SignalR hub behavior**: Event broadcasting patterns, connection lifecycle, message formats, reconnection handling
- **Health check implementation**: Protocol server validation approach (TCP vs application-level), polling frequency, failure detection
- **Kubernetes discovery**: Label selectors, service discovery patterns, metadata exposure
- **Error handling and logging**: API error responses, SignalR error broadcasting, logging verbosity, structured logging

**Guidance from research:**
- Use ASP.NET Core 9.0 with built-in SignalR (no separate WebSocket server)
- KubernetesClient NuGet package (official .NET Kubernetes API library)
- Deploy in file-simulator namespace alongside existing v1.0 servers
- RBAC permissions: read pods/deployments/services, create/update for dynamic management (Phase 11)

</decisions>

<specifics>
## Specific Ideas

No specific requirements provided - user trusts standard ASP.NET Core + SignalR + Kubernetes patterns.

**Key constraints from research:**
- Must coexist with v1.0 servers (7 NAS + 6 protocols) without resource conflicts
- Current Minikube: 8GB RAM, 4 CPU - adequate for Phase 6-9
- Success criterion: v1.0 servers remain fully operational (no performance degradation)

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 06-backend-api-foundation*
*Context gathered: 2026-02-02*
