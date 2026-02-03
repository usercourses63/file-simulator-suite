import { useState, useCallback } from 'react';
import {
  ServerConfigurationExport,
  ImportResult,
  ConflictResolution,
  ServerConfiguration
} from '../types/serverManagement';

interface UseConfigExportOptions {
  apiBaseUrl: string;
}

// Conflict detection result
export interface ConflictInfo {
  serverName: string;
  protocol: string;
  existingNodePort?: number;
  importNodePort?: number;
}

export interface ImportValidation {
  willCreate: ServerConfiguration[];
  conflicts: ConflictInfo[];
}

interface UseConfigExportReturn {
  // State
  isExporting: boolean;
  isImporting: boolean;
  isValidating: boolean;
  error: string | null;
  validation: ImportValidation | null;
  importResult: ImportResult | null;

  // Actions
  exportConfig: () => Promise<void>;
  previewConfig: () => Promise<ServerConfigurationExport | null>;
  validateImport: (config: ServerConfigurationExport) => Promise<ImportValidation>;
  importWithResolutions: (config: ServerConfigurationExport, resolutions: ConflictResolution[]) => Promise<ImportResult>;
  importFile: (file: File) => Promise<{ config: ServerConfigurationExport; validation: ImportValidation }>;
  clearError: () => void;
  clearResults: () => void;
}

export function useConfigExport({ apiBaseUrl }: UseConfigExportOptions): UseConfigExportReturn {
  const [isExporting, setIsExporting] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const [isValidating, setIsValidating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [validation, setValidation] = useState<ImportValidation | null>(null);
  const [importResult, setImportResult] = useState<ImportResult | null>(null);

  // Export configuration as file download
  const exportConfig = useCallback(async () => {
    setIsExporting(true);
    setError(null);

    try {
      const response = await fetch(`${apiBaseUrl}/api/configuration/export`);

      if (!response.ok) {
        throw new Error('Failed to export configuration');
      }

      // Get filename from Content-Disposition header or generate one
      const contentDisposition = response.headers.get('Content-Disposition');
      let filename = `file-simulator-config-${new Date().toISOString().slice(0, 10)}.json`;
      if (contentDisposition) {
        const match = contentDisposition.match(/filename="?([^"]+)"?/);
        if (match) filename = match[1];
      }

      // Download the file
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = filename;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Export failed';
      setError(message);
      throw err;
    } finally {
      setIsExporting(false);
    }
  }, [apiBaseUrl]);

  // Preview configuration (get as JSON, not file)
  const previewConfig = useCallback(async (): Promise<ServerConfigurationExport | null> => {
    try {
      const response = await fetch(`${apiBaseUrl}/api/configuration/preview`);
      if (!response.ok) {
        throw new Error('Failed to get configuration preview');
      }
      return response.json();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Preview failed';
      setError(message);
      return null;
    }
  }, [apiBaseUrl]);

  // Validate import and identify conflicts
  const validateImport = useCallback(async (config: ServerConfigurationExport): Promise<ImportValidation> => {
    setIsValidating(true);
    setError(null);
    setValidation(null);

    try {
      const response = await fetch(`${apiBaseUrl}/api/configuration/validate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(config)
      });

      if (!response.ok) {
        const data = await response.json();
        throw new Error(data.error || 'Validation failed');
      }

      const result = await response.json() as ImportValidation;
      setValidation(result);
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Validation failed';
      setError(message);
      throw err;
    } finally {
      setIsValidating(false);
    }
  }, [apiBaseUrl]);

  // Import configuration with per-conflict resolutions
  // This is the key method - user provides resolution for EACH conflict individually
  const importWithResolutions = useCallback(async (
    config: ServerConfigurationExport,
    resolutions: ConflictResolution[]
  ): Promise<ImportResult> => {
    setIsImporting(true);
    setError(null);
    setImportResult(null);

    try {
      const response = await fetch(`${apiBaseUrl}/api/configuration/import`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          configuration: config,
          resolutions  // Per-conflict resolutions, not a single global strategy
        })
      });

      if (!response.ok && response.status !== 207) {
        const data = await response.json();
        throw new Error(data.error || 'Import failed');
      }

      const result = await response.json() as ImportResult;
      setImportResult(result);
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Import failed';
      setError(message);
      throw err;
    } finally {
      setIsImporting(false);
    }
  }, [apiBaseUrl]);

  // Read and validate file
  const importFile = useCallback(async (
    file: File
  ): Promise<{ config: ServerConfigurationExport; validation: ImportValidation }> => {
    setError(null);

    try {
      // Read and parse file
      const text = await file.text();
      const config = JSON.parse(text) as ServerConfigurationExport;

      // Validate structure
      if (!config.version || !config.servers) {
        throw new Error('Invalid configuration file format');
      }

      // Validate to detect conflicts
      const validationResult = await validateImport(config);

      return { config, validation: validationResult };
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Import failed';
      setError(message);
      throw err;
    }
  }, [validateImport]);

  const clearError = useCallback(() => setError(null), []);
  const clearResults = useCallback(() => {
    setValidation(null);
    setImportResult(null);
  }, []);

  return {
    isExporting,
    isImporting,
    isValidating,
    error,
    validation,
    importResult,
    exportConfig,
    previewConfig,
    validateImport,
    importWithResolutions,
    importFile,
    clearError,
    clearResults
  };
}

export default useConfigExport;
