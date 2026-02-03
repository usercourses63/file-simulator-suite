import { useState } from 'react';

interface TimeRangeSelectorProps {
  onRangeChange: (startTime: Date, endTime: Date) => void;
  defaultRange?: string;
}

const PRESETS = [
  { label: '1h', value: '1h' },
  { label: '6h', value: '6h' },
  { label: '24h', value: '24h' },
  { label: '7d', value: '7d' }
];

const DROPDOWN_OPTIONS = [
  { label: '15 minutes', value: '15m' },
  { label: '30 minutes', value: '30m' },
  { label: '1 hour', value: '1h' },
  { label: '2 hours', value: '2h' },
  { label: '6 hours', value: '6h' },
  { label: '12 hours', value: '12h' },
  { label: '24 hours', value: '24h' },
  { label: '3 days', value: '3d' },
  { label: '7 days', value: '7d' }
];

function parseRange(value: string): { startTime: Date; endTime: Date } {
  const endTime = new Date();
  const startTime = new Date();

  const match = value.match(/^(\d+)([mhd])$/);
  if (!match) throw new Error(`Invalid range: ${value}`);

  const [, num, unit] = match;
  const amount = parseInt(num, 10);

  switch (unit) {
    case 'm': startTime.setMinutes(startTime.getMinutes() - amount); break;
    case 'h': startTime.setHours(startTime.getHours() - amount); break;
    case 'd': startTime.setDate(startTime.getDate() - amount); break;
  }

  return { startTime, endTime };
}

/**
 * Time range selector with preset buttons and dropdown.
 * Used in HistoryTab for selecting metrics time range.
 */
export function TimeRangeSelector({ onRangeChange, defaultRange = '24h' }: TimeRangeSelectorProps) {
  const [selectedRange, setSelectedRange] = useState(defaultRange);

  const handleRangeSelect = (value: string) => {
    setSelectedRange(value);
    const { startTime, endTime } = parseRange(value);
    onRangeChange(startTime, endTime);
  };

  return (
    <div className="time-range-selector">
      <div className="time-range-presets">
        {PRESETS.map(preset => (
          <button
            key={preset.value}
            className={`time-range-preset ${selectedRange === preset.value ? 'time-range-preset--active' : ''}`}
            onClick={() => handleRangeSelect(preset.value)}
            type="button"
          >
            {preset.label}
          </button>
        ))}
      </div>
      <select
        className="time-range-dropdown"
        value={selectedRange}
        onChange={(e) => handleRangeSelect(e.target.value)}
      >
        {DROPDOWN_OPTIONS.map(opt => (
          <option key={opt.value} value={opt.value}>{opt.label}</option>
        ))}
      </select>
    </div>
  );
}

export default TimeRangeSelector;
