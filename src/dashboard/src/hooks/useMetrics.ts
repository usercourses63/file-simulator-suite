import { useState, useEffect, useCallback } from 'react';
import { MetricsSamplesResponse, MetricsHourlyResponse } from '../types/metrics';

interface UseMetricsOptions {
  apiBaseUrl: string;
  serverId?: string;
  serverType?: string;
  startTime: Date;
  endTime: Date;
  resolution: 'raw' | 'hourly';
  autoRefresh?: boolean;
  refreshInterval?: number;  // ms, default 30000
}

interface UseMetricsResult {
  data: MetricsSamplesResponse | MetricsHourlyResponse | null;
  isLoading: boolean;
  error: string | null;
  refresh: () => void;
}

/**
 * Hook for fetching historical metrics from the API.
 * Supports auto-refresh and automatic resolution selection.
 */
export function useMetrics(options: UseMetricsOptions): UseMetricsResult {
  const [data, setData] = useState<MetricsSamplesResponse | MetricsHourlyResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchMetrics = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    const params = new URLSearchParams({
      startTime: options.startTime.toISOString(),
      endTime: options.endTime.toISOString()
    });

    if (options.serverId) params.set('serverId', options.serverId);
    if (options.serverType) params.set('serverType', options.serverType);

    const endpoint = options.resolution === 'raw'
      ? `${options.apiBaseUrl}/api/metrics/samples`
      : `${options.apiBaseUrl}/api/metrics/hourly`;

    try {
      const response = await fetch(`${endpoint}?${params}`);
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${await response.text()}`);
      }
      const json = await response.json();
      setData(json);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setIsLoading(false);
    }
  }, [options.apiBaseUrl, options.serverId, options.serverType, options.startTime, options.endTime, options.resolution]);

  useEffect(() => {
    fetchMetrics();
  }, [fetchMetrics]);

  useEffect(() => {
    if (!options.autoRefresh) return;

    const interval = setInterval(fetchMetrics, options.refreshInterval || 30000);
    return () => clearInterval(interval);
  }, [fetchMetrics, options.autoRefresh, options.refreshInterval]);

  return { data, isLoading, error, refresh: fetchMetrics };
}

export default useMetrics;
