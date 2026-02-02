import { useEffect, useState, useRef } from 'react';
import * as signalR from '@microsoft/signalr';

/**
 * Result returned by useSignalR hook.
 */
export interface UseSignalRResult<T> {
  /** Latest data received from SignalR */
  data: T | null;
  /** Whether WebSocket is connected */
  isConnected: boolean;
  /** Whether currently reconnecting */
  isReconnecting: boolean;
  /** Current reconnection attempt number (resets on success) */
  reconnectAttempt: number;
  /** Error message if connection failed */
  error: string | null;
  /** Timestamp of last received message */
  lastUpdate: Date | null;
}

/**
 * Custom hook for managing SignalR WebSocket connections with automatic reconnection.
 *
 * @param hubUrl - URL of the SignalR hub (e.g., "http://localhost:30500/hubs/status")
 * @param eventName - Name of the event to subscribe to (e.g., "ServerStatusUpdate")
 * @returns Connection state and latest data
 *
 * @example
 * const { data, isConnected, isReconnecting, reconnectAttempt, error, lastUpdate } =
 *   useSignalR<ServerStatusUpdate>('http://localhost:30500/hubs/status', 'ServerStatusUpdate');
 */
export function useSignalR<T>(
  hubUrl: string,
  eventName: string
): UseSignalRResult<T> {
  const [data, setData] = useState<T | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [isReconnecting, setIsReconnecting] = useState(false);
  const [reconnectAttempt, setReconnectAttempt] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);

  // Use ref to store connection to prevent recreating on re-renders
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  // Track if component is mounted to prevent state updates after unmount
  const isMountedRef = useRef(true);

  useEffect(() => {
    isMountedRef.current = true;

    // Build connection with automatic reconnection
    // Custom retry delays: 0, 2000, 5000, 10000, 30000 ms
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connectionRef.current = connection;

    // Connection lifecycle handlers
    connection.onreconnecting((err) => {
      if (!isMountedRef.current) return;

      setIsReconnecting(true);
      setIsConnected(false);
      setReconnectAttempt(prev => prev + 1);
      console.log('SignalR reconnecting...', err?.message);
    });

    connection.onreconnected((connectionId) => {
      if (!isMountedRef.current) return;

      setIsReconnecting(false);
      setIsConnected(true);
      setReconnectAttempt(0);
      setError(null);
      console.log('SignalR reconnected, connectionId:', connectionId);
    });

    connection.onclose((err) => {
      if (!isMountedRef.current) return;

      setIsConnected(false);
      setIsReconnecting(false);
      if (err) {
        setError(err.message);
        console.error('SignalR connection closed with error:', err);
      } else {
        console.log('SignalR connection closed');
      }
    });

    // Subscribe to messages
    connection.on(eventName, (message: T) => {
      if (!isMountedRef.current) return;

      setData(message);
      setLastUpdate(new Date());
    });

    // Start connection
    connection.start()
      .then(() => {
        if (!isMountedRef.current) return;

        setIsConnected(true);
        setError(null);
        console.log('SignalR connected to', hubUrl);
      })
      .catch((err) => {
        if (!isMountedRef.current) return;

        setError(err.message);
        console.error('SignalR connection failed:', err);
      });

    // Cleanup on unmount
    return () => {
      isMountedRef.current = false;
      connection.off(eventName);
      connection.stop().catch(err => {
        console.warn('Error stopping SignalR connection:', err);
      });
    };
  }, [hubUrl, eventName]);

  return { data, isConnected, isReconnecting, reconnectAttempt, error, lastUpdate };
}

export default useSignalR;
