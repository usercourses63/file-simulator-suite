import { useState, useEffect } from 'react';
import type {
  ConsumerGroupInfo,
  ConsumerGroupDetail as GroupDetail,
  ResetOffsetsRequest
} from '../types/kafka';
import { getLagLevel } from '../types/kafka';

/**
 * Props for ConsumerGroupDetail component.
 */
interface ConsumerGroupDetailProps {
  /** Consumer group summary info */
  group: ConsumerGroupInfo;
  /** Function to fetch group detail */
  getDetail: (groupId: string) => Promise<GroupDetail>;
  /** Function to reset group offsets */
  onResetOffsets: (request: ResetOffsetsRequest) => Promise<void>;
  /** Function to delete the group */
  onDeleteGroup: (groupId: string) => Promise<void>;
}

/**
 * ConsumerGroupDetail component displays expandable consumer group information.
 * Shows members, partition offsets with lag coloring, and controls for:
 * - Reset offsets (only for Empty state groups)
 * - Delete group (with confirmation)
 */
function ConsumerGroupDetail({
  group,
  getDetail,
  onResetOffsets,
  onDeleteGroup
}: ConsumerGroupDetailProps) {
  const [expanded, setExpanded] = useState(false);
  const [detail, setDetail] = useState<GroupDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Reset offsets state
  const [showResetForm, setShowResetForm] = useState(false);
  const [resetTopic, setResetTopic] = useState('');
  const [resetTo, setResetTo] = useState<'earliest' | 'latest'>('earliest');
  const [resetting, setResetting] = useState(false);

  // Delete state
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

  // Fetch detail when expanded
  useEffect(() => {
    if (expanded && !detail) {
      loadDetail();
    }
  }, [expanded]);

  const loadDetail = async () => {
    setLoading(true);
    setError(null);
    try {
      const d = await getDetail(group.groupId);
      setDetail(d);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load details');
    } finally {
      setLoading(false);
    }
  };

  const handleReset = async () => {
    if (!resetTopic) {
      setError('Please select a topic');
      return;
    }
    setResetting(true);
    setError(null);
    try {
      await onResetOffsets({
        groupId: group.groupId,
        topic: resetTopic,
        resetTo
      });
      setShowResetForm(false);
      await loadDetail();  // Refresh
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to reset offsets');
    } finally {
      setResetting(false);
    }
  };

  const handleDelete = async () => {
    setDeleting(true);
    try {
      await onDeleteGroup(group.groupId);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete group');
      setDeleting(false);
      setConfirmDelete(false);
    }
  };

  // Get unique topics from partitions
  const topics = detail
    ? [...new Set(detail.partitions.map(p => p.topic))]
    : [];

  return (
    <div className="consumer-group-detail">
      <div
        className="consumer-group-detail__header"
        onClick={() => setExpanded(!expanded)}
      >
        <span className="consumer-group-detail__expand">
          {expanded ? '\u25BC' : '\u25B6'}
        </span>
        <span className="consumer-group-detail__id">{group.groupId}</span>
        <span className="consumer-group-detail__state">{group.state}</span>
        <span className="consumer-group-detail__members">
          {group.memberCount} member{group.memberCount !== 1 ? 's' : ''}
        </span>
        <span className={`consumer-group-detail__lag consumer-group-detail__lag--${getLagLevel(group.totalLag)}`}>
          Lag: {group.totalLag}
        </span>
      </div>

      {expanded && (
        <div className="consumer-group-detail__body">
          {loading ? (
            <div className="consumer-group-detail__loading">Loading...</div>
          ) : error ? (
            <div className="consumer-group-detail__error">{error}</div>
          ) : detail ? (
            <>
              {/* Members */}
              {detail.members.length > 0 && (
                <div className="consumer-group-detail__section">
                  <h5>Members ({detail.members.length})</h5>
                  <ul className="consumer-group-detail__members-list">
                    {detail.members.map(m => (
                      <li key={m.memberId}>
                        <span className="consumer-group-detail__member-client">{m.clientId}</span>
                        <span className="consumer-group-detail__member-host">{m.host}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Partitions */}
              {detail.partitions.length > 0 && (
                <div className="consumer-group-detail__section">
                  <h5>Partition Offsets</h5>
                  <table className="consumer-group-detail__partitions">
                    <thead>
                      <tr>
                        <th>Topic</th>
                        <th>Partition</th>
                        <th>Current</th>
                        <th>High</th>
                        <th>Lag</th>
                      </tr>
                    </thead>
                    <tbody>
                      {detail.partitions.map(p => (
                        <tr key={`${p.topic}-${p.partition}`}>
                          <td>{p.topic}</td>
                          <td>{p.partition}</td>
                          <td>{p.currentOffset}</td>
                          <td>{p.highWatermark}</td>
                          <td className={`consumer-group-detail__partition-lag--${getLagLevel(p.lag)}`}>
                            {p.lag}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {/* Actions */}
              <div className="consumer-group-detail__actions">
                {/* Reset Offsets */}
                {group.state === 'Empty' ? (
                  showResetForm ? (
                    <div className="consumer-group-detail__reset-form">
                      <select
                        value={resetTopic}
                        onChange={e => setResetTopic(e.target.value)}
                        disabled={resetting}
                      >
                        <option value="">Select topic</option>
                        {topics.map(t => (
                          <option key={t} value={t}>{t}</option>
                        ))}
                      </select>
                      <select
                        value={resetTo}
                        onChange={e => setResetTo(e.target.value as 'earliest' | 'latest')}
                        disabled={resetting}
                      >
                        <option value="earliest">Earliest</option>
                        <option value="latest">Latest</option>
                      </select>
                      <button onClick={handleReset} disabled={resetting}>
                        {resetting ? 'Resetting...' : 'Reset'}
                      </button>
                      <button onClick={() => setShowResetForm(false)} disabled={resetting}>
                        Cancel
                      </button>
                    </div>
                  ) : (
                    <button
                      className="consumer-group-detail__action-btn"
                      onClick={() => setShowResetForm(true)}
                    >
                      Reset Offsets
                    </button>
                  )
                ) : (
                  <span className="consumer-group-detail__action-hint">
                    Group must be inactive to reset offsets
                  </span>
                )}

                {/* Delete */}
                {confirmDelete ? (
                  <div className="consumer-group-detail__confirm-delete">
                    <span>Delete group?</span>
                    <button
                      className="consumer-group-detail__delete-confirm"
                      onClick={handleDelete}
                      disabled={deleting}
                    >
                      {deleting ? 'Deleting...' : 'Yes'}
                    </button>
                    <button
                      onClick={() => setConfirmDelete(false)}
                      disabled={deleting}
                    >
                      No
                    </button>
                  </div>
                ) : (
                  <button
                    className="consumer-group-detail__action-btn consumer-group-detail__action-btn--danger"
                    onClick={() => setConfirmDelete(true)}
                  >
                    Delete Group
                  </button>
                )}
              </div>
            </>
          ) : null}
        </div>
      )}
    </div>
  );
}

export default ConsumerGroupDetail;
