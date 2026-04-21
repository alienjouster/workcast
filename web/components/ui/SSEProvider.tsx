'use client';

import { useSSE } from '@/lib/hooks/useSSE';
import { useNotifications } from '@/lib/hooks/useNotifications';

/** Mounts the SSE connection and notification listener for the lifetime of the app. Renders nothing. */
export function SSEProvider() {
  useSSE();
  useNotifications();
  return null;
}
