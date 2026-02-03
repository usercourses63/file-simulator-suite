import { useState, useCallback } from 'react';
import {
  LineChart, Line, XAxis, YAxis, CartesianGrid,
  Tooltip, Legend, ReferenceArea, ResponsiveContainer
} from 'recharts';
import { ChartDataPoint } from '../types/metrics';

interface LatencyChartProps {
  data: ChartDataPoint[];
  serverIds: string[];
}

interface ZoomState {
  left: number | 'dataMin';
  right: number | 'dataMax';
  refAreaLeft: number | null;
  refAreaRight: number | null;
}

const COLORS = ['#8884d8', '#82ca9d', '#ffc658', '#ff7300', '#0088fe', '#00C49F', '#FFBB28', '#FF8042', '#a855f7', '#ec4899', '#14b8a6', '#f97316', '#ef4444'];

/**
 * Recharts LineChart with click-and-drag zoom functionality.
 * Uses ReferenceArea to show zoom selection during drag.
 */
export function LatencyChart({ data, serverIds }: LatencyChartProps) {
  const [zoomState, setZoomState] = useState<ZoomState>({
    left: 'dataMin',
    right: 'dataMax',
    refAreaLeft: null,
    refAreaRight: null
  });

  // Cast to any for Recharts event handlers due to complex internal types
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const handleMouseDown = useCallback((state: any) => {
    if (state?.activeLabel !== undefined) {
      const label = typeof state.activeLabel === 'number'
        ? state.activeLabel
        : Number(state.activeLabel);
      if (!isNaN(label)) {
        setZoomState(prev => ({ ...prev, refAreaLeft: label }));
      }
    }
  }, []);

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const handleMouseMove = useCallback((state: any) => {
    if (zoomState.refAreaLeft !== null && state?.activeLabel !== undefined) {
      const label = typeof state.activeLabel === 'number'
        ? state.activeLabel
        : Number(state.activeLabel);
      if (!isNaN(label)) {
        setZoomState(prev => ({ ...prev, refAreaRight: label }));
      }
    }
  }, [zoomState.refAreaLeft]);

  const handleMouseUp = useCallback(() => {
    const { refAreaLeft, refAreaRight } = zoomState;

    if (refAreaLeft === refAreaRight || refAreaRight === null) {
      setZoomState(prev => ({ ...prev, refAreaLeft: null, refAreaRight: null }));
      return;
    }

    const [left, right] = refAreaLeft! < refAreaRight
      ? [refAreaLeft, refAreaRight]
      : [refAreaRight, refAreaLeft];

    setZoomState({
      left: left!,
      right: right!,
      refAreaLeft: null,
      refAreaRight: null
    });
  }, [zoomState.refAreaLeft, zoomState.refAreaRight]);

  const handleZoomOut = useCallback(() => {
    setZoomState({
      left: 'dataMin',
      right: 'dataMax',
      refAreaLeft: null,
      refAreaRight: null
    });
  }, []);

  const formatTime = (ts: number) => new Date(ts).toLocaleTimeString();

  return (
    <div className="latency-chart">
      <div className="latency-chart-controls">
        <button
          onClick={handleZoomOut}
          disabled={zoomState.left === 'dataMin'}
          className="zoom-reset-btn"
          type="button"
        >
          Reset Zoom
        </button>
        <span className="zoom-hint">Drag to zoom</span>
      </div>
      <ResponsiveContainer width="100%" height={400}>
        <LineChart
          data={data}
          onMouseDown={handleMouseDown}
          onMouseMove={handleMouseMove}
          onMouseUp={handleMouseUp}
        >
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis
            dataKey="timestamp"
            domain={[zoomState.left, zoomState.right]}
            type="number"
            tickFormatter={formatTime}
            scale="time"
          />
          <YAxis domain={['auto', 'auto']} unit="ms" />
          <Tooltip
            labelFormatter={(label) => {
              const ts = typeof label === 'number' ? label : Number(label);
              return new Date(ts).toLocaleString();
            }}
            formatter={(value) => {
              if (value === null || value === undefined) return ['-', 'Latency'];
              return [`${value}ms`, 'Latency'];
            }}
          />
          <Legend />
          {serverIds.map((serverId, index) => (
            <Line
              key={serverId}
              type="monotone"
              dataKey={serverId}
              stroke={COLORS[index % COLORS.length]}
              dot={false}
              name={serverId}
              connectNulls
            />
          ))}
          {zoomState.refAreaLeft !== null && zoomState.refAreaRight !== null && (
            <ReferenceArea
              x1={zoomState.refAreaLeft}
              x2={zoomState.refAreaRight}
              strokeOpacity={0.3}
              fill="#8884d8"
              fillOpacity={0.3}
            />
          )}
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

export default LatencyChart;
