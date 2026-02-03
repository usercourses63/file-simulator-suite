import { useKafka } from '../hooks/useKafka';
import { getLagLevel } from '../types/kafka';
import './KafkaTab.css';

/**
 * Props for KafkaTab component.
 */
interface KafkaTabProps {
  /** Base URL for the API (e.g., "http://172.25.174.184:30500") */
  apiBaseUrl: string;
}

/**
 * KafkaTab component displays Kafka topics and consumer groups.
 * Shows health status, topic list with partition counts,
 * and consumer groups with lag indicators.
 */
function KafkaTab({ apiBaseUrl }: KafkaTabProps) {
  const {
    topics,
    topicsLoading,
    topicsError,
    consumerGroups,
    groupsLoading,
    groupsError,
    isHealthy
  } = useKafka({ apiBaseUrl });

  return (
    <div className="kafka-tab">
      {/* Health Status */}
      <div className="kafka-health">
        <span className={`kafka-health__indicator kafka-health__indicator--${isHealthy ? 'healthy' : isHealthy === false ? 'unhealthy' : 'unknown'}`} />
        <span className="kafka-health__label">
          Kafka: {isHealthy ? 'Connected' : isHealthy === false ? 'Disconnected' : 'Checking...'}
        </span>
      </div>

      <div className="kafka-content">
        {/* Topics Section */}
        <section className="kafka-section">
          <header className="kafka-section__header">
            <h2>Topics</h2>
            <span className="kafka-section__count">{topics.length}</span>
          </header>

          {topicsLoading ? (
            <div className="kafka-loading">Loading topics...</div>
          ) : topicsError ? (
            <div className="kafka-error">{topicsError}</div>
          ) : topics.length === 0 ? (
            <div className="kafka-empty">No topics found</div>
          ) : (
            <div className="kafka-topics">
              {topics.map(topic => (
                <div key={topic.name} className="kafka-topic">
                  <span className="kafka-topic__name">{topic.name}</span>
                  <span className="kafka-topic__partitions">{topic.partitionCount} partitions</span>
                </div>
              ))}
            </div>
          )}
        </section>

        {/* Consumer Groups Section */}
        <section className="kafka-section">
          <header className="kafka-section__header">
            <h2>Consumer Groups</h2>
            <span className="kafka-section__count">{consumerGroups.length}</span>
          </header>

          {groupsLoading ? (
            <div className="kafka-loading">Loading consumer groups...</div>
          ) : groupsError ? (
            <div className="kafka-error">{groupsError}</div>
          ) : consumerGroups.length === 0 ? (
            <div className="kafka-empty">No consumer groups found</div>
          ) : (
            <div className="kafka-groups">
              {consumerGroups.map(group => (
                <div key={group.groupId} className="kafka-group">
                  <span className="kafka-group__id">{group.groupId}</span>
                  <span className="kafka-group__state">{group.state}</span>
                  <span className="kafka-group__members">{group.memberCount} members</span>
                  <span className={`kafka-group__lag kafka-group__lag--${getLagLevel(group.totalLag)}`}>
                    Lag: {group.totalLag}
                  </span>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

export default KafkaTab;
