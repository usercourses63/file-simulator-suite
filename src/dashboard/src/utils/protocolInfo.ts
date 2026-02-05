import { Protocol } from '../types/server';

/**
 * Protocol-specific connection information.
 */
export interface ProtocolInfo {
  /** Display name for the protocol */
  displayName: string;
  /** Default port number */
  defaultPort: number;
  /** Service hostname template (cluster internal) */
  serviceHost: string;
  /** NodePort for external access */
  nodePort: number;
  /** Connection string example */
  connectionString: string;
  /** Protocol-specific configuration items */
  config: Record<string, string>;
  /** Default credentials (dev environment) */
  credentials?: {
    username: string;
    password: string;
  };
}

/**
 * Get protocol-specific connection information for a server.
 *
 * @param serverName - Name of the server (used for host resolution)
 * @param protocol - Protocol type
 * @param namespace - Kubernetes namespace (default: file-simulator)
 * @returns Protocol-specific connection info
 */
export function getProtocolInfo(
  serverName: string,
  protocol: Protocol,
  namespace: string = 'file-simulator'
): ProtocolInfo {
  // Build service hostname
  const serviceBase = `${serverName}.${namespace}.svc.cluster.local`;

  switch (protocol) {
    case 'FTP':
      return {
        displayName: 'FTP Server',
        defaultPort: 21,
        serviceHost: serviceBase,
        nodePort: 30021,
        connectionString: `ftp://${serviceBase}:21`,
        config: {
          'Mode': 'Passive (PASV)',
          'Passive Ports': '30000-30009',
          'Transfer Mode': 'Binary',
          'Anonymous': 'Disabled'
        },
        credentials: {
          username: 'ftpuser',
          password: 'ftppass'
        }
      };

    case 'SFTP':
      return {
        displayName: 'SFTP Server',
        defaultPort: 22,
        serviceHost: serviceBase,
        nodePort: 30022,
        connectionString: `sftp://${serviceBase}:22`,
        config: {
          'SSH Version': 'OpenSSH',
          'Key Auth': 'Supported',
          'Root Directory': '/data'
        },
        credentials: {
          username: 'sftpuser',
          password: 'sftppass'
        }
      };

    case 'HTTP':
      return {
        displayName: 'HTTP/WebDAV Server',
        defaultPort: 80,
        serviceHost: serviceBase,
        nodePort: 30088,
        connectionString: `http://${serviceBase}:80`,
        config: {
          'WebDAV': 'Enabled',
          'Auth': 'Basic',
          'Methods': 'GET, PUT, DELETE, MKCOL, PROPFIND'
        },
        credentials: {
          username: 'webdav',
          password: 'webdav'
        }
      };

    case 'S3':
      return {
        displayName: 'S3/MinIO Server',
        defaultPort: 9000,
        serviceHost: serviceBase,
        nodePort: 30900,
        connectionString: `http://${serviceBase}:9000`,
        config: {
          'Bucket': 'simulator-data',
          'Region': 'us-east-1',
          'Console URL': 'http://file-simulator.local:30901',
          'Path Style': 'Enabled'
        },
        credentials: {
          username: 'minioadmin',
          password: 'minioadmin123'
        }
      };

    case 'SMB':
      return {
        displayName: 'SMB/Samba Server',
        defaultPort: 445,
        serviceHost: serviceBase,
        nodePort: 30445,
        connectionString: `\\\\${serviceBase}\\simulator-data`,
        config: {
          'Share Name': 'simulator-data',
          'Protocol': 'SMB2/SMB3',
          'Guest Access': 'Disabled'
        },
        credentials: {
          username: 'smbuser',
          password: 'smbpass'
        }
      };

    case 'NFS':
      return {
        displayName: 'NFS Server',
        defaultPort: 2049,
        serviceHost: serviceBase,
        nodePort: 32049,
        connectionString: `${serviceBase}:/exports/data`,
        config: {
          'Export Path': '/exports/data',
          'NFS Version': 'v3 (unfs3)',
          'Sync Mode': 'async',
          'Access': 'rw,no_root_squash'
        }
      };

    case 'Management':
      return {
        displayName: 'Management UI (FileBrowser)',
        defaultPort: 8080,
        serviceHost: serviceBase,
        nodePort: 30180,
        connectionString: `http://${serviceBase}:8080`,
        config: {
          'Application': 'FileBrowser',
          'Features': 'Browse, Upload, Download, Delete',
          'Auth': 'Username/Password'
        },
        credentials: {
          username: 'admin',
          password: 'admin123'
        }
      };

    default:
      return {
        displayName: protocol,
        defaultPort: 0,
        serviceHost: serviceBase,
        nodePort: 0,
        connectionString: serviceBase,
        config: {}
      };
  }
}

/**
 * Get Minikube IP-based connection info for external access.
 *
 * @param protocol - Protocol type
 * @param minikubeIp - Minikube IP address (default: 192.168.49.2)
 * @returns External connection string
 */
export function getExternalConnectionString(
  protocol: Protocol,
  minikubeIp: string = '192.168.49.2'
): string {
  const portMap: Record<Protocol, number> = {
    'FTP': 30021,
    'SFTP': 30022,
    'HTTP': 30088,
    'S3': 30900,
    'SMB': 30445,
    'NFS': 32049,
    'Management': 30180
  };

  const port = portMap[protocol];

  switch (protocol) {
    case 'FTP':
      return `ftp://${minikubeIp}:${port}`;
    case 'SFTP':
      return `sftp://${minikubeIp}:${port}`;
    case 'HTTP':
      return `http://${minikubeIp}:${port}`;
    case 'S3':
      return `http://${minikubeIp}:${port}`;
    case 'SMB':
      return `\\\\${minikubeIp}\\simulator-data (via tunnel)`;
    case 'NFS':
      return `${minikubeIp}:/exports/data`;
    case 'Management':
      return `http://${minikubeIp}:${port}`;
    default:
      return `${minikubeIp}:${port}`;
  }
}
