import { useState, useRef, useCallback } from 'react';
import type { ActivityEvent } from '../types/activity';

const MAX_EVENTS = 100;
const DEDUP_WINDOW_MS = 5000;

export function useActivityLog() {
  const [events, setEvents] = useState<ActivityEvent[]>([]);
  const dedupRef = useRef<Map<string, number>>(new Map());

  const addEvent = useCallback((event: ActivityEvent) => {
    const dedupKey = `${event.type}:${event.title}`;
    const now = Date.now();
    const lastSeen = dedupRef.current.get(dedupKey);

    if (lastSeen && now - lastSeen < DEDUP_WINDOW_MS) {
      return;
    }
    dedupRef.current.set(dedupKey, now);

    setEvents(prev => [event, ...prev].slice(0, MAX_EVENTS));
  }, []);

  const addEvents = useCallback((newEvents: ActivityEvent[]) => {
    const now = Date.now();
    const accepted: ActivityEvent[] = [];

    for (const event of newEvents) {
      const dedupKey = `${event.type}:${event.title}`;
      const lastSeen = dedupRef.current.get(dedupKey);
      if (lastSeen && now - lastSeen < DEDUP_WINDOW_MS) {
        continue;
      }
      dedupRef.current.set(dedupKey, now);
      accepted.push(event);
    }

    if (accepted.length > 0) {
      setEvents(prev => [...accepted, ...prev].slice(0, MAX_EVENTS));
    }
  }, []);

  const clearEvents = useCallback(() => {
    setEvents([]);
    dedupRef.current.clear();
  }, []);

  return { events, addEvent, addEvents, clearEvents };
}
