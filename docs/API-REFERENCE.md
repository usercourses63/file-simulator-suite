# API Reference

Complete reference for the File Simulator Control API.

## Table of Contents

- [Overview](#overview)
- [Base URL](#base-url)
- [Authentication](#authentication)
- [Health](#health)
- [Connection Info](#connection-info)
- [Servers](#servers)
- [Files](#files)
- [Kafka](#kafka)
- [Alerts](#alerts)
- [Metrics](#metrics)
- [Configuration](#configuration)
- [WebSocket/SignalR](#websocketsignalr)
- [Error Responses](#error-responses)

---

## Overview

The Control API is a REST API built with ASP.NET Core Minimal API. It provides programmatic access to all simulator management features.

**Content Type:** `application/json` for all request/response bodies

**HTTP Methods:**
- `GET` - Retrieve resources
- `POST` - Create resources or trigger actions
- `DELETE` - Remove resources

---

## Base URL

| Environment | URL |
|-------------|-----|
| Local Development | `http://localhost:5000` |
| Kubernetes (NodePort) | `http://file-simulator.local:30500` |
| Kubernetes (Internal) | `http://file-sim-file-simulator-control-api.file-simulator.svc.cluster.local:5000` |

---

## Authentication

The Control API does not require authentication. It is designed for development and testing environments.

For production deployments, consider:
- Network policies to restrict access
- Ingress with authentication middleware
- Service mesh mTLS

---

## Health

### GET /api/health

Health check endpoint for readiness/liveness probes.

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2026-02-05T14:30:00Z"
}
```

**Status Codes:**
- `200 OK` - Service is healthy
- `503 Service Unavailable` - Service is unhealthy

---

## Connection Info

### GET /api/connection-info

Get connection details for all simulator services.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `format` | string | `json` | Output format: `json`, `env`, `yaml`, `dotnet` |

**Response (JSON format):**
```json
{
  "hostname": "file-simulator.local",
  "ftp": {
    "host": "file-simulator.local",
    "port": 30021,
    "username": "ftpuser",
    "password": "ftppass123"
  },
  "sftp": {
    "host": "file-simulator.local",
    "port": 30022,
    "username": "sftpuser",
    "password": "sftppass123"
  },
  "http": {
    "baseUrl": "http://file-simulator.local:30088"
  },
  "s3": {
    "endpoint": "http://file-simulator.local:30900",
    "accessKey": "minioadmin",
    "secretKey": "minioadmin123",
    "bucket": "simulator"
  },
  "kafka": {
    "bootstrapServers": "file-simulator.local:30093"
  },
  "nas": [
    {
      "name": "nas-input-1",
      "host": "file-simulator.local",
      "port": 32150,
      "path": "/data"
    }
    // ... additional NAS servers
  ]
}
```

**Response (env format):**
```bash
FILE_FTP_HOST=file-simulator.local
FILE_FTP_PORT=30021
FILE_FTP_USERNAME=ftpuser
FILE_FTP_PASSWORD=ftppass123
FILE_SFTP_HOST=file-simulator.local
FILE_SFTP_PORT=30022
# ...
```

**Response (dotnet format):**
```json
{
  "FileSimulator": {
    "Ftp": {
      "Host": "file-simulator.local",
      "Port": 30021,
      "Username": "ftpuser",
      "Password": "ftppass123"
    }
    // ...
  }
}
```

---

## Servers

### GET /api/servers

List all servers (static Helm-deployed and dynamic).

**Response:**
```json
[
  {
    "name": "file-sim-file-simulator-ftp",
    "protocol": "FTP",
    "isDynamic": false,
    "status": "Running",
    "isHealthy": true,
    "host": "file-simulator.local",
    "port": 30021,
    "replicas": 1,
    "readyReplicas": 1
  },
  {
    "name": "my-dynamic-ftp",
    "protocol": "FTP",
    "isDynamic": true,
    "status": "Running",
    "isHealthy": true,
    "host": "file-simulator.local",
    "port": 31001,
    "replicas": 1,
    "readyReplicas": 1
  }
]
```

### GET /api/servers/{name}

Get details for a specific server.

**Response:**
```json
{
  "name": "file-sim-file-simulator-ftp",
  "protocol": "FTP",
  "isDynamic": false,
  "status": "Running",
  "isHealthy": true,
  "host": "file-simulator.local",
  "port": 30021,
  "replicas": 1,
  "readyReplicas": 1,
  "labels": {
    "app.kubernetes.io/name": "ftp",
    "app.kubernetes.io/component": "ftp"
  }
}
```

**Status Codes:**
- `200 OK` - Server found
- `404 Not Found` - Server not found

### POST /api/servers/ftp

Create a dynamic FTP server.

**Request Body:**
```json
{
  "name": "my-ftp",
  "username": "testuser",
  "password": "testpass123"
}
```

**Validation:**
- `name`: Required, 3-50 chars, alphanumeric and hyphens, must start with letter
- `username`: Required, 3-32 chars
- `password`: Required, 8-64 chars

**Response:** `201 Created`
```json
{
  "name": "my-ftp",
  "protocol": "FTP",
  "isDynamic": true,
  "status": "Pending",
  "host": "file-simulator.local",
  "port": 31001
}
```

**Status Codes:**
- `201 Created` - Server created
- `400 Bad Request` - Validation failed
- `409 Conflict` - Name already in use

### POST /api/servers/sftp

Create a dynamic SFTP server.

**Request Body:**
```json
{
  "name": "my-sftp",
  "username": "testuser",
  "password": "testpass123"
}
```

**Response:** Same structure as FTP creation.

### POST /api/servers/nas

Create a dynamic NAS (NFS) server.

**Request Body:**
```json
{
  "name": "my-nas",
  "directory": "input"
}
```

**Directory Presets:**
- `input` - Maps to `nas-input-dynamic/` subdirectory
- `output` - Maps to `nas-output-dynamic/` subdirectory
- `backup` - Maps to `nas-backup-dynamic/` subdirectory
- Custom path - Any valid subdirectory path

**Response:** Same structure as FTP creation.

### DELETE /api/servers/{name}

Delete a dynamic server.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `deleteData` | boolean | `false` | Also delete associated NAS files |

**Response:** `204 No Content`

**Status Codes:**
- `204 No Content` - Server deleted
- `400 Bad Request` - Cannot delete static server
- `404 Not Found` - Server not found

### POST /api/servers/{name}/stop

Stop a server (scale deployment to 0 replicas).

**Response:**
```json
{
  "message": "Server 'my-ftp' stopped"
}
```

### POST /api/servers/{name}/start

Start a stopped server (scale deployment to 1 replica).

**Response:**
```json
{
  "message": "Server 'my-ftp' started"
}
```

### POST /api/servers/{name}/restart

Restart a server (delete pod to trigger recreation).

**Response:**
```json
{
  "message": "Server 'my-ftp' restarting"
}
```

### GET /api/servers/check-name/{name}

Check if a server name is available for creation.

**Response:**
```json
{
  "name": "my-ftp",
  "available": true
}
```

---

## Files

### GET /api/files/tree

Get directory tree listing.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | `""` | Relative path from base directory |
| `depth` | integer | `3` | Max recursion depth (1-5) |

**Response:**
```json
[
  {
    "id": "input",
    "name": "input",
    "isDirectory": true,
    "size": null,
    "modified": "2026-02-05T12:00:00Z",
    "protocols": ["FTP", "SFTP", "HTTP", "S3", "SMB", "NFS"],
    "children": [
      {
        "id": "input/test.txt",
        "name": "test.txt",
        "isDirectory": false,
        "size": 1024,
        "modified": "2026-02-05T12:30:00Z",
        "protocols": ["FTP", "SFTP", "HTTP", "S3", "SMB", "NFS"],
        "children": null
      }
    ]
  }
]
```

### POST /api/files/upload

Upload a file.

**Request:** `multipart/form-data`

| Field | Type | Description |
|-------|------|-------------|
| `file` | File | File to upload (max 100MB) |
| `path` | Query | Target directory (optional) |

**Example (curl):**
```bash
curl -X POST http://file-simulator.local:30500/api/files/upload \
  -F "file=@test.txt" \
  -F "path=input"
```

**Response:**
```json
{
  "id": "input/test.txt",
  "name": "test.txt",
  "isDirectory": false,
  "size": 1024,
  "modified": "2026-02-05T12:30:00Z",
  "protocols": ["FTP", "SFTP", "HTTP", "S3", "SMB", "NFS"]
}
```

### GET /api/files/download

Download a file.

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | Relative path to file |

**Response:** File stream with `Content-Disposition: attachment` header.

### DELETE /api/files

Delete a file or directory.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | Required | Relative path to delete |
| `recursive` | boolean | `false` | Required for directories |

**Response:** `204 No Content`

**Status Codes:**
- `204 No Content` - Deleted successfully
- `400 Bad Request` - Missing recursive flag for directory
- `404 Not Found` - Path not found

---

## Kafka

### GET /api/kafka/health

Check Kafka broker connectivity.

**Response:**
```json
{
  "status": "healthy"
}
```

### GET /api/kafka/topics

List all topics (excluding internal topics).

**Response:**
```json
[
  {
    "name": "my-topic",
    "partitions": 3,
    "replicationFactor": 1,
    "messageCount": 42
  }
]
```

### GET /api/kafka/topics/{name}

Get topic details.

**Response:**
```json
{
  "name": "my-topic",
  "partitions": 3,
  "replicationFactor": 1,
  "messageCount": 42,
  "partitionDetails": [
    {
      "partitionId": 0,
      "leader": 1,
      "replicas": [1],
      "isr": [1]
    }
  ]
}
```

### POST /api/kafka/topics

Create a new topic.

**Request Body:**
```json
{
  "name": "my-topic",
  "partitions": 3,
  "replicationFactor": 1
}
```

**Response:** `201 Created` with topic details.

### DELETE /api/kafka/topics/{name}

Delete a topic.

**Response:** `204 No Content`

### GET /api/kafka/topics/{name}/messages

Get recent messages from a topic.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `count` | integer | `50` | Number of messages (1-1000) |

**Response:**
```json
[
  {
    "key": "key1",
    "value": "hello world",
    "partition": 0,
    "offset": 42,
    "timestamp": "2026-02-05T12:30:00Z"
  }
]
```

### POST /api/kafka/topics/{name}/messages

Produce a message to a topic.

**Request Body:**
```json
{
  "key": "key1",
  "value": "hello world"
}
```

**Response:**
```json
{
  "topic": "my-topic",
  "partition": 0,
  "offset": 43,
  "timestamp": "2026-02-05T12:30:00Z"
}
```

### GET /api/kafka/consumer-groups

List all consumer groups.

**Response:**
```json
[
  {
    "groupId": "my-consumer-group",
    "state": "Stable",
    "memberCount": 2
  }
]
```

### GET /api/kafka/consumer-groups/{groupId}

Get consumer group details.

**Response:**
```json
{
  "groupId": "my-consumer-group",
  "state": "Stable",
  "members": [
    {
      "memberId": "consumer-1",
      "clientId": "client-1",
      "host": "/172.17.0.5"
    }
  ],
  "offsets": [
    {
      "topic": "my-topic",
      "partition": 0,
      "currentOffset": 42,
      "endOffset": 50,
      "lag": 8
    }
  ]
}
```

### POST /api/kafka/consumer-groups/{groupId}/reset

Reset consumer group offsets.

**Request Body:**
```json
{
  "topic": "my-topic",
  "resetTo": "earliest"
}
```

`resetTo` values: `earliest`, `latest`

**Response:**
```json
{
  "message": "Offsets reset for group 'my-consumer-group'"
}
```

### DELETE /api/kafka/consumer-groups/{groupId}

Delete a consumer group (must be inactive).

**Response:** `204 No Content`

---

## Alerts

### GET /api/alerts/active

Get all active (unresolved) alerts.

**Response:**
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "type": "ServerHealth",
    "severity": "Warning",
    "source": "file-sim-file-simulator-ftp",
    "message": "Server unhealthy: 3 consecutive failures",
    "triggeredAt": "2026-02-05T12:30:00Z",
    "resolvedAt": null,
    "isResolved": false
  }
]
```

### GET /api/alerts/history

Get alert history.

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `severity` | string | Filter by severity: Info, Warning, Critical |

**Response:** Array of alerts (last 100).

### GET /api/alerts/{id}

Get specific alert by ID.

**Response:** Single alert object.

### GET /api/alerts/stats

Get alert statistics for last 24 hours.

**Response:**
```json
{
  "totalAlerts": 15,
  "activeAlerts": 2,
  "resolvedAlerts": 13,
  "bySeverity": [
    { "severity": "Info", "count": 5, "active": 0 },
    { "severity": "Warning", "count": 8, "active": 1 },
    { "severity": "Critical", "count": 2, "active": 1 }
  ],
  "byType": [
    { "type": "ServerHealth", "count": 10, "active": 2 },
    { "type": "DiskSpace", "count": 5, "active": 0 }
  ],
  "timeRange": {
    "start": "2026-02-04T12:30:00Z",
    "end": "2026-02-05T12:30:00Z"
  }
}
```

---

## Metrics

### GET /api/metrics/samples

Get raw health samples for a time range (best for <24h).

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `startTime` | datetime | Yes | Range start (ISO 8601) |
| `endTime` | datetime | Yes | Range end (ISO 8601) |
| `serverId` | string | No | Filter by server name |
| `serverType` | string | No | Filter by type (FTP, SFTP, NAS, etc.) |

**Response:**
```json
{
  "samples": [
    {
      "id": 1,
      "timestamp": "2026-02-05T12:30:00Z",
      "serverId": "file-sim-file-simulator-ftp",
      "serverType": "FTP",
      "isHealthy": true,
      "latencyMs": 5.2
    }
  ],
  "totalCount": 100,
  "queryStart": "2026-02-05T00:00:00Z",
  "queryEnd": "2026-02-05T12:00:00Z"
}
```

### GET /api/metrics/hourly

Get hourly aggregations (best for >24h ranges).

**Query Parameters:** Same as `/api/metrics/samples`.

**Response:**
```json
{
  "hourly": [
    {
      "id": 1,
      "hourStart": "2026-02-05T12:00:00Z",
      "serverId": "file-sim-file-simulator-ftp",
      "serverType": "FTP",
      "sampleCount": 720,
      "healthyCount": 718,
      "avgLatencyMs": 5.5,
      "minLatencyMs": 2.1,
      "maxLatencyMs": 15.3,
      "p95LatencyMs": 8.2
    }
  ],
  "totalCount": 24,
  "queryStart": "2026-02-04T12:00:00Z",
  "queryEnd": "2026-02-05T12:00:00Z"
}
```

### GET /api/metrics/servers

Get list of servers with available metrics date range.

**Response:**
```json
[
  {
    "serverId": "file-sim-file-simulator-ftp",
    "serverType": "FTP",
    "firstSample": "2026-02-01T00:00:00Z",
    "lastSample": "2026-02-05T12:30:00Z",
    "totalSamples": 50000
  }
]
```

---

## Configuration

### GET /api/configuration/export

Export current dynamic server configuration as JSON file download.

**Response:** JSON file with `Content-Disposition: attachment` header.

### GET /api/configuration/preview

Get configuration as JSON response (for preview).

**Response:**
```json
{
  "version": "2.0",
  "exportedAt": "2026-02-05T12:30:00Z",
  "namespace": "file-simulator",
  "releasePrefix": "file-sim-file-simulator",
  "servers": [
    {
      "name": "my-ftp",
      "protocol": "FTP",
      "isDynamic": true,
      "ftp": {
        "username": "testuser",
        "password": "testpass123"
      }
    }
  ],
  "metadata": {
    "description": "Configuration export",
    "serverCount": 1
  }
}
```

### POST /api/configuration/validate

Validate import configuration without applying.

**Request Body:** Configuration export JSON.

**Response:**
```json
{
  "isValid": true,
  "willCreate": ["my-ftp", "my-sftp"],
  "conflicts": [
    {
      "serverName": "existing-ftp",
      "reason": "Server already exists"
    }
  ]
}
```

### POST /api/configuration/import

Import configuration.

**Request Body:**
```json
{
  "configuration": { /* export JSON */ },
  "strategy": "Skip"
}
```

**Strategy values:**
- `Skip` - Skip conflicting servers
- `Replace` - Delete and recreate conflicting servers
- `Rename` - Create with new name (append suffix)

**Response:**
```json
{
  "created": ["my-ftp", "my-sftp"],
  "skipped": ["existing-ftp"],
  "failed": []
}
```

### POST /api/configuration/import/file

Import configuration from uploaded JSON file.

**Request:** `multipart/form-data` with `file` field.

### GET /api/configuration/templates

Get configuration templates for common scenarios.

**Response:**
```json
{
  "basic": { /* single FTP server */ },
  "multi-nas": { /* 3 NAS servers */ },
  "full-stack": { /* FTP + SFTP + NAS */ }
}
```

---

## WebSocket/SignalR

The Control API uses SignalR for real-time updates. Connect to the following hubs:

### Server Status Hub

**URL:** `/hubs/server-status`

**Events:**

| Event | Payload | Description |
|-------|---------|-------------|
| `ServerStatusUpdate` | Array of server status | Broadcast every 5 seconds |

**Client Connection (JavaScript):**
```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://file-simulator.local:30500/hubs/server-status")
  .withAutomaticReconnect()
  .build();

connection.on("ServerStatusUpdate", (servers) => {
  console.log("Server status:", servers);
});

await connection.start();
```

### File Events Hub

**URL:** `/hubs/file-events`

**Events:**

| Event | Payload | Description |
|-------|---------|-------------|
| `FileEvent` | FileEventDto | File created/modified/deleted |

### Kafka Hub

**URL:** `/hubs/kafka`

**Events:**

| Event | Payload | Description |
|-------|---------|-------------|
| `KafkaMessage` | KafkaMessage | Message received on subscribed topic |

**Methods:**

| Method | Parameters | Description |
|--------|------------|-------------|
| `Subscribe` | topic (string) | Subscribe to topic messages |
| `Unsubscribe` | topic (string) | Unsubscribe from topic |

### Metrics Hub

**URL:** `/hubs/metrics`

**Events:**

| Event | Payload | Description |
|-------|---------|-------------|
| `MetricsSample` | Array of samples | Latest health samples |

### Alerts Hub

**URL:** `/hubs/alerts`

**Events:**

| Event | Payload | Description |
|-------|---------|-------------|
| `AlertTriggered` | Alert | New alert triggered |
| `AlertResolved` | Alert | Alert resolved |

---

## Error Responses

All endpoints return consistent error responses:

**400 Bad Request:**
```json
{
  "error": "Validation failed",
  "errors": {
    "name": ["Name is required", "Name must be at least 3 characters"]
  }
}
```

**404 Not Found:**
```json
{
  "error": "Server 'my-ftp' not found"
}
```

**409 Conflict:**
```json
{
  "error": "Server name 'my-ftp' is already in use"
}
```

**500 Internal Server Error:**
```json
{
  "error": "Failed to create server",
  "details": "Connection refused"
}
```

**503 Service Unavailable:**
```json
{
  "error": "Kafka unavailable",
  "details": "Connection timeout"
}
```
