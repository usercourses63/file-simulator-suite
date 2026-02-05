import { useState, useEffect, useCallback } from 'react';
import { Alert, AlertStats } from '../types/alert';
import { useAlertStream } from './useAlertStream';
import { showAlert } from '../components/AlertToaster';

/**
 * Result returned by useAlerts hook.
 */
export interface UseAlertsResult {
  /** Currently active (unresolved) alerts */
  activeAlerts: Alert[];
  /** Historical alerts (both resolved and unresolved) */
  alertHistory: Alert[];
  /** Alert statistics */
  stats: AlertStats;
  /** Whether alert data is loading */
  isLoading: boolean;
  /** Error message if fetch failed */
  error: string | null;
  /** Fetch active alerts from API */
  fetchActiveAlerts: () => Promise<void>;
  /** Fetch alert history from API */
  fetchAlertHistory: () => Promise<void>;
  /** Whether SignalR alert stream is connected */
  isConnected: boolean;
  /** Whether SignalR is reconnecting */
  isReconnecting: boolean;
}

/**
 * Custom hook for managing alert state with real-time updates.
 *
 * @param apiBaseUrl - Base URL of the control API (e.g., "http://localhost:30500")
 * @returns Alert state and operations
 *
 * @example
 * const { activeAlerts, alertHistory, stats, isLoading, error, fetchActiveAlerts } =
 *   useAlerts('http://localhost:30500');
 */
export function useAlerts(apiBaseUrl: string): UseAlertsResult {
  const [activeAlerts, setActiveAlerts] = useState<Alert[]>([]);
  const [alertHistory, setAlertHistory] = useState<Alert[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Calculate statistics from active alerts
  const stats: AlertStats = {
    totalCount: activeAlerts.length,
    infoCount: activeAlerts.filter(a => a.severity === 'Info').length,
    warningCount: activeAlerts.filter(a => a.severity === 'Warning').length,
    criticalCount: activeAlerts.filter(a => a.severity === 'Critical').length,
    byType: activeAlerts.reduce((acc, alert) => {
      acc[alert.type] = (acc[alert.type] || 0) + 1;
      return acc;
    }, {} as Record<string, number>)
  };

  // Fetch active alerts from API
  const fetchActiveAlerts = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(`${apiBaseUrl}/api/alerts/active`);
      if (!response.ok) {
        throw new Error(`Failed to fetch active alerts: ${response.statusText}`);
      }

      const data = await response.json();
      setActiveAlerts(data);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error';
      setError(errorMessage);
      console.error('Error fetching active alerts:', err);
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  // Fetch alert history from API
  const fetchAlertHistory = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(`${apiBaseUrl}/api/alerts/history`);
      if (!response.ok) {
        throw new Error(`Failed to fetch alert history: ${response.statusText}`);
      }

      const data = await response.json();
      setAlertHistory(data);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error';
      setError(errorMessage);
      console.error('Error fetching alert history:', err);
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  // Handle AlertTriggered events
  const handleAlertTriggered = useCallback((alert: Alert) => {
    console.log('Alert triggered:', alert);

    // Add to active alerts
    setActiveAlerts(prev => {
      // Check if alert already exists (shouldn't happen due to backend deduplication)
      if (prev.some(a => a.id === alert.id)) {
        return prev;
      }
      return [...prev, alert];
    });

    // Add to alert history
    setAlertHistory(prev => {
      if (prev.some(a => a.id === alert.id)) {
        return prev;
      }
      return [alert, ...prev];
    });

    // Show toast notification
    showAlert(alert);
  }, []);

  // Handle AlertResolved events
  const handleAlertResolved = useCallback((alert: Alert) => {
    console.log('Alert resolved:', alert);

    // Remove from active alerts
    setActiveAlerts(prev => prev.filter(a => a.id !== alert.id));

    // Update in alert history
    setAlertHistory(prev => prev.map(a =>
      a.id === alert.id ? alert : a
    ));
  }, []);

  // Connect to SignalR alert stream
  const { isConnected, isReconnecting } = useAlertStream(
    `${apiBaseUrl}/hubs/alerts`,
    {
      onAlertTriggered: handleAlertTriggered,
      onAlertResolved: handleAlertResolved
    }
  );

  // Initial fetch of active alerts
  useEffect(() => {
    fetchActiveAlerts();
  }, [fetchActiveAlerts]);

  return {
    activeAlerts,
    alertHistory,
    stats,
    isLoading,
    error,
    fetchActiveAlerts,
    fetchAlertHistory,
    isConnected,
    isReconnecting
  };
}

export default useAlerts;
