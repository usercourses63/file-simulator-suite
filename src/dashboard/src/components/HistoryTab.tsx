import { useState, useMemo } from 'react';
import TimeRangeSelector from './TimeRangeSelector';
import LatencyChart from './LatencyChart';
import { useMetrics } from '../hooks/useMetrics';
import { HealthSampleDto, ChartDataPoint } from '../types/metrics';

interface HistoryTabProps {
  apiBaseUrl: string;
  initialServerId?: string;
}

/**
 * History tab with time range selection and zoomable latency chart.
 * Automatically switches between raw samples and hourly aggregations
 * based on the selected time range.
 */
export function HistoryTab({ apiBaseUrl, initialServerId }: HistoryTabProps) {
  // Default to last 24 hours
  const [timeRange, setTimeRange] = useState(() => {
    const end = new Date();
    const start = new Date();
    start.setHours(start.getHours() - 24);
    return { startTime: start, endTime: end };
  });

  const [selectedServer, setSelectedServer] = useState<string | undefined>(initialServerId);
  const [autoRefresh, setAutoRefresh] = useState(false);

  // Determine resolution based on time range (>24h uses hourly)
  const rangeHours = (timeRange.endTime.getTime() - timeRange.startTime.getTime()) / (1000 * 60 * 60);
  const resolution = rangeHours > 24 ? 'hourly' : 'raw';

  const { data, isLoading, error, refresh } = useMetrics({
    apiBaseUrl,
    serverId: selectedServer,
    startTime: timeRange.startTime,
    endTime: timeRange.endTime,
    resolution,
    autoRefresh,
    refreshInterval: 30000
  });

  // Transform data for Recharts
  const chartData = useMemo<ChartDataPoint[]>(() => {
    if (!data) return [];

    if ('samples' in data) {
      // Raw samples
      const byTimestamp = new Map<number, ChartDataPoint>();

      for (const sample of data.samples as HealthSampleDto[]) {
        const ts = new Date(sample.timestamp).getTime();
        if (!byTimestamp.has(ts)) {
          byTimestamp.set(ts, { timestamp: ts });
        }
        byTimestamp.get(ts)![sample.serverId] = sample.latencyMs ?? undefined;
      }

      return Array.from(byTimestamp.values()).sort((a, b) => a.timestamp - b.timestamp);
    } else {
      // Hourly data
      const byTimestamp = new Map<number, ChartDataPoint>();

      for (const hourly of data.hourly) {
        const ts = new Date(hourly.hourStart).getTime();
        if (!byTimestamp.has(ts)) {
          byTimestamp.set(ts, { timestamp: ts });
        }
        byTimestamp.get(ts)![hourly.serverId] = hourly.avgLatencyMs ?? undefined;
      }

      return Array.from(byTimestamp.values()).sort((a, b) => a.timestamp - b.timestamp);
    }
  }, [data]);

  // Get unique server IDs from data
  const serverIds = useMemo(() => {
    if (!data) return [];

    const ids = new Set<string>();
    if ('samples' in data) {
      for (const sample of data.samples) {
        ids.add(sample.serverId);
      }
    } else {
      for (const hourly of data.hourly) {
        ids.add(hourly.serverId);
      }
    }
    return Array.from(ids).sort();
  }, [data]);

  const handleRangeChange = (startTime: Date, endTime: Date) => {
    setTimeRange({ startTime, endTime });
  };

  return (
    <div className="history-tab">
      <div className="history-tab-header">
        <h2>Historical Metrics</h2>
        <TimeRangeSelector onRangeChange={handleRangeChange} defaultRange="24h" />
      </div>

      <div className="history-tab-filters">
        <label>
          Server:
          <select
            value={selectedServer || ''}
            onChange={(e) => setSelectedServer(e.target.value || undefined)}
          >
            <option value="">All Servers</option>
            {serverIds.map(id => (
              <option key={id} value={id}>{id}</option>
            ))}
          </select>
        </label>
        <span className="resolution-badge">
          Resolution: {resolution === 'raw' ? 'Raw (5s)' : 'Hourly'}
        </span>
        <label className="auto-refresh-label">
          <input
            type="checkbox"
            checked={autoRefresh}
            onChange={(e) => setAutoRefresh(e.target.checked)}
          />
          Auto-refresh (30s)
        </label>
        <button onClick={refresh} disabled={isLoading} type="button" className="refresh-btn">
          Refresh
        </button>
      </div>

      {error && (
        <div className="history-tab-error">
          Error loading metrics: {error}
        </div>
      )}

      {isLoading && !data && (
        <div className="history-tab-loading">Loading metrics...</div>
      )}

      {data && chartData.length > 0 && (
        <LatencyChart data={chartData} serverIds={serverIds} />
      )}

      {data && chartData.length === 0 && (
        <div className="history-tab-empty">
          No metrics data for the selected time range.
        </div>
      )}

      <div className="history-tab-stats">
        {data && 'samples' in data && (
          <span>{data.totalCount} samples loaded</span>
        )}
        {data && 'hourly' in data && (
          <span>{data.totalCount} hourly records loaded</span>
        )}
      </div>
    </div>
  );
}

export default HistoryTab;
