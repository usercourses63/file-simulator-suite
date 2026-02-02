---
status: complete
phase: 06-backend-api-foundation
source: [06-01-SUMMARY.md, 06-02-SUMMARY.md, 06-03-SUMMARY.md]
started: 2026-02-02T13:00:00Z
updated: 2026-02-02T14:12:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Control API Pod Deployment
expected: Control API pod deploys via Helm and shows Running/Ready. Existing v1.0 servers unaffected.
result: pass

### 2. RBAC Resources Created
expected: kubectl --context=file-simulator get serviceaccount,role,rolebinding -n file-simulator | grep control-api shows all three RBAC resources created.
result: pass

### 3. NodePort External Access
expected: curl http://$(minikube -p file-simulator ip):30500/health returns "Healthy" from Windows host.
result: pass

### 4. API Info Endpoint
expected: curl http://$(minikube -p file-simulator ip):30500/ returns JSON with API version and capabilities.
result: pass

### 5. Server Discovery Endpoint
expected: curl http://$(minikube -p file-simulator ip):30500/api/servers returns JSON array listing all 13 protocol servers (7 NAS + 6 protocols).
result: pass

### 6. Server Status Endpoint
expected: curl http://$(minikube -p file-simulator ip):30500/api/status returns JSON with health status for each server (healthy/unhealthy with latency).
result: pass

### 7. SignalR Hub Connection
expected: SignalR client can connect to ws://$(minikube -p file-simulator ip):30500/hubs/status without errors. Connection logged in pod logs.
result: pass

### 8. Real-Time Status Broadcasts
expected: Connected SignalR client receives ServerStatusUpdate messages every 5 seconds with current server health.
result: pass

## Summary

total: 8
passed: 8
issues: 0
pending: 0
skipped: 0

## Gaps

[none yet]
