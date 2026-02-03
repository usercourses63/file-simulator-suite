import { useState, useEffect, useCallback } from 'react';
import type {
  TopicInfo,
  CreateTopicRequest,
  ConsumerGroupInfo,
  ConsumerGroupDetail,
  KafkaMessage,
  ProduceMessageRequest,
  ProduceMessageResult,
  ResetOffsetsRequest
} from '../types/kafka';

/**
 * Options for useKafka hook.
 */
interface UseKafkaOptions {
  /** Base URL for the API (e.g., "http://172.25.174.184:30500") */
  apiBaseUrl: string;
  /** Auto-refresh interval in milliseconds (default: 5000) */
  refreshInterval?: number;
}

/**
 * Result returned by useKafka hook.
 */
interface UseKafkaResult {
  // Topics
  topics: TopicInfo[];
  topicsLoading: boolean;
  topicsError: string | null;
  refreshTopics: () => Promise<void>;
  createTopic: (request: CreateTopicRequest) => Promise<void>;
  deleteTopic: (name: string) => Promise<void>;

  // Consumer Groups
  consumerGroups: ConsumerGroupInfo[];
  groupsLoading: boolean;
  groupsError: string | null;
  refreshGroups: () => Promise<void>;
  getGroupDetail: (groupId: string) => Promise<ConsumerGroupDetail>;
  resetOffsets: (request: ResetOffsetsRequest) => Promise<void>;
  deleteGroup: (groupId: string) => Promise<void>;

  // Messages
  getMessages: (topic: string, count?: number) => Promise<KafkaMessage[]>;
  produceMessage: (request: ProduceMessageRequest) => Promise<ProduceMessageResult>;

  // Health
  isHealthy: boolean | null;
}

/**
 * Custom hook for Kafka REST API operations.
 * Provides topic management, consumer group management, and message operations.
 *
 * @param options - Configuration options
 * @returns API methods and state for Kafka operations
 *
 * @example
 * const {
 *   topics,
 *   topicsLoading,
 *   topicsError,
 *   consumerGroups,
 *   isHealthy
 * } = useKafka({ apiBaseUrl: 'http://localhost:30500' });
 */
export function useKafka({ apiBaseUrl, refreshInterval = 5000 }: UseKafkaOptions): UseKafkaResult {
  const [topics, setTopics] = useState<TopicInfo[]>([]);
  const [topicsLoading, setTopicsLoading] = useState(true);
  const [topicsError, setTopicsError] = useState<string | null>(null);

  const [consumerGroups, setConsumerGroups] = useState<ConsumerGroupInfo[]>([]);
  const [groupsLoading, setGroupsLoading] = useState(true);
  const [groupsError, setGroupsError] = useState<string | null>(null);

  const [isHealthy, setIsHealthy] = useState<boolean | null>(null);

  const baseUrl = `${apiBaseUrl}/api/kafka`;

  // Fetch topics
  const refreshTopics = useCallback(async () => {
    try {
      const response = await fetch(`${baseUrl}/topics`);
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const data = await response.json();
      setTopics(data);
      setTopicsError(null);
    } catch (err) {
      setTopicsError(err instanceof Error ? err.message : 'Failed to fetch topics');
    } finally {
      setTopicsLoading(false);
    }
  }, [baseUrl]);

  // Fetch consumer groups
  const refreshGroups = useCallback(async () => {
    try {
      const response = await fetch(`${baseUrl}/consumer-groups`);
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const data = await response.json();
      setConsumerGroups(data);
      setGroupsError(null);
    } catch (err) {
      setGroupsError(err instanceof Error ? err.message : 'Failed to fetch groups');
    } finally {
      setGroupsLoading(false);
    }
  }, [baseUrl]);

  // Check health
  const checkHealth = useCallback(async () => {
    try {
      const response = await fetch(`${baseUrl}/health`);
      setIsHealthy(response.ok);
    } catch {
      setIsHealthy(false);
    }
  }, [baseUrl]);

  // Initial fetch and auto-refresh
  useEffect(() => {
    refreshTopics();
    refreshGroups();
    checkHealth();

    const interval = setInterval(() => {
      refreshTopics();
      refreshGroups();
      checkHealth();
    }, refreshInterval);

    return () => clearInterval(interval);
  }, [refreshTopics, refreshGroups, checkHealth, refreshInterval]);

  // Create topic
  const createTopic = useCallback(async (request: CreateTopicRequest) => {
    const response = await fetch(`${baseUrl}/topics`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || `HTTP ${response.status}`);
    }
    await refreshTopics();
  }, [baseUrl, refreshTopics]);

  // Delete topic
  const deleteTopic = useCallback(async (name: string) => {
    const response = await fetch(`${baseUrl}/topics/${encodeURIComponent(name)}`, {
      method: 'DELETE'
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || `HTTP ${response.status}`);
    }
    await refreshTopics();
  }, [baseUrl, refreshTopics]);

  // Get group detail
  const getGroupDetail = useCallback(async (groupId: string): Promise<ConsumerGroupDetail> => {
    const response = await fetch(`${baseUrl}/consumer-groups/${encodeURIComponent(groupId)}`);
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || `HTTP ${response.status}`);
    }
    return response.json();
  }, [baseUrl]);

  // Reset offsets
  const resetOffsets = useCallback(async (request: ResetOffsetsRequest) => {
    const response = await fetch(
      `${baseUrl}/consumer-groups/${encodeURIComponent(request.groupId)}/reset`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
      }
    );
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || `HTTP ${response.status}`);
    }
    await refreshGroups();
  }, [baseUrl, refreshGroups]);

  // Delete group
  const deleteGroup = useCallback(async (groupId: string) => {
    const response = await fetch(
      `${baseUrl}/consumer-groups/${encodeURIComponent(groupId)}`,
      { method: 'DELETE' }
    );
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || `HTTP ${response.status}`);
    }
    await refreshGroups();
  }, [baseUrl, refreshGroups]);

  // Get messages
  const getMessages = useCallback(async (topic: string, count = 50): Promise<KafkaMessage[]> => {
    const response = await fetch(
      `${baseUrl}/topics/${encodeURIComponent(topic)}/messages?count=${count}`
    );
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || `HTTP ${response.status}`);
    }
    return response.json();
  }, [baseUrl]);

  // Produce message
  const produceMessage = useCallback(async (request: ProduceMessageRequest): Promise<ProduceMessageResult> => {
    const response = await fetch(
      `${baseUrl}/topics/${encodeURIComponent(request.topic)}/messages`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
      }
    );
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || `HTTP ${response.status}`);
    }
    return response.json();
  }, [baseUrl]);

  return {
    topics,
    topicsLoading,
    topicsError,
    refreshTopics,
    createTopic,
    deleteTopic,
    consumerGroups,
    groupsLoading,
    groupsError,
    refreshGroups,
    getGroupDetail,
    resetOffsets,
    deleteGroup,
    getMessages,
    produceMessage,
    isHealthy
  };
}

export default useKafka;
