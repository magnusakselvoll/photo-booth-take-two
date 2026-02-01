import { useEffect, useRef } from 'react';
import type { PhotoBoothEvent } from './types';

type EventHandler = (event: PhotoBoothEvent) => void;

export function useEventStream(onEvent: EventHandler) {
  const eventSourceRef = useRef<EventSource | null>(null);
  const onEventRef = useRef<EventHandler>(onEvent);
  const reconnectTimeoutRef = useRef<number | null>(null);

  // Keep the callback ref up to date
  useEffect(() => {
    onEventRef.current = onEvent;
  });

  useEffect(() => {
    const connect = () => {
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
      }

      const eventSource = new EventSource('/api/events');
      eventSourceRef.current = eventSource;

      eventSource.addEventListener('countdown-started', (e) => {
        const data = JSON.parse(e.data);
        onEventRef.current(data);
      });

      eventSource.addEventListener('photo-captured', (e) => {
        const data = JSON.parse(e.data);
        onEventRef.current(data);
      });

      eventSource.addEventListener('capture-failed', (e) => {
        const data = JSON.parse(e.data);
        onEventRef.current(data);
      });

      eventSource.onerror = () => {
        eventSource.close();
        // Reconnect after a delay
        reconnectTimeoutRef.current = window.setTimeout(connect, 3000);
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
