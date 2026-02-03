import { useState } from 'react';
import { useKafka } from '../hooks/useKafka';
import TopicList from './TopicList';
import CreateTopicForm from './CreateTopicForm';
import MessageProducer from './MessageProducer';
import MessageViewer from './MessageViewer';
import ConsumerGroupDetail from './ConsumerGroupDetail';
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
 * Shows topics (with create/delete), message producer/consumer, and consumer groups.
 * Layout: Left panel (topics) | Center (message producer/viewer) | Right (consumer groups)
 */
function KafkaTab({ apiBaseUrl }: KafkaTabProps) {
  const kafkaHubUrl = `${apiBaseUrl}/hubs/kafka`;

  const {
    topics,
    topicsLoading,
    topicsError,
    createTopic,
    deleteTopic,
    consumerGroups,
    groupsLoading,
    groupsError,
    getGroupDetail,
    resetOffsets,
    deleteGroup,
    getMessages,
    produceMessage,
    isHealthy
  } = useKafka({ apiBaseUrl });

  const [selectedTopic, setSelectedTopic] = useState<string | null>(null);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [viewMode, setViewMode] = useState<'produce' | 'consume'>('produce');

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

        {/* Center: Producer/Consumer */}
        <div className="kafka-panel kafka-panel--messages">
          {selectedTopic ? (
            <>
              <div className="kafka-view-toggle">
                <button
                  className={`kafka-view-toggle__btn ${viewMode === 'produce' ? 'kafka-view-toggle__btn--active' : ''}`}
                  onClick={() => setViewMode('produce')}
                  type="button"
                >
                  Produce
                </button>
                <button
                  className={`kafka-view-toggle__btn ${viewMode === 'consume' ? 'kafka-view-toggle__btn--active' : ''}`}
                  onClick={() => setViewMode('consume')}
                  type="button"
                >
                  Consume
                </button>
              </div>

              {viewMode === 'produce' ? (
                <MessageProducer
                  topic={selectedTopic}
                  onProduce={produceMessage}
                />
              ) : (
                <MessageViewer
                  topic={selectedTopic}
                  hubUrl={kafkaHubUrl}
                  getMessages={getMessages}
                />
              )}
            </>
          ) : (
            <div className="kafka-placeholder">
              Select a topic to produce or consume messages
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
            <div className="kafka-groups-list">
              {consumerGroups.map(group => (
                <ConsumerGroupDetail
                  key={group.groupId}
                  group={group}
                  getDetail={getGroupDetail}
                  onResetOffsets={resetOffsets}
                  onDeleteGroup={deleteGroup}
                />
              ))}
            </div>
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
