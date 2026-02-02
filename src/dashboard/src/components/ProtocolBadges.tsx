import type { FileProtocol } from '../types/fileTypes';

interface ProtocolBadgesProps {
  protocols: FileProtocol[];
  size?: 'small' | 'normal';
}

/**
 * Displays protocol visibility badges for a file/directory.
 * Shows which protocols can access the item.
 */
export function ProtocolBadges({ protocols, size = 'normal' }: ProtocolBadgesProps) {
  if (protocols.length === 0) {
    return null;
  }

  const sizeClass = size === 'small' ? 'protocol-badges--small' : '';

  return (
    <div className={`protocol-badges ${sizeClass}`}>
      {protocols.map(protocol => (
        <span
          key={protocol}
          className={`protocol-badge protocol-badge--${protocol.toLowerCase()}`}
          title={`Accessible via ${protocol}`}
        >
          {protocol}
        </span>
      ))}
    </div>
  );
}

export default ProtocolBadges;
