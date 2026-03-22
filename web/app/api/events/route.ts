import { type NextRequest } from 'next/server';

// Always run this route dynamically — never allow Next.js to cache or prerender it.
export const dynamic = 'force-dynamic';

const API_INTERNAL = process.env.API_INTERNAL_URL ?? 'http://localhost:8080';

/**
 * Dedicated SSE proxy route — passes the backend's text/event-stream body through
 * to the browser without buffering. Cannot go through the generic catch-all proxy
 * because that awaits the full response body before forwarding.
 *
 * Uses a TransformStream to pump each chunk from the upstream fetch into the
 * client response as soon as it arrives, avoiding Node.js/undici response
 * body buffering that would otherwise delay SSE frames.
 */
export async function GET(request: NextRequest) {
  const upstream = await fetch(`${API_INTERNAL}/api/events`, {
    headers: { Accept: 'text/event-stream' },
    signal: request.signal,
    cache: 'no-store',
  });

  if (!upstream.body) {
    return new Response('upstream returned no body', { status: 502 });
  }

  const { readable, writable } = new TransformStream();
  const writer = writable.getWriter();
  const reader = upstream.body.getReader();

  // Pump each chunk immediately as it arrives from the backend, bypassing any
  // internal response-body buffering in the Node.js fetch implementation.
  (async () => {
    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) { await writer.close(); break; }
        await writer.write(value);
      }
    } catch {
      await writer.abort().catch(() => {});
    }
  })();

  return new Response(readable, {
    headers: {
      'Content-Type':      'text/event-stream',
      'Cache-Control':     'no-cache',
      'Connection':        'keep-alive',
      'X-Accel-Buffering': 'no',
    },
  });
}
