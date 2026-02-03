import { useState, useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { MetricsSampleEvent } from '../types/metrics';

interface UseMetricsStreamResult {
  latestSamples: Map<string, number[]>;  // serverId -> last 60 latency values
  isConnected: boolean;
  error: string | null;
}

const MAX_SAMPLES = 60;  // Keep last 60 samples (5 minutes at 5s interval)

/**
 * Hook for real-time metrics streaming via SignalR.
 * Receives MetricsSample events and maintains a rolling buffer of latency values per server.
 */
export function useMetricsStream(hubUrl: string): UseMetricsStreamResult {
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [latestSamples, setLatestSamples] = useState<Map<string, number[]>>(new Map());
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on('MetricsSample', (event: MetricsSampleEvent) => {
      setLatestSamples(prev => {
        const next = new Map(prev);
        for (const sample of event.samples) {
          const current = next.get(sample.serverId) || [];
          const latency = sample.latencyMs ?? 0;  // Convert null to 0 for sparkline
          const updated = [...current, latency].slice(-MAX_SAMPLES);
          next.set(sample.serverId, updated);
        }
        return next;
      });
    });

    connection.onreconnecting(() => {
      setIsConnected(false);
      setError('Reconnecting...');
    });

    connection.onreconnected(() => {
      setIsConnected(true);
      setError(null);
    });

    connection.onclose((err) => {
      setIsConnected(false);
      if (err) setError(err.message);
    });

    connection.start()
      .then(() => {
        setIsConnected(true);
        setError(null);
      })
      .catch((err) => {
        setError(err.message);
      });

    return () => {
      connection.stop();
    };
  }, [hubUrl]);

  return { latestSamples, isConnected, error };
}

export default useMetricsStream;
