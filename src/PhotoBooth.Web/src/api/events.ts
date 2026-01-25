import { useEffect, useRef, useCallback } from 'react';
import type { PhotoBoothEvent } from './types';

type EventHandler = (event: PhotoBoothEvent) => void;

export function useEventStream(onEvent: EventHandler) {
  const eventSourceRef = useRef<EventSource | null>(null);
  const onEventRef = useRef(onEvent);

  onEventRef.current = onEvent;

  const connect = useCallback(() => {
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
      setTimeout(connect, 3000);
    };
  }, []);

  useEffect(() => {
    connect();

    return () => {
      eventSourceRef.current?.close();
    };
  }, [connect]);
}
