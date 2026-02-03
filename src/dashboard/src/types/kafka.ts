/**
 * TypeScript types for Kafka integration.
 * Matches backend DTOs from FileSimulator.ControlApi.
 */

/**
 * Kafka topic information.
 * Matches backend TopicInfo record.
 */
export interface TopicInfo {
  name: string;
  partitionCount: number;
  replicationFactor: number;
  messageCount: number;
  lastActivity: string | null;
}

/**
 * Topic creation request.
 * Matches backend CreateTopicRequest record.
 */
export interface CreateTopicRequest {
  name: string;
  partitions: number;
  replicationFactor: number;
}

/**
 * Consumer group information (list view).
 * Matches backend ConsumerGroupInfo record.
 */
export interface ConsumerGroupInfo {
  groupId: string;
  state: string;
  memberCount: number;
  totalLag: number;
}

/**
 * Consumer group detail (expanded view).
 * Matches backend ConsumerGroupDetail record.
 */
export interface ConsumerGroupDetail {
  groupId: string;
  state: string;
  memberCount: number;
  totalLag: number;
  members: ConsumerGroupMember[];
  partitions: PartitionOffset[];
}

/**
 * Consumer group member.
 * Matches backend ConsumerGroupMember record.
 */
export interface ConsumerGroupMember {
  memberId: string;
  clientId: string;
  host: string;
}

/**
 * Partition offset information.
 * Matches backend PartitionOffset record.
 */
export interface PartitionOffset {
  topic: string;
  partition: number;
  currentOffset: number;
  highWatermark: number;
  lag: number;
}

/**
 * Message to produce.
 * Matches backend ProduceMessageRequest record.
 */
export interface ProduceMessageRequest {
  topic: string;
  key: string | null;
  value: string;
}

/**
 * Produced message result.
 * Matches backend ProduceMessageResult record.
 */
export interface ProduceMessageResult {
  topic: string;
  partition: number;
  offset: number;
  timestamp: string;
}

/**
 * Kafka message for display.
 * Matches backend KafkaMessage record.
 */
export interface KafkaMessage {
  topic: string;
  partition: number;
  offset: number;
  key: string | null;
  value: string;
  timestamp: string;
}

/**
 * Offset reset request.
 * Matches backend ResetOffsetsRequest record.
 */
export interface ResetOffsetsRequest {
  groupId: string;
  topic: string;
  resetTo: 'earliest' | 'latest';
}

/**
 * Lag level for color coding in UI.
 * - green: lag <= 10 (healthy)
 * - yellow: lag <= 100 (warning)
 * - red: lag > 100 (critical)
 */
export type LagLevel = 'green' | 'yellow' | 'red';

/**
 * Helper function to get lag level based on lag count.
 * Used for color-coding consumer group lag in the UI.
 *
 * @param lag - Number of messages behind
 * @returns LagLevel for styling
 */
export function getLagLevel(lag: number): LagLevel {
  if (lag <= 10) return 'green';
  if (lag <= 100) return 'yellow';
  return 'red';
}
