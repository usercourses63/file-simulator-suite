import { useState, useEffect, useCallback, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import type { KafkaMessage } from '../types/kafka';

/**
 * Options for useKafkaStream hook.
 */
interface UseKafkaStreamOptions {
  /** SignalR hub URL (e.g., "http://172.25.174.184:30500/hubs/kafka") */
  hubUrl: string;
  /** Topic to subscribe to */
  topic: string;
  /** Rolling buffer size for messages (default: 50) */
  maxMessages?: number;
  /** Enable/disable streaming (default: true) */
  enabled?: boolean;
}

/**
 * Result returned by useKafkaStream hook.
 */
interface UseKafkaStreamResult {
  /** Messages received from the topic (newest first) */
  messages: KafkaMessage[];
  /** Whether SignalR is connected */
  isConnected: boolean;
  /** Error message if connection failed */
  error: string | null;
  /** Clear all messages from the buffer */
  clearMessages: () => void;
}

/**
 * Custom hook for streaming Kafka messages via SignalR.
 * Connects to the Kafka SignalR hub and subscribes to a specific topic.
 *
 * @param options - Configuration options
 * @returns Streaming state and messages
 *
 * @example
 * const { messages, isConnected, error, clearMessages } = useKafkaStream({
 *   hubUrl: 'http://localhost:30500/hubs/kafka',
 *   topic: 'my-topic',
 *   maxMessages: 100
 * });
 */
export function useKafkaStream({
  hubUrl,
  topic,
  maxMessages = 50,
  enabled = true
}: UseKafkaStreamOptions): UseKafkaStreamResult {
  const [messages, setMessages] = useState<KafkaMessage[]>([]);
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    if (!enabled) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    connectionRef.current = connection;

    // Handle incoming Kafka messages
    connection.on('KafkaMessage', (message: KafkaMessage) => {
      setMessages(prev => {
        const updated = [message, ...prev];
        return updated.slice(0, maxMessages);
      });
    });

    // Connection lifecycle handlers
    connection.onclose((err) => {
      setIsConnected(false);
      if (err) setError(err.message);
    });

    connection.onreconnecting((err) => {
      setIsConnected(false);
      if (err) setError(`Reconnecting: ${err.message}`);
    });

    connection.onreconnected(() => {
      setIsConnected(true);
      setError(null);
      // Re-subscribe after reconnection
      connection.invoke('SubscribeToTopic', topic).catch(console.error);
    });

    // Start connection and subscribe to topic
    connection.start()
      .then(() => {
        setIsConnected(true);
        setError(null);
        return connection.invoke('SubscribeToTopic', topic);
      })
      .catch(err => {
        setError(err.message);
      });

    // Cleanup on unmount or when dependencies change
    return () => {
      connection.invoke('UnsubscribeFromTopic', topic)
        .catch(() => {})  // Ignore errors on cleanup
        .finally(() => connection.stop());
    };
  }, [hubUrl, topic, maxMessages, enabled]);

  const clearMessages = useCallback(() => {
    setMessages([]);
  }, []);

  return {
    messages,
    isConnected,
    error,
    clearMessages
  };
}

export default useKafkaStream;
