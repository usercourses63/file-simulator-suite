import { useState, useCallback } from 'react';
import {
  CreateFtpServerRequest,
  CreateSftpServerRequest,
  CreateNasServerRequest,
  UpdateFtpServerRequest,
  UpdateSftpServerRequest,
  UpdateNasServerRequest,
  DynamicServer,
  LifecycleAction,
  NameCheckResult,
  DeploymentProgress,
  ApiError
} from '../types/serverManagement';

interface UseServerManagementOptions {
  apiBaseUrl: string;
}

interface UseServerManagementReturn {
  // State
  isLoading: boolean;
  error: string | null;
  progress: DeploymentProgress | null;

  // CRUD operations
  createFtpServer: (request: CreateFtpServerRequest) => Promise<DynamicServer>;
  createSftpServer: (request: CreateSftpServerRequest) => Promise<DynamicServer>;
  createNasServer: (request: CreateNasServerRequest) => Promise<DynamicServer>;
  deleteServer: (name: string, deleteData?: boolean) => Promise<void>;

  // Update operations (for inline editing)
  updateFtpServer: (name: string, updates: UpdateFtpServerRequest) => Promise<DynamicServer>;
  updateSftpServer: (name: string, updates: UpdateSftpServerRequest) => Promise<DynamicServer>;
  updateNasServer: (name: string, updates: UpdateNasServerRequest) => Promise<DynamicServer>;

  // Lifecycle operations
  startServer: (name: string) => Promise<void>;
  stopServer: (name: string) => Promise<void>;
  restartServer: (name: string) => Promise<void>;

  // Utilities
  checkNameAvailability: (name: string) => Promise<NameCheckResult>;
  clearError: () => void;
  clearProgress: () => void;
}

export function useServerManagement({ apiBaseUrl }: UseServerManagementOptions): UseServerManagementReturn {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [progress, setProgress] = useState<DeploymentProgress | null>(null);

  const handleApiError = async (response: Response): Promise<never> => {
    const data = await response.json().catch(() => ({})) as ApiError;
    const errorMessage = data.error || data.details || `HTTP ${response.status}`;
    throw new Error(errorMessage);
  };

  const createFtpServer = useCallback(async (request: CreateFtpServerRequest): Promise<DynamicServer> => {
    setIsLoading(true);
    setError(null);
    setProgress({ phase: 'validating', message: 'Validating configuration...', serverName: request.name });

    try {
      setProgress({ phase: 'creating-deployment', message: 'Creating FTP deployment...', serverName: request.name });

      const response = await fetch(`${apiBaseUrl}/api/servers/ftp`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
      });

      if (!response.ok) {
        await handleApiError(response);
      }

      const server = await response.json() as DynamicServer;

      setProgress({ phase: 'complete', message: 'FTP server created successfully', serverName: request.name });
      return server;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create FTP server';
      setError(message);
      setProgress({ phase: 'error', message, serverName: request.name });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  const createSftpServer = useCallback(async (request: CreateSftpServerRequest): Promise<DynamicServer> => {
    setIsLoading(true);
    setError(null);
    setProgress({ phase: 'validating', message: 'Validating configuration...', serverName: request.name });

    try {
      setProgress({ phase: 'creating-deployment', message: 'Creating SFTP deployment...', serverName: request.name });

      const response = await fetch(`${apiBaseUrl}/api/servers/sftp`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
      });

      if (!response.ok) {
        await handleApiError(response);
      }

      const server = await response.json() as DynamicServer;
      setProgress({ phase: 'complete', message: 'SFTP server created successfully', serverName: request.name });
      return server;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create SFTP server';
      setError(message);
      setProgress({ phase: 'error', message, serverName: request.name });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  const createNasServer = useCallback(async (request: CreateNasServerRequest): Promise<DynamicServer> => {
    setIsLoading(true);
    setError(null);
    setProgress({ phase: 'validating', message: 'Validating configuration...', serverName: request.name });

    try {
      setProgress({ phase: 'creating-deployment', message: 'Creating NAS deployment...', serverName: request.name });

      const response = await fetch(`${apiBaseUrl}/api/servers/nas`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
      });

      if (!response.ok) {
        await handleApiError(response);
      }

      const server = await response.json() as DynamicServer;
      setProgress({ phase: 'complete', message: 'NAS server created successfully', serverName: request.name });
      return server;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create NAS server';
      setError(message);
      setProgress({ phase: 'error', message, serverName: request.name });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  // Update operations for inline editing
  const updateFtpServer = useCallback(async (name: string, updates: UpdateFtpServerRequest): Promise<DynamicServer> => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(`${apiBaseUrl}/api/servers/ftp/${encodeURIComponent(name)}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(updates)
      });

      if (!response.ok) {
        await handleApiError(response);
      }

      return response.json();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update server';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  const updateSftpServer = useCallback(async (name: string, updates: UpdateSftpServerRequest): Promise<DynamicServer> => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(`${apiBaseUrl}/api/servers/sftp/${encodeURIComponent(name)}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(updates)
      });

      if (!response.ok) {
        await handleApiError(response);
      }

      return response.json();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update server';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  const updateNasServer = useCallback(async (name: string, updates: UpdateNasServerRequest): Promise<DynamicServer> => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(`${apiBaseUrl}/api/servers/nas/${encodeURIComponent(name)}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(updates)
      });

      if (!response.ok) {
        await handleApiError(response);
      }

      return response.json();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update server';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  const deleteServer = useCallback(async (name: string, deleteData = false): Promise<void> => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(
        `${apiBaseUrl}/api/servers/${encodeURIComponent(name)}?deleteData=${deleteData}`,
        { method: 'DELETE' }
      );

      if (!response.ok) {
        await handleApiError(response);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to delete server';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  const performLifecycleAction = useCallback(async (name: string, action: LifecycleAction): Promise<void> => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(
        `${apiBaseUrl}/api/servers/${encodeURIComponent(name)}/${action}`,
        { method: 'POST' }
      );

      if (!response.ok) {
        await handleApiError(response);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : `Failed to ${action} server`;
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  const startServer = useCallback((name: string) => performLifecycleAction(name, 'start'), [performLifecycleAction]);
  const stopServer = useCallback((name: string) => performLifecycleAction(name, 'stop'), [performLifecycleAction]);
  const restartServer = useCallback((name: string) => performLifecycleAction(name, 'restart'), [performLifecycleAction]);

  const checkNameAvailability = useCallback(async (name: string): Promise<NameCheckResult> => {
    const response = await fetch(`${apiBaseUrl}/api/servers/check-name/${encodeURIComponent(name)}`);
    if (!response.ok) {
      throw new Error('Failed to check name availability');
    }
    return response.json();
  }, [apiBaseUrl]);

  const clearError = useCallback(() => setError(null), []);
  const clearProgress = useCallback(() => setProgress(null), []);

  return {
    isLoading,
    error,
    progress,
    createFtpServer,
    createSftpServer,
    createNasServer,
    deleteServer,
    updateFtpServer,
    updateSftpServer,
    updateNasServer,
    startServer,
    stopServer,
    restartServer,
    checkNameAvailability,
    clearError,
    clearProgress
  };
}

export default useServerManagement;
