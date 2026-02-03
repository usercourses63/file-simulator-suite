import { Sparklines, SparklinesLine, SparklinesReferenceLine } from 'react-sparklines';

interface ServerSparklineProps {
  data: number[];  // Last N latency values (nulls converted to 0)
  isHealthy: boolean;
  onClick?: () => void;
  width?: number;
  height?: number;
}

/**
 * Mini sparkline showing latency trend for a server.
 * Colored green when healthy, red when unhealthy.
 * Click to navigate to History tab filtered to this server.
 */
export function ServerSparkline({
  data,
  isHealthy,
  onClick,
  width = 80,
  height = 20
}: ServerSparklineProps) {
  // Handle empty data
  if (data.length === 0) {
    return (
      <div
        className="server-sparkline server-sparkline--empty"
        onClick={onClick}
        title="No metrics data yet"
      >
        <span className="sparkline-placeholder">--</span>
      </div>
    );
  }

  const color = isHealthy ? '#22c55e' : '#ef4444';

  return (
    <div
      className="server-sparkline"
      onClick={onClick}
      title="Click to view history"
      role="button"
      tabIndex={0}
      onKeyDown={(e) => e.key === 'Enter' && onClick?.()}
    >
      <Sparklines data={data} width={width} height={height} margin={2}>
        <SparklinesLine color={color} style={{ strokeWidth: 1.5, fill: 'none' }} />
        <SparklinesReferenceLine type="mean" style={{ stroke: '#999', strokeDasharray: '2,2' }} />
      </Sparklines>
    </div>
  );
}

export default ServerSparkline;
