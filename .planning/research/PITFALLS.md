# Domain Pitfalls: Adding Control Platform to Kubernetes File Simulator

**Domain:** Real-time monitoring and control platform for existing Kubernetes multi-protocol file simulator
**Researched:** 2026-02-02
**Milestone:** v2.0 Simulator Control Platform
**Context:** Adding React dashboard, WebSocket streaming, Kubernetes API orchestration, Kafka, and file watching to stable v1.0 simulator
**Confidence:** HIGH

## Executive Summary

This document catalogs critical mistakes when **adding a monitoring/control platform to an existing, working Kubernetes application**. Unlike greenfield development, the primary risk is **breaking what already works** while introducing complex new capabilities (real-time dashboards, dynamic resource management, Kafka integration, Windows file watching).

**Key risk categories:**
1. **Integration risks** - New platform breaking existing protocol servers
2. **Resource exhaustion** - Minikube memory/CPU constraints
3. **WebSocket complexity** - Connection management, state synchronization, race conditions
4. **Kubernetes RBAC** - Permissions for dynamic resource creation
5. **Windows file watching** - Performance issues, event floods
6. **Kafka overhead** - JVM memory in constrained environments

---

## Critical Pitfalls

Mistakes that cause rewrites, complete failures, or break existing working functionality.

### Pitfall 1: WebSocket Connection Storms During Reconnection

**What goes wrong:** When WebSocket disconnects and multiple clients reconnect simultaneously, each client triggers full state synchronization, overwhelming the backend with duplicate queries and causing cascading failures that bring down both the control platform AND the existing protocol servers.

**Why it happens:**
- **Root cause:** Naive reconnection logic fetches complete state (all servers, files, metrics) on every reconnect without coordination
- **Amplification:** N clients × M servers × K metrics = exponential query load
- **Resource competition:** Control platform queries steal CPU/memory from existing FTP/SFTP/NFS servers sharing the same Minikube cluster
- **Kubernetes API overload:** Mass reconnection floods Kubernetes API with pod/service queries, triggering API server rate limiting that affects ALL cluster operations

**Consequences:**
- Existing protocol servers become unresponsive during dashboard reconnections
- FTP/SFTP connections timeout or drop
- NFS mounts show "Stale file handle" errors
- Kubernetes API rate limiting blocks legitimate operations
- Dashboard shows "Loading..." forever, never recovers
- Cascading failure: control platform failure breaks existing stable infrastructure

**How to avoid:**
1. **IMPLEMENT** exponential backoff with jitter for reconnection attempts
2. **DESIGN** incremental state sync: send only changes since last connection, not full state
3. **ADD** connection admission control: limit concurrent WebSocket connections (e.g., max 50)
4. **CACHE** Kubernetes API responses with 5-10s TTL; don't query on every WebSocket message
5. **SEPARATE** resource limits: control platform in separate namespace with isolated CPU/memory quotas
6. **TEST** reconnection storms explicitly: disconnect 10 clients, reconnect simultaneously, verify backend remains stable

**Warning signs:**
- Backend logs show spike in API queries during reconnection
- CPU usage spikes when dashboard reconnects
- Existing protocol servers log connection timeouts
- Prometheus metrics show Kubernetes API latency increase
- Multiple clients get different states simultaneously (cache inconsistency)

**Phase to address:** Phase 1 (Backend API & WebSocket Infrastructure) - implement connection management patterns before building features on top

**Recovery cost:** HIGH - requires architectural redesign of state synchronization; impacts all real-time features

