import { useState } from 'react';
import { useKafka } from '../hooks/useKafka';
import { getLagLevel } from '../types/kafka';
import TopicList from './TopicList';
import CreateTopicForm from './CreateTopicForm';
import MessageProducer from './MessageProducer';
import './KafkaTab.css';

/**
 * Props for KafkaTab component.
 */
interface KafkaTabProps {
  /** Base URL for the API (e.g., "http://172.25.174.184:30500") */
  apiBaseUrl: string;
}

/**
 * KafkaTab component displays Kafka management UI.
 * Shows topics (with create/delete), message producer, and consumer groups.
 * Layout: Left panel (topics) | Center (message producer) | Right (consumer groups)
 */
function KafkaTab({ apiBaseUrl }: KafkaTabProps) {
  const {
    topics,
    topicsLoading,
    topicsError,
    createTopic,
    deleteTopic,
    consumerGroups,
    groupsLoading,
    groupsError,
    produceMessage,
    isHealthy
  } = useKafka({ apiBaseUrl });

  const [selectedTopic, setSelectedTopic] = useState<string | null>(null);
  const [showCreateForm, setShowCreateForm] = useState(false);

  return (
    <div className="kafka-tab">
      {/* Health Status */}
      <div className="kafka-health">
        <span className={`kafka-health__indicator kafka-health__indicator--${isHealthy ? 'healthy' : isHealthy === false ? 'unhealthy' : 'unknown'}`} />
        <span className="kafka-health__label">
          Kafka: {isHealthy ? 'Connected' : isHealthy === false ? 'Disconnected' : 'Checking...'}
        </span>
      </div>

      <div className="kafka-layout">
        {/* Left: Topics */}
        <div className="kafka-panel kafka-panel--topics">
          {topicsLoading ? (
            <div className="kafka-loading">Loading topics...</div>
          ) : topicsError ? (
            <div className="kafka-error">{topicsError}</div>
          ) : (
            <TopicList
              topics={topics}
              selectedTopic={selectedTopic}
              onSelectTopic={setSelectedTopic}
              onDeleteTopic={deleteTopic}
              onCreateClick={() => setShowCreateForm(true)}
            />
          )}
        </div>

        {/* Center: Message Producer (when topic selected) */}
        <div className="kafka-panel kafka-panel--producer">
          {selectedTopic ? (
            <MessageProducer
              topic={selectedTopic}
              onProduce={produceMessage}
            />
          ) : (
            <div className="kafka-placeholder">
              Select a topic to produce messages
            </div>
          )}
        </div>

        {/* Right: Consumer Groups */}
        <div className="kafka-panel kafka-panel--groups">
          <h3>Consumer Groups</h3>
          {groupsLoading ? (
            <div className="kafka-loading">Loading groups...</div>
          ) : groupsError ? (
            <div className="kafka-error">{groupsError}</div>
          ) : consumerGroups.length === 0 ? (
            <div className="kafka-empty">No consumer groups</div>
          ) : (
            <ul className="kafka-group-list">
              {consumerGroups.map(group => (
                <li key={group.groupId} className="kafka-group-item">
                  <span className="kafka-group-item__id">{group.groupId}</span>
                  <span className="kafka-group-item__state">{group.state}</span>
                  <span className="kafka-group-item__members">{group.memberCount} members</span>
                  <span className={`kafka-group-item__lag kafka-group-item__lag--${getLagLevel(group.totalLag)}`}>
                    Lag: {group.totalLag}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      {/* Create Topic Modal */}
      {showCreateForm && (
        <CreateTopicForm
          onSubmit={createTopic}
          onCancel={() => setShowCreateForm(false)}
        />
      )}
    </div>
  );
}

export default KafkaTab;
