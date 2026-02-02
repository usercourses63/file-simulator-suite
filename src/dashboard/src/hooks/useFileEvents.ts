import { useEffect, useState, useRef, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import type { FileEvent } from '../types/fileTypes';

/**
 * Result returned by useFileEvents hook.
 */
export interface UseFileEventsResult {
  /** Recent file events (newest first, max 50) */
  events: FileEvent[];
  /** Whether connected to file events hub */
  isConnected: boolean;
  /** Error message if connection failed */
  error: string | null;
  /** Clear all events from the feed */
  clearEvents: () => void;
}

/**
 * Hook for receiving real-time file events via SignalR.
 * Maintains a rolling buffer of the last 50 events.
 *
 * @param hubUrl - URL of the file events hub (e.g., "http://192.168.49.2:30500/hubs/fileevents")
 * @returns Connection state and events array
 */
export function useFileEvents(hubUrl: string): UseFileEventsResult {
  const [events, setEvents] = useState<FileEvent[]>([]);
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const isMountedRef = useRef(true);
  const maxEvents = 50;

  const clearEvents = useCallback(() => {
    setEvents([]);
  }, []);

  useEffect(() => {
    isMountedRef.current = true;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connectionRef.current = connection;

    // Handle file events
    connection.on('FileEvent', (event: FileEvent) => {
      if (!isMountedRef.current) return;

      setEvents(prev => [event, ...prev].slice(0, maxEvents));
    });

    connection.onreconnecting(() => {
      if (!isMountedRef.current) return;
      setIsConnected(false);
      console.log('FileEvents hub reconnecting...');
    });

    connection.onreconnected(() => {
      if (!isMountedRef.current) return;
      setIsConnected(true);
      setError(null);
      console.log('FileEvents hub reconnected');
    });

    connection.onclose((err) => {
      if (!isMountedRef.current) return;
      setIsConnected(false);
      if (err) {
        setError(err.message);
      }
    });

    connection.start()
      .then(() => {
        if (!isMountedRef.current) return;
        setIsConnected(true);
        setError(null);
        console.log('Connected to FileEvents hub');
      })
      .catch((err) => {
        if (!isMountedRef.current) return;
        setError(err.message);
        console.error('FileEvents connection failed:', err);
      });

    return () => {
      isMountedRef.current = false;
      connection.off('FileEvent');
      connection.stop().catch(console.warn);
    };
  }, [hubUrl]);

  return { events, isConnected, error, clearEvents };
}

export default useFileEvents;