**Sources:**
- [WebSockets on production with Node.js](https://medium.com/voodoo-engineering/websockets-on-production-with-node-js-bdc82d07bb9f)
- [How I scaled a legacy NodeJS application handling over 40k active Long-lived WebSocket connections](https://khelechy.medium.com/how-i-scaled-a-legacy-nodejs-application-handling-over-40k-active-long-lived-websocket-connections-aa11b43e0db0)
- [Handling Race Conditions in Real-Time Apps](https://dev.to/mattlewandowski93/handling-race-conditions-in-real-time-apps-49c8)

---

### Pitfall 2: Missing ownerReferences Causes Orphaned Kubernetes Resources

**What goes wrong:** When control platform dynamically creates FTP/SFTP/NAS servers at runtime without setting ownerReferences, deleting the control plane leaves zombie pods, services, and PVCs consuming resources and causing confusion ("Why are there 12 FTP servers when I only configured 3?").

**Why it happens:**
- **Root cause:** Kubernetes operators/controllers must explicitly set `metadata.ownerReferences` to establish parent-child relationships; it's not automatic
- **Missing cleanup:** Without ownerReferences, Kubernetes garbage collector doesn't know that child resources should be deleted when parent is removed
- **Namespace restrictions:** ownerReferences cannot cross namespace boundaries; cluster-scoped owners required for cluster-scoped resources
- **Forgotten during iteration:** Developer creates resources successfully in Phase 3, but cleanup code never written; issue discovered months later

**Consequences:**
- Minikube runs out of resources (8GB memory exhausted by zombie pods)
- Helm uninstall doesn't clean up dynamically-created resources
- Port conflicts: new servers can't bind to ports occupied by orphaned services
- Namespace deletion hangs for hours (Kubernetes waiting for finalizers)
- Cost accumulation: orphaned LoadBalancer services incur cloud charges in production
- Configuration drift: actual running resources don't match declared configuration

**How to avoid:**
1. **ALWAYS** call `controllerutil.SetControllerReference(owner, resource, scheme)` before creating Kubernetes resources
2. **VALIDATE** ownerReferences in integration tests: create resource, delete owner, verify child cleaned up
3. **IMPLEMENT** finalizers for custom cleanup logic beyond standard cascading delete
4. **USE** `propagationPolicy: Foreground` for deletion to ensure children deleted before parent
5. **LABEL** all dynamically-created resources with `app.kubernetes.io/managed-by=file-simulator-control-plane`
6. **AUDIT** regularly: `kubectl get all --all-namespaces -l app.kubernetes.io/managed-by=file-simulator-control-plane` and verify against expected state
7. **DOCUMENT** cleanup procedures for manual recovery when automation fails

**Example configuration:**
```go
// CORRECT - Sets owner reference
import "sigs.k8s.io/controller-runtime/pkg/controller/controllerutil"

func (r *ControlPlaneReconciler) createFTPServer(ctx context.Context, spec ServerSpec) error {
    pod := &corev1.Pod{
        ObjectMeta: metav1.ObjectMeta{
            Name: spec.Name,
            Namespace: spec.Namespace,
        },
        Spec: spec.PodSpec,
    }

    // This is CRITICAL - establishes parent-child relationship
    if err := controllerutil.SetControllerReference(r.ControlPlane, pod, r.Scheme); err != nil {
        return err
    }

    return r.Client.Create(ctx, pod)
}
```

**Warning signs:**
- `kubectl get all` shows resources not in Helm chart
- Namespace deletion stuck in "Terminating" state
- Resource count grows over time without corresponding configuration changes
- Helm list shows release deleted but resources still exist
- PVC count exceeds expected number

**Phase to address:** Phase 3 (Dynamic Server Management) - before implementing ANY dynamic resource creation

**Recovery cost:** MEDIUM - Requires manual identification and deletion of orphaned resources, then code changes to prevent recurrence

**Sources:**
- [Orphaned Resources in Kubernetes](https://www.stackstate.com/blog/orphaned-resources-in-kubernetes-detection-impact-and-prevention-tips/)
- [Garbage Collection - Kubernetes](https://kubernetes.io/docs/concepts/architecture/garbage-collection/)
- [Owner References - Kubernetes Training](https://www.nakamasato.com/kubernetes-training/kubernetes-features/owner-references/)
- [Ordered cleanup with OwnerReference](https://kubebyexample.com/learning-paths/operator-framework/operator-sdk-go/ordered-cleanup-ownerreference)

---

### Pitfall 3: Windows FileSystemWatcher Buffer Overflow with High-Volume Directories

**What goes wrong:** FileSystemWatcher monitoring C:\simulator-data with 1000+ files loses events when buffer overflows, causing the dashboard to show stale/incorrect file states while Windows testers see different reality, eroding trust in the monitoring platform.

**Why it happens:**
- **Root cause:** FileSystemWatcher uses a fixed-size kernel buffer (default 8KB) to queue file change events; buffer overflows when event rate exceeds processing rate
- **High-volume scenario:** Test suite creates 500 files in 2 seconds → 500 events queued → buffer overflow → InternalBufferOverflowException
- **Missed events:** When buffer overflows, FileSystemWatcher reports only "something changed" without specifics, then requires full directory rescan
- **Event duplication:** Windows generates multiple events per file operation (created, attributes changed, modified), multiplying event volume
- **Network share overhead:** Watching Windows directories mounted in Minikube via 9p/CIFS adds latency to event processing

**Consequences:**
- Dashboard shows File A exists, but it was actually deleted 5 minutes ago
- File upload appears to succeed on Windows but dashboard never shows it
- InternalBufferOverflowException crashes file watcher sidecar, requiring pod restart
- Race condition: user uploads file, checks dashboard immediately, file not shown, reports bug
- Performance degradation: full directory rescans every time buffer overflows
- Lost audit trail: file events missing from Kafka stream used for compliance reporting

**How to avoid:**
1. **INCREASE** buffer size to maximum (64KB for local, network share limit): `watcher.InternalBufferSize = 65536`
2. **BATCH** events with 100-200ms debounce: collect duplicates, process once
3. **QUEUE** events to Channel or ConcurrentQueue: keep handlers fast (< 1ms), process async
4. **FILTER** before watching: use NotifyFilter to ignore irrelevant events (LastAccess, Security)
5. **PARTITION** watching: watch subdirectories separately instead of single root directory
6. **IMPLEMENT** overflow recovery: on InternalBufferOverflowException, trigger incremental directory diff
7. **ALERT** on overflow: emit metric to Prometheus when buffer overflows occur
8. **TEST** with realistic load: script that creates 1000 files in 10 seconds, verify all events captured

**Example configuration:**
```csharp
// CORRECT - Handles high-volume scenarios
var watcher = new FileSystemWatcher(@"C:\simulator-data")
{
    InternalBufferSize = 65536, // Maximum buffer size
    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size, // Ignore attributes, security
    IncludeSubdirectories = true
};

// Use concurrent queue for async processing
var eventQueue = new Channel<FileSystemEventArgs>(1000);

watcher.Changed += (s, e) => eventQueue.Writer.TryWrite(e);
watcher.Created += (s, e) => eventQueue.Writer.TryWrite(e);
watcher.Deleted += (s, e) => eventQueue.Writer.TryWrite(e);

// Handle buffer overflow gracefully
watcher.Error += (s, e) => {
    if (e.GetException() is InternalBufferOverflowException) {
        logger.LogWarning("FileSystemWatcher buffer overflow - triggering directory rescan");
        TriggerIncrementalRescan();
    }
};

// Process queue with batching and debounce
await foreach (var batch in eventQueue.Reader.ReadBatchAsync(batchSize: 50, timeout: 200ms)) {
    var uniqueFiles = batch.DistinctBy(e => e.FullPath);
    await ProcessFileEvents(uniqueFiles);
}
```

**Warning signs:**
- Logs show InternalBufferOverflowException
- Event count metrics show sudden drops (missed events)
- File state in database doesn't match Windows directory listing
- Users report "my file uploaded but dashboard doesn't show it"
- CPU spikes on file watcher pod during test runs

**Phase to address:** Phase 2 (File Event Streaming) - before deploying to testers

**Recovery cost:** MEDIUM - Can usually increase buffer size and add batching without architectural changes

**Sources:**
- [FileSystemWatcher not working when large amount of files are changed](https://learn.microsoft.com/en-us/archive/msdn-technet-forums/60b12c2e-9dc8-4de4-be2d-bf8345bdaaee)
- [Tamed FileSystemWatcher](https://petermeinl.wordpress.com/2015/05/18/tamed-filesystemwatcher/)
- [FileSystemWatcher Class - .NET](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-io-filesystemwatcher)

---

### Pitfall 4: Kafka Consumes Excessive Memory in Minikube, Starving Existing Services

**What goes wrong:** Single-broker Kafka in KRaft mode configured with default JVM heap (1GB) plus off-heap memory consumes 1.5-2GB RAM, pushing Minikube over 8GB limit and causing OOM kills of existing FTP/SFTP/NAS servers that were stable in v1.0.

**Why it happens:**
- **Root cause:** Kafka's default configuration optimized for production (large heap, multiple partitions, replication) far exceeds needs of development file simulator
- **JVM overhead:** Java heap (1GB) + metaspace (256MB) + direct buffers (512MB) + OS page cache = ~2GB actual usage
- **Memory limit trap:** Setting container `memory: 768Mi` but JVM `-Xmx1g` causes OOMKilled (container limit < JVM heap)
- **Replication factor mistake:** Setting `offsets.topic.replication.factor=3` in single-broker cluster causes startup failure
- **Resource competition:** v1.0 uses ~2.8GB (706Mi requests, 2.85Gi limits); adding Kafka's 2GB breaks everything

**Consequences:**
- Minikube node runs out of memory: existing pods evicted randomly
- FTP server killed mid-transfer, corrupting files
- NFS server dies during mount operation, causing kernel panic on client
- Kafka itself OOMKilled in tight loop, never fully starting
- Kubernetes scheduler thrashing: evict pod A to schedule pod B, evict B to schedule A
- Development environment unusable: "worked yesterday, broken today" after adding Kafka

**How to avoid:**
1. **MINIMAL** JVM heap for single-broker dev: `-Xmx512m -Xms512m`
2. **MATCH** container limits to JVM heap: `memory.limit = 768Mi` for `-Xmx512m`
3. **SINGLE** partition/replica for dev topics: `num.partitions=1`, `default.replication.factor=1`
4. **CONFIGURE** offsets topic: `offsets.topic.replication.factor=1` (not 3)
5. **DISABLE** unnecessary features: `auto.create.topics.enable=false`, compression
6. **INCREASE** Minikube memory to 12GB if adding Kafka: `minikube start --memory=12288`
7. **PROFILE** actual usage: `kubectl top pod` to measure real consumption vs. configured limits
8. **SEPARATE** namespace with ResourceQuota: limit control-plane namespace to 4GB, file-simulator to 6GB

**Example configuration:**
```yaml
# Minimal Kafka for Minikube - development ONLY
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: kafka
spec:
  template:
    spec:
      containers:
      - name: kafka
        image: apache/kafka:3.8.1
        env:
        - name: KAFKA_HEAP_OPTS
          value: "-Xmx512m -Xms512m -XX:MaxMetaspaceSize=128m"
        - name: KAFKA_JVM_PERFORMANCE_OPTS
          value: "-XX:+UseG1GC -XX:MaxGCPauseMillis=20"
        - name: KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR
          value: "1"  # CRITICAL for single broker
        - name: KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR
          value: "1"
        - name: KAFKA_TRANSACTION_STATE_LOG_MIN_ISR
          value: "1"
        - name: KAFKA_NUM_PARTITIONS
          value: "1"
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "768Mi"  # Slightly more than heap for overhead
            cpu: "500m"
```

**Warning signs:**
- Kafka pod in CrashLoopBackOff with OOMKilled status
- Existing protocol server pods evicted during Kafka startup
- `kubectl top node` shows memory usage > 90%
- Kafka logs show "OutOfMemoryError: Java heap space"
- FTP/SFTP connections start failing after Kafka deployment

**Phase to address:** Phase 4 (Kafka Integration) - configure minimal resource profile BEFORE deploying

**Recovery cost:** LOW - Configuration change, but requires Minikube restart with more memory

**Sources:**
- [Solving Java Out of Memory Issues in Kafka-Powered Microservices](https://medium.com/@msbreuer/solving-java-out-of-memory-issues-in-kafka-powered-microservices-c6911882c174)
- [Kafka pod is constantly created and destroyed](https://github.com/banzaicloud/koperator/issues/112)
- [Configure CPU and Memory for Confluent Platform in Kubernetes](https://docs.confluent.io/operator/current/co-resources.html)
- [Running Kafka in Kubernetes with KRaft mode](https://rafael-natali.medium.com/running-kafka-in-kubernetes-with-kraft-mode-549d22ab31b0)

---

### Pitfall 5: React WebSocket State Update Race Conditions

**What goes wrong:** Dashboard displays incorrect server state (shows FTP server as "running" when actually stopped) due to race condition: user clicks "Stop Server", API processes request, WebSocket broadcasts "stopping" event, but React component's subsequent state update overwrites with stale "running" state from previous fetch.

**Why it happens:**
- **Root cause:** Asynchronous state updates in React combined with out-of-order WebSocket messages create race between pessimistic UI update and authoritative backend state
- **Fetch-first pattern trap:** Component fetches state on mount, then subscribes to WebSocket; events that occurred between fetch and subscription are lost
- **Event reordering:** Network delays cause "server stopped" event to arrive before "server stopping" event
- **State update batching:** React batches setState calls; later update overwrites earlier update instead of merging
- **Multiple sources of truth:** REST API, WebSocket stream, and local component state diverge

**Consequences:**
- User clicks "Start FTP server", sees "Starting..." for 2 seconds, then UI reverts to "Stopped" (actual server running)
- Stop button appears but server already stopped → 404 error when clicked
- File upload progress bar shows 100% but backend still processing
- Dashboard shows 7 NAS servers when 3 were deleted (zombie UI state)
- User loses trust in monitoring platform: "dashboard is always wrong"

**How to avoid:**
1. **FETCH** state, then subscribe to WebSocket, then **refetch** to catch gap events (3-step pattern)
2. **USE** functional state updates: `setState(prev => merge(prev, update))` not `setState(update)`
3. **SEQUENCE** events with monotonic version numbers or timestamps; discard out-of-order updates
4. **SINGLE** source of truth: WebSocket stream is authoritative, REST API only for initial load
5. **IMPLEMENT** event cache/queue: buffer WebSocket messages during initial fetch, replay after
6. **OPTIMISTIC** UI updates with rollback: update UI immediately, revert if backend rejects
7. **TEST** race scenarios explicitly: mock delayed WebSocket messages, verify UI consistency

**Example implementation:**
```typescript
// CORRECT - Handles race conditions with event cache and refetch
function useServerState(serverId: string) {
  const [state, setState] = useState<ServerState | null>(null);
  const eventCache = useRef<ServerEvent[]>([]);

  useEffect(() => {
    let mounted = true;

    // Step 1: Initial fetch
    const initialState = await fetchServerState(serverId);
    if (!mounted) return;

    // Step 2: Subscribe to WebSocket
    const unsubscribe = wsSubscribe(`server.${serverId}`, (event) => {
      if (!state) {
        // Cache events until initial state loaded
        eventCache.current.push(event);
      } else {
        // Apply event with functional update to avoid race
        setState(prev => applyEvent(prev, event));
      }
    });

    // Step 3: Refetch to catch gap events
    const gapState = await fetchServerState(serverId);
    if (!mounted) return;

    setState(gapState);

    // Replay cached events
    eventCache.current.forEach(event => {
      setState(prev => applyEvent(prev, event));
    });
    eventCache.current = [];

    return () => {
      mounted = false;
      unsubscribe();
    };
  }, [serverId]);

  return state;
}
```

**Warning signs:**
- Sentry/LogRocket shows UI state not matching backend state
- Users report "button clicked but nothing happened"
- E2E tests flaky: sometimes pass, sometimes fail with wrong state
- Console shows "setState called after unmount" warnings
- Redux DevTools shows action dispatched but state unchanged

**Phase to address:** Phase 1 (Backend API & WebSocket) - establish patterns before building complex features

**Recovery cost:** HIGH - Requires refactoring state management across entire dashboard

**Sources:**
- [Real-time State Management in React Using WebSockets](https://moldstud.com/articles/p-real-time-state-management-in-react-using-websockets-boost-your-apps-performance)
- [Handling Race Conditions in Real-Time Apps](https://dev.to/mattlewandowski93/handling-race-conditions-in-real-time-apps-49c8)
- [Handling State Update Race Conditions in React](https://medium.com/cyberark-engineering/handling-state-update-race-conditions-in-react-8e6c95b74c17)
- [Using WebSockets with React Query](https://tkdodo.eu/blog/using-web-sockets-with-react-query)

---

### Pitfall 6: Missing RBAC Permissions for Dynamic Resource Creation

**What goes wrong:** Control plane ServiceAccount can query existing pods/services but cannot create new ones, causing "forbidden: User 'system:serviceaccount:file-simulator:control-plane' cannot create resource 'pods'" error when user clicks "Add FTP Server" in dashboard, with cryptic error message and no UI feedback.

**Why it happens:**
- **Root cause:** Kubernetes RBAC follows principle of least privilege; ServiceAccount has NO permissions by default except to query its own token
- **Read vs write separation:** Many examples show read-only permissions (get, list, watch) but omit write permissions (create, update, delete) needed for control plane
- **Namespace scoping mistake:** Role grants permissions in one namespace but control plane tries to create resources in different namespace
- **Forgotten verbs:** Developer adds "create" but forgets "update" needed to patch existing resources or "deletecollection" for bulk operations
- **Resource subresources:** "pods" permission doesn't grant "pods/log" or "pods/exec" which monitoring might need

**Consequences:**
- "Add Server" button silently fails; user sees spinner forever
- Cryptic 403 errors in browser console with no user-facing message
- Dashboard shows "Creating..." indefinitely; server never appears
- Partial state: Service created but Deployment failed → orphaned service
- Security audit fails: attempting to use cluster-admin in production for "simplicity"

**How to avoid:**
1. **CREATE** Role with explicit verbs for ALL operations:
   ```yaml
   apiVersion: rbac.authorization.k8s.io/v1
   kind: Role
   metadata:
     name: control-plane-manager
     namespace: file-simulator
   rules:
   - apiGroups: [""]
     resources: ["pods", "services", "persistentvolumeclaims", "configmaps"]
     verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]
   - apiGroups: ["apps"]
     resources: ["deployments", "statefulsets"]
     verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]
   - apiGroups: [""]
     resources: ["pods/log", "pods/status"]
     verbs: ["get"]
   ```
2. **BIND** Role to ServiceAccount with RoleBinding (NOT ClusterRoleBinding unless truly cluster-scoped)
3. **TEST** RBAC with `kubectl auth can-i --as=system:serviceaccount:file-simulator:control-plane create pods`
4. **USE** `kubectl auth reconcile -f rbac.yaml` to validate RBAC manifests before applying
5. **AUDIT** actual permissions with `kubectl describe role control-plane-manager`
6. **IMPLEMENT** graceful error handling: catch 403, show user-friendly message with remediation steps
7. **DOCUMENT** RBAC requirements prominently in installation guide for OpenShift/restricted clusters

**Warning signs:**
- Browser console shows "Error 403: Forbidden"
- Backend logs show "User cannot create resource 'pods' in namespace 'file-simulator'"
- `kubectl describe rolebinding` shows ServiceAccount not bound to any Role
- Dashboard operations work in admin context but fail with ServiceAccount
- Works in Minikube (permissive) but fails in OpenShift (strict RBAC/SCCs)

**Phase to address:** Phase 3 (Dynamic Server Management) - before implementing any create/update/delete operations

**Recovery cost:** LOW - Add missing RBAC rules, but hard to debug without clear error messages

**Sources:**
- [Using RBAC Authorization](https://kubernetes.io/docs/reference/access-authn-authz/rbac/)
- [Kubernetes ServiceAccount and RBAC: Creating Pods with Controlled Permissions](https://www.jeeviacademy.com/kubernetes-serviceaccount-and-rbac-creating-pods-with-controlled-permissions/)
- [Kubernetes RBAC: the Complete Best Practices Guide](https://www.armosec.io/blog/a-guide-for-using-kubernetes-rbac/)
- [Implementing Kubernetes RBAC: Best Practices and Examples](https://trilio.io/kubernetes-best-practices/kubernetes-rbac/)

---

## Moderate Pitfalls

Mistakes that cause delays, debugging sessions, or technical debt but don't break existing functionality.

### Pitfall 7: WebSocket Connection Leaks on Component Unmount

**What goes wrong:** Dashboard user navigates between pages (Servers → Files → Metrics → Servers), each navigation creates new WebSocket connection but doesn't close old one, accumulating 50+ connections over 10 minutes and eventually hitting server's connection limit (100), blocking new users.

**Why it happens:**
- **Root cause:** React component subscribes to WebSocket in useEffect but doesn't return cleanup function
- **Development hot reload:** Webpack HMR triggers unmount/remount without cleanup, leaking connections
- **Page navigation:** React Router unmounts old page component but WebSocket stays connected
- **Framework abstraction:** Socket.io/SignalR automatic reconnection hides connection leak

**How to avoid:**
```typescript
// CORRECT - Cleanup function prevents leak
useEffect(() => {
  const socket = io('/api/ws');

  socket.on('server-update', handleUpdate);

  return () => {
    socket.off('server-update', handleUpdate); // Remove listener
    socket.disconnect(); // Close connection
  };
}, []);
```

**Warning signs:**
- Backend logs show connection count increasing without bound
- `netstat` shows ESTABLISHED connections to :8080 from same client IP
- Memory usage on backend increases linearly with time
- New users see "Connection refused" after dashboard used for hours

**Phase to address:** Phase 1 (Backend API & WebSocket Infrastructure)

---

### Pitfall 8: Kubernetes API Client Rate Limiting

**What goes wrong:** Dashboard polls Kubernetes API every 1 second for pod status across 7 NAS servers, hitting API server's 50 QPS rate limit, causing 429 errors and dashboard showing "Error loading servers" intermittently.

**How to avoid:**
1. **USE** Kubernetes watch API instead of polling: `client.CoreV1().Pods(namespace).Watch(ctx, opts)`
2. **CACHE** responses with 5-10s TTL; serve cached data to dashboard
3. **BATCH** queries: get all pods in one call, not 7 individual queries
4. **INCREASE** API server rate limits if needed (Minikube: `--extra-config=apiserver.max-requests-inflight=100`)

**Warning signs:**
- Dashboard shows "Error loading" intermittently
- Backend logs show "rate: Wait(n=1) would exceed context deadline"
- Kubernetes API server logs show "request has been rate limited"

**Phase to address:** Phase 1 (Backend API & WebSocket Infrastructure) - implement watch API from start

---

### Pitfall 9: File Watcher Sidecar Dies Silently, Events Stop Flowing

**What goes wrong:** FileSystemWatcher sidecar container crashes due to unhandled exception but pod stays in "Running" state (main container healthy), causing silent failure where file events stop flowing to Kafka but dashboard shows no error.

**How to avoid:**
1. **ADD** liveness probe for file watcher sidecar: HTTP endpoint that verifies watcher still active
2. **SET** `restartPolicy: Always` for sidecar containers (native sidecar in K8s 1.29+)
3. **EMIT** heartbeat metric every 10s to Prometheus; alert if missing
4. **LOG** to stdout/stderr properly; avoid swallowing exceptions
5. **IMPLEMENT** global exception handler with retry logic

**Warning signs:**
- File events stopped arriving in Kafka but no alerts
- `kubectl logs file-watcher-sidecar` shows exception stack trace, then silence
- Dashboard file browser stale; Windows directory has new files but UI doesn't
- Prometheus shows `file_watcher_events_total` counter stopped incrementing

**Phase to address:** Phase 2 (File Event Streaming) - add health checks immediately

---

### Pitfall 10: Configuration Drift Between Helm Values and Control Plane State

**What goes wrong:** User adds 3 FTP servers via dashboard UI (dynamic creation), then runs `helm upgrade` with old values.yaml specifying 1 FTP server, Helm deletes 2 servers without warning, losing user's configuration.

**How to avoid:**
1. **SEPARATE** static (Helm-managed) and dynamic (control-plane-managed) resources with labels
2. **ANNOTATE** dynamic resources with `helm.sh/resource-policy: keep` to prevent Helm deletion
3. **EXPORT** control plane state to values.yaml before Helm operations
4. **WARN** in UI: "This server was created dynamically. To persist, export configuration."
5. **DOCUMENT** interaction between Helm and control plane explicitly

**Warning signs:**
- Servers disappear after Helm upgrade
- `helm diff` shows deletions not expected
- Users report "my configuration keeps getting reset"

**Phase to address:** Phase 3 (Dynamic Server Management) - design label/annotation strategy from start

---

### Pitfall 11: Kafka Topic Retention Fills Minikube Disk

**What goes wrong:** Kafka file-events topic configured with infinite retention (`retention.ms=-1`) accumulates 50GB of events over weeks, filling Minikube's 20GB disk, causing all pods to evict due to disk pressure.

**How to avoid:**
1. **SET** retention for dev: `retention.ms=86400000` (1 day) or `retention.bytes=1073741824` (1GB)
2. **ENABLE** log compaction for state topics: `cleanup.policy=compact`
3. **MONITOR** disk usage: `kubectl top node`, alert at 80%
4. **DOCUMENT** retention policy in deployment guide

**Warning signs:**
- `kubectl get nodes` shows `DiskPressure=True`
- Pods evicted with "Insufficient disk space"
- `df -h` inside Minikube shows 95% usage

**Phase to address:** Phase 4 (Kafka Integration) - configure retention before deploying

---

### Pitfall 12: Control Plane API Breaks Backward Compatibility with Existing Clients

**What goes wrong:** v2.0 control plane adds `/api/servers` endpoint that conflicts with existing `/api/server/:id` endpoint used by .NET microservices from v1.0, breaking deployed applications that consume file events.

**How to avoid:**
1. **VERSION** APIs from start: `/api/v1/servers`, `/api/v2/servers`
2. **SEPARATE** control plane API (port 8080) from file operations API (port 8081)
3. **TEST** with v1.0 client scripts to verify no regressions
4. **DEPRECATE** gracefully: support old endpoints for 2 releases with warnings

**Phase to address:** Phase 1 (Backend API) - design API structure before implementing features

---

## Performance Traps

Patterns that work at small scale but fail as usage grows.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Full directory scan on every WebSocket connection | Dashboard takes 10s to load with 1000 files | Cache directory listings with 5s TTL; use incremental updates | >500 files in watched directories |
| N+1 queries for server status | Dashboard makes 7 API calls (one per NAS server) | Batch query: `GET /api/servers` returns all servers | >10 servers |
| Unbounded WebSocket message queue | Backend OOM after 1 hour of dashboard idle | Cap queue size at 1000 messages; drop old messages | Long-running connections with slow clients |
| Synchronous Kubernetes API calls in WebSocket handler | WebSocket messages delayed 100ms each | Async API calls; use channels for non-blocking communication | >10 clients connected |
| Polling Kafka for new messages every 100ms | CPU usage 50% on Kafka pod | Use Kafka consumer with long poll timeout (5s) | Continuous operation |
| Loading entire file content for preview | File browser crashes loading 10GB file | Stream file content; limit preview to first 1MB | Files >100MB |

---

## Integration Gotchas

Common mistakes when integrating control platform with existing v1.0 infrastructure.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Health checks | Control plane checks its own health, not existing FTP/SFTP servers | Implement connectivity checks to ALL protocol servers; aggregate health |
| Metrics collection | Prometheus scrapes control plane metrics only | Scrape existing servers + control plane; use federation if separate namespaces |
| Logging | Centralized logging filters by control-plane namespace, missing FTP/SFTP logs | Include both `namespace: file-simulator` AND `namespace: control-plane` in queries |
| Network policies | Adding NetworkPolicy for control plane blocks existing server-to-server communication | Use label selectors carefully; test existing communication paths after adding policies |
| DNS resolution | Control plane uses service names but external clients use NodePort IPs | Document both access patterns; provide DNS names AND IP:port for external clients |
| TLS certificates | Control plane gets cert for `control-plane.file-simulator.svc.cluster.local`, existing servers have no TLS | Either add TLS to all services OR accept mixed HTTP/HTTPS with appropriate warnings |

---

## Security Mistakes

Domain-specific security issues beyond general web security.

| Mistake | Risk | Prevention |
|---------|------|------------|
| WebSocket authentication skipped for "simplicity" | Any network client can connect and control simulator | Require JWT token in WebSocket handshake; validate on every message |
| RBAC uses cluster-admin for control plane | Control plane compromise = full cluster access | Use namespaced Role with minimal verbs (get, list, create pods/services only) |
| File operations API has no path traversal protection | `DELETE /api/files?path=../../etc/passwd` deletes host files | Validate paths within C:\simulator-data; reject paths with ".." or absolute paths |
| Kafka has no authentication | Any pod can produce fake file events | Enable SASL/SCRAM or mTLS; restrict topic access with ACLs |
| Dashboard served over HTTP in production | Credentials sent in plaintext | Require TLS; use HSTS header; redirect HTTP → HTTPS |
| Dynamically created servers inherit cluster-admin ServiceAccount | New FTP server can delete other servers | Each server gets dedicated ServiceAccount with minimal permissions |

---

## UX Pitfalls

Common user experience mistakes in monitoring/control platforms.

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| No feedback during long operations | User clicks "Start Kafka" → spinner for 60s → timeout error | Show progress stages: "Pulling image...", "Creating pod...", "Waiting for ready..." |
| Error messages show Kubernetes internals | "Error: pods 'kafka-0' is forbidden: User cannot create resource" | Translate to user terms: "Insufficient permissions. Contact admin to grant pod creation." |
| State updates without notifications | Server stops but no indication why | Show notification: "FTP server stopped due to health check failure" with link to logs |
| No undo for destructive actions | User clicks "Delete All Servers" → 7 servers deleted immediately | Confirmation dialog with server names listed; "Type 'DELETE' to confirm" |
| Dashboard requires page refresh to see changes | User creates server, must reload page to see it | WebSocket broadcasts creation event; UI updates automatically |
| No distinction between static and dynamic resources | User confused which servers were created by Helm vs UI | Visual indicator: badge showing "Helm-managed" vs "Dynamic" |

---

## "Looks Done But Isn't" Checklist

Things that appear complete but are missing critical pieces.

- [ ] **WebSocket reconnection:** Connects on first load but doesn't reconnect after network blip — verify automatic reconnection with exponential backoff
- [ ] **File operations:** Upload works but no progress indicator — verify progress events emitted and displayed
- [ ] **Server creation:** "Add Server" succeeds but server not accessible — verify Service created, ports exposed, DNS resolution works
- [ ] **Kafka integration:** Events published but no consumers — verify at least one consumer exists, messages actually processed
- [ ] **Error handling:** Success path works but errors show generic "Something went wrong" — verify specific error messages for common failures
- [ ] **Resource cleanup:** Resources created but never deleted — verify ownerReferences set, garbage collection tested
- [ ] **Health monitoring:** Dashboard shows "healthy" but FTP server unreachable — verify health check actually connects to server, not just checks pod status
- [ ] **State persistence:** Configuration saved but lost on pod restart — verify data stored in PVC or ConfigMap, not in-memory only
- [ ] **Multi-tenancy:** Works with 1 user but breaks with 10 concurrent users — verify connection limits, rate limiting, resource quotas

---

## Recovery Strategies

When pitfalls occur despite prevention, how to recover.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| WebSocket connection storm | MEDIUM | 1. Redeploy backend with connection limit. 2. Implement queue-based state sync. 3. Add rate limiting. 4. Test with load tool (artillery.io) |
| Orphaned resources | LOW | 1. List resources: `kubectl get all -l managed-by=control-plane`. 2. Identify orphans: compare to expected state. 3. Delete manually: `kubectl delete pod orphan-ftp-1`. 4. Add ownerReferences to prevent recurrence |
| FileSystemWatcher overflow | MEDIUM | 1. Restart file watcher pod. 2. Increase buffer size to 64KB. 3. Add batching with 200ms debounce. 4. Monitor overflow metrics |
| Kafka OOM | LOW | 1. Reduce heap: `KAFKA_HEAP_OPTS=-Xmx512m`. 2. Set replication factor=1. 3. Restart Kafka pod. 4. Monitor memory with `kubectl top pod` |
| React state race | HIGH | 1. Identify affected components (state audit). 2. Refactor to functional updates. 3. Implement event cache pattern. 4. Add integration tests for race scenarios |
| Missing RBAC | LOW | 1. Create Role with missing verbs. 2. Apply: `kubectl apply -f rbac.yaml`. 3. Verify: `kubectl auth can-i create pods --as=system:serviceaccount:...` |
| Configuration drift | MEDIUM | 1. Export control plane state: `GET /api/config/export`. 2. Update values.yaml. 3. Helm upgrade with merged config. 4. Document workflow |
| Kafka disk full | MEDIUM | 1. Delete old topics: `kafka-topics.sh --delete --topic old-events`. 2. Set retention policy. 3. Expand Minikube disk or increase cluster size |

---

## Pitfall-to-Phase Mapping

How roadmap phases should address these pitfalls.

| Pitfall | Prevention Phase | Verification Method |
|---------|------------------|---------------------|
| #1: WebSocket connection storms | Phase 1 (Backend API) | Load test: 50 clients disconnect/reconnect simultaneously; verify existing servers remain responsive |
| #2: Orphaned resources | Phase 3 (Dynamic Server Mgmt) | Integration test: create server dynamically, delete parent, verify child deleted within 30s |
| #3: FileSystemWatcher overflow | Phase 2 (File Event Streaming) | Stress test: create 1000 files in 10s; verify all events captured; no InternalBufferOverflowException |
| #4: Kafka memory exhaustion | Phase 4 (Kafka Integration) | Monitor `kubectl top pod` before/after Kafka deployment; verify existing servers not evicted |
| #5: React state races | Phase 1 (Backend API) | E2E test: click "Stop Server" then immediately fetch state; verify state consistent |
| #6: Missing RBAC | Phase 3 (Dynamic Server Mgmt) | `kubectl auth can-i` for all operations; test with non-admin ServiceAccount |
| #7: WebSocket leaks | Phase 1 (Backend API) | Navigate between pages 20 times; verify connection count returns to baseline |
| #8: K8s API rate limiting | Phase 1 (Backend API) | Monitor API request rate; verify <10 QPS; use watch API not polling |
| #9: Silent sidecar failure | Phase 2 (File Event Streaming) | Kill file watcher process; verify pod restarts within 30s; alert triggered |
| #10: Config drift (Helm vs UI) | Phase 3 (Dynamic Server Mgmt) | Create server in UI, run helm upgrade, verify server not deleted |
| #11: Kafka disk retention | Phase 4 (Kafka Integration) | Check topic config: `retention.ms` set; monitor disk usage over 24h |
| #12: API backward compatibility | Phase 1 (Backend API) | Run v1.0 integration tests against v2.0 API; verify no regressions |

---

## Architectural Decision Record: Control Platform vs Existing Infrastructure

**Problem:** How to add control/monitoring platform without destabilizing v1.0 multi-NAS simulator that works reliably.

**Options Evaluated:**

1. **Monolithic approach:** Add control plane to existing pods ❌
   - Risk: Breaking existing services
   - Impact: Single bug affects all protocols

2. **Separate namespace:** Deploy control plane in `control-plane` namespace ⚠️
   - Pros: Isolation, separate resource limits
   - Cons: RBAC complexity (cross-namespace access), network policies

3. **Separate Minikube profile:** Run control plane in different cluster ❌
   - Pros: Complete isolation
   - Cons: Cannot manage servers in different cluster; defeats purpose

4. **Same namespace, separate deployments:** control-plane deployment + existing servers ✅ **RECOMMENDED**
   - Pros: RBAC simplified (same namespace), easy service discovery, resource sharing
   - Cons: Must set resource limits carefully to prevent interference
   - Mitigation: ResourceQuota per application group using labels

**Recommendation:**
- Same namespace (`file-simulator`) but separate deployments
- Resource limits: control-plane (2GB), existing servers (4GB), Kafka (2GB) = 8GB total
- Label-based resource quotas if limits exceeded
- NetworkPolicy to prevent control-plane from accessing protocol server data directly (enforce API layer)

---

## Quick Reference: Troubleshooting Checklist

When control platform causes issues with existing infrastructure:

- [ ] **Check Minikube resources:** `kubectl top node` - is memory >90%? CPU >80%?
- [ ] **Verify existing services healthy:** Test FTP, SFTP, NFS, HTTP directly (not through dashboard)
- [ ] **Check WebSocket connections:** `netstat | grep :8080 | wc -l` - is count >100?
- [ ] **Monitor Kubernetes API rate:** Check apiserver metrics - hitting rate limits?
- [ ] **Verify RBAC permissions:** `kubectl auth can-i create pods --as=system:serviceaccount:file-simulator:control-plane`
- [ ] **Check orphaned resources:** `kubectl get all -l app.kubernetes.io/managed-by=control-plane` - unexpected resources?
- [ ] **File watcher health:** `kubectl logs file-watcher-sidecar` - InternalBufferOverflowException?
- [ ] **Kafka memory:** `kubectl top pod kafka-0` - is usage >90% of limit?
- [ ] **Dashboard state consistency:** Compare UI state to `kubectl get pods` - do they match?
- [ ] **Network policies:** If added, do they block existing server-to-server communication?

---

## Confidence Assessment

| Pitfall | Confidence | Evidence |
|---------|------------|----------|
| #1: WebSocket connection storms | **HIGH** | Multiple production postmortems from high-scale WebSocket applications; matches described scenario |
| #2: Orphaned Kubernetes resources | **HIGH** | Official Kubernetes documentation on garbage collection; operator best practices |
| #3: FileSystemWatcher overflow | **HIGH** | Microsoft official documentation on buffer limits; .NET issue tracker discussions |
| #4: Kafka memory in Minikube | **HIGH** | Multiple community reports of Kafka OOM in constrained environments; Confluent documentation |
| #5: React WebSocket state races | **HIGH** | React documentation on state updates; multiple blog posts from production incidents |
| #6: Missing RBAC permissions | **HIGH** | Kubernetes RBAC specification; common operator pattern |
| #7: WebSocket connection leaks | **MEDIUM** | React useEffect cleanup pattern; common mistake in community discussions |
| #8: K8s API rate limiting | **MEDIUM** | Kubernetes API documentation; rate limiting is configurable so thresholds vary |
| #9: Silent sidecar failure | **MEDIUM** | Kubernetes sidecar patterns; lack of automated health checks is common oversight |
| #10: Config drift (Helm vs UI) | **MEDIUM** | Helm documentation on resource policies; operator pattern discussions |
| #11: Kafka retention disk filling | **HIGH** | Kafka configuration documentation; common mistake in dev environments |
| #12: API backward compatibility | **MEDIUM** | General API design principle; severity depends on client coupling |

---

## Gaps and Open Questions

**Areas of HIGH confidence (v2.0 specific):**
- All critical pitfalls (#1-#6) verified with authoritative sources or production incident reports
- Integration risks between new platform and existing v1.0 infrastructure clearly documented
- Resource constraints in Minikube quantified based on v1.0 baseline measurements

**Areas requiring validation during implementation:**
- Exact WebSocket connection limit before performance degradation (varies by backend technology)
- FileSystemWatcher buffer overflow threshold with Windows + Minikube 9p mount (need empirical testing)
- Kafka minimum viable resource allocation for single-broker development (512MB heap vs 768MB vs 1GB)
- React state management patterns with multiple concurrent WebSocket streams (need prototype)

**Recommended validation activities:**
- **Phase 1:** Load test WebSocket reconnection scenarios (simulate 50 clients)
- **Phase 2:** Stress test FileSystemWatcher with 1000+ files created in 10s burst
- **Phase 4:** Profile Kafka actual memory usage in Minikube under development workload
- **Throughout:** Monitor v1.0 protocol servers (FTP, SFTP, NAS) for regressions after each phase

---

## Sources

### WebSocket & Real-Time Communication
- [WebSockets on production with Node.js - Voodoo Engineering](https://medium.com/voodoo-engineering/websockets-on-production-with-node-js-bdc82d07bb9f)
- [How I scaled a legacy NodeJS application handling over 40k active Long-lived WebSocket connections](https://khelechy.medium.com/how-i-scaled-a-legacy-nodejs-application-handling-over-40k-active-long-lived-websocket-connections-aa11b43e0db0)
- [Real-time State Management in React Using WebSockets](https://moldstud.com/articles/p-real-time-state-management-in-react-using-websockets-boost-your-apps-performance)
- [Handling Race Conditions in Real-Time Apps](https://dev.to/mattlewandowski93/handling-race-conditions-in-real-time-apps-49c8)
- [Handling State Update Race Conditions in React](https://medium.com/cyberark-engineering/handling-state-update-race-conditions-in-react-8e6c95b74c17)
- [Using WebSockets with React Query - TkDodo's blog](https://tkdodo.eu/blog/using-web-sockets-with-react-query)

### Kubernetes RBAC & Resource Management
- [Using RBAC Authorization - Kubernetes Official Documentation](https://kubernetes.io/docs/reference/access-authn-authz/rbac/)
- [Kubernetes ServiceAccount and RBAC: Creating Pods with Controlled Permissions](https://www.jeeviacademy.com/kubernetes-serviceaccount-and-rbac-creating-pods-with-controlled-permissions/)
- [Kubernetes RBAC: the Complete Best Practices Guide - ARMO](https://www.armosec.io/blog/a-guide-for-using-kubernetes-rbac/)
- [Implementing Kubernetes RBAC: Best Practices and Examples](https://trilio.io/kubernetes-best-practices/kubernetes-rbac/)
- [Orphaned Resources in Kubernetes - StackState](https://www.stackstate.com/blog/orphaned-resources-in-kubernetes-detection-impact-and-prevention-tips/)
- [Garbage Collection - Kubernetes](https://kubernetes.io/docs/concepts/architecture/garbage-collection/)
- [Owner References - Kubernetes Training](https://www.nakamasato.com/kubernetes-training/kubernetes-features/owner-references/)
- [Ordered cleanup with OwnerReference - Kube by Example](https://kubebyexample.com/learning-paths/operator-framework/operator-sdk-go/ordered-cleanup-ownerreference)

### Windows FileSystemWatcher
- [FileSystemWatcher not working when large amount of files are changed - Microsoft Learn](https://learn.microsoft.com/en-us/archive/msdn-technet-forums/60b12c2e-9dc8-4de4-be2d-bf8345bdaaee)
- [Tamed FileSystemWatcher - Peter Meinl](https://petermeinl.wordpress.com/2015/05/18/tamed-filesystemwatcher/)
- [FileSystemWatcher Class - .NET Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-io-filesystemwatcher)
- [FileSystemWatcher Follies - Microsoft Learn](https://learn.microsoft.com/en-us/archive/blogs/winsdk/filesystemwatcher-follies)

### Kafka in Kubernetes
- [Solving Java Out of Memory Issues in Kafka-Powered Microservices](https://medium.com/@msbreuer/solving-java-out-of-memory-issues-in-kafka-powered-microservices-c6911882c174)
- [Kafka pod is constantly created and destroyed - GitHub Issue](https://github.com/banzaicloud/koperator/issues/112)
- [Configure CPU and Memory for Confluent Platform in Kubernetes](https://docs.confluent.io/operator/current/co-resources.html)
- [Running Kafka in Kubernetes with KRaft mode](https://rafael-natali.medium.com/running-kafka-in-kubernetes-with-kraft-mode-549d22ab31b0)
- [Deploying Apache Kafka on Kubernetes with KRaft Mode: A Complete Guide](https://medium.com/@soumeng.kol/deploying-apache-kafka-on-kubernetes-with-kraft-mode-a-complete-guide-2edef6d9fe91)

### Additional References
- [docker-windows-volume-watcher - GitHub](https://github.com/merofeev/docker-windows-volume-watcher)
- [Kubernetes Sidecar Containers: Use Cases and Best Practices](https://www.groundcover.com/blog/kubernetes-sidecar)
- [Auto-Pruning Kubernetes Resources with Harness](https://www.harness.io/blog/auto-pruning-orphaned-resources)

---

*Pitfalls research for: File Simulator Suite v2.0 Simulator Control Platform*
*Researched: 2026-02-02*
*Focus: Integration risks when adding monitoring/control to stable v1.0 infrastructure*
