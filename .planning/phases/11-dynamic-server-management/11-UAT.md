---
status: testing
phase: 11-dynamic-server-management
source: [11-02-SUMMARY.md, 11-03-SUMMARY.md, 11-06-SUMMARY.md, 11-07-SUMMARY.md, 11-09-PLAN.md]
started: 2026-02-03T14:00:00Z
updated: 2026-02-03T14:00:00Z
---

## Current Test

number: 1
name: Create FTP Server via UI
expected: |
  1. Click "+ Add Server" in header
  2. Select FTP protocol
  3. Enter name: test-ftp-1
  4. Enter username: testuser
  5. Enter password: testpass123
  6. Click "Create Server"
  7. Wait for progress to show "Complete"
  8. Server appears in grid with "Dynamic" badge
awaiting: user response

## Tests

### 1. Create FTP Server via UI
expected: Click "+ Add Server", select FTP, enter name/credentials, create. Server deploys within 30 seconds and appears in grid with "Dynamic" badge.
result: [pending]

### 2. Create SFTP Server via UI
expected: Click "+ Add Server", select SFTP, enter name/credentials (sftpuser/sftppass123), create. Server appears with "Dynamic" badge.
result: [pending]

### 3. Create NAS Server via UI
expected: Click "+ Add Server", select NAS, enter name (test-nas-1), select "Input Directory" preset, create. Server appears with "Dynamic" badge.
result: [pending]

### 4. Verify Kubernetes Resources Created
expected: Run `kubectl --context=file-simulator get deployments -n file-simulator | grep test-`. Deployments exist with ownerReferences pointing to control-api pod.
result: [pending]

### 5. Delete Single Server
expected: Hover over test-ftp-1 card, click trash icon, confirm deletion. Server disappears from grid and K8s resources are deleted within 60 seconds.
result: [pending]

### 6. Multi-Select Batch Delete
expected: Click checkbox on test-sftp-1, click checkbox on test-nas-1, batch bar shows "2 servers selected", click "Delete (2)", confirm. Both servers disappear.
result: [pending]

### 7. Server Lifecycle Operations
expected: Create test-lifecycle-1 server, click card to open details panel, use Stop/Start/Restart buttons. Server status changes appropriately (Stop scales to 0, Start scales to 1, Restart restarts pods).
result: [pending]

### 8. Configuration Export
expected: Click Settings (gear icon), click "Export Configuration". JSON file downloads containing both static (Helm) and dynamic servers.
result: [pending]

### 9. Configuration Import
expected: Create import-test-1, export config, delete import-test-1, import config, resolve any conflicts. Server is recreated.
result: [pending]

### 10. Static Server Protection
expected: Find a Helm-managed server (shows "Helm" badge). No delete button on hover, no checkbox for multi-select. Cannot be deleted.
result: [pending]

## Summary

total: 10
passed: 0
issues: 0
pending: 10
skipped: 0

## Gaps

[none yet]
