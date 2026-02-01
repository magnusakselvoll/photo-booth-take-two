import { useEffect, useRef } from 'react';
import type { PhotoBoothEvent } from './types';

type EventHandler = (event: PhotoBoothEvent) => void;

const MIN_RECONNECT_DELAY_MS = 1000;
const MAX_RECONNECT_DELAY_MS = 30000;

export function useEventStream(onEvent: EventHandler) {
  const eventSourceRef = useRef<EventSource | null>(null);
  const onEventRef = useRef<EventHandler>(onEvent);
  const reconnectTimeoutRef = useRef<number | null>(null);
  const reconnectDelayRef = useRef<number>(MIN_RECONNECT_DELAY_MS);

  // Keep the callback ref up to date
  useEffect(() => {
    onEventRef.current = onEvent;
  });

  useEffect(() => {
    const parseAndDispatch = (e: MessageEvent) => {
      try {
        const data = JSON.parse(e.data);
        onEventRef.current(data);
      } catch (err) {
        console.error('Failed to parse event:', err);
      }
    };

    const connect = () => {
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
      }

      const eventSource = new EventSource('/api/events');
      eventSourceRef.current = eventSource;

      eventSource.onopen = () => {
        // Reset backoff on successful connection
        reconnectDelayRef.current = MIN_RECONNECT_DELAY_MS;
      };

      eventSource.addEventListener('countdown-started', parseAndDispatch);
      eventSource.addEventListener('photo-captured', parseAndDispatch);
      eventSource.addEventListener('capture-failed', parseAndDispatch);

      eventSource.onerror = () => {
        eventSource.close();
        // Reconnect with exponential backoff
        const delay = reconnectDelayRef.current;
        reconnectDelayRef.current = Math.min(delay * 2, MAX_RECONNECT_DELAY_MS);
        reconnectTimeoutRef.current = window.setTimeout(connect, delay);
      };
    };

    connect();

    return () => {
      eventSourceRef.current?.close();
      if (reconnectTimeoutRef.current !== null) {
        clearTimeout(reconnectTimeoutRef.current);
      }
    };
  }, []);
}
