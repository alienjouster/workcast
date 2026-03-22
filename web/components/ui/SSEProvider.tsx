'use client';

import { useSSE } from '@/lib/hooks/useSSE';

/** Mounts the SSE connection for the lifetime of the app. Renders nothing. */
export function SSEProvider() {
  useSSE();
  return null;
}
