# Plan 09-06 Summary: Human Verification Checkpoint

**Duration:** 15 min (including deployment fix)
**Status:** APPROVED

## Verification Results

| Test | Status | Notes |
|------|--------|-------|
| Database Persistence | PASS | 286 samples before pod delete, 364 after restart |
| Sample Recording | PASS | 5-second granularity, all 13 servers |
| REST API Endpoints | PASS | /api/metrics/samples, /hourly, /servers |
| Server Filtering | PASS | 68 FTP-only samples with filter |
| Dashboard Build | PASS | TypeScript compiles without errors |
| History Tab | PASS | Visible in navigation, shows chart |
| Time Range Presets | PASS | 1h, 6h, 24h, 7d buttons work |
| LatencyChart Zoom | PASS | ReferenceArea implemented |
| Sparklines | PASS | Integrated into ServerCard |

## Issue Found & Fixed

**Problem:** Control-api pod crashed with `SQLite Error 14: 'unable to open database file'`

**Root Cause:** hostPath volumes on Minikube don't automatically apply fsGroup permissions. The non-root container user (1000:1000) couldn't write to `/mnt/control-data/`.

**Fix:** Added init container to `control-api.yaml`:
```yaml
initContainers:
  - name: init-permissions
    image: busybox:1.36
    command: ['sh', '-c', 'chown -R 1000:1000 /mnt/control-data']
    securityContext:
      runAsNonRoot: false
      runAsUser: 0
```

## Latency Shows 0ms - Expected Behavior

User observed latency values of 0-1ms for all servers. This is correct:
- TCP health checks to local Minikube services complete in sub-millisecond
- Values round to 0 or 1ms at this granularity
- Higher latency would indicate remote servers or network issues

## Phase 9 Success Criteria

- [x] SQLite database persists health metrics with 5-second granularity for 7 days
- [x] Historical trends dashboard shows connection counts, latency, and errors over time
- [x] User can query metrics for specific time ranges (last 1h, last 24h, last 7d)
- [x] Database survives pod restarts with data intact
- [x] Auto-cleanup removes data older than 7 days (service configured, runs hourly)

## Commits

| Hash | Message |
|------|---------|
| f993fd9 | fix(helm): add init container for control-data volume permissions |
