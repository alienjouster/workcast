import { type NextRequest, NextResponse } from 'next/server';

const API_INTERNAL = process.env.API_INTERNAL_URL ?? 'http://localhost:8080';

async function proxy(
  request: NextRequest,
  { params }: { params: { path: string[] } }
): Promise<NextResponse> {
  const path = params.path.join('/');
  const { search } = new URL(request.url);
  const target = `${API_INTERNAL}/api/${path}${search}`;

  let body: BodyInit | undefined;
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    const contentType = request.headers.get('Content-Type') ?? '';
    // Multipart uploads must be forwarded as-is (with their boundary).
    // All other requests are JSON and can be read as text.
    body = contentType.startsWith('multipart/form-data')
      ? await request.arrayBuffer()
      : await request.text();
  }

  const forwardedContentType = request.headers.get('Content-Type') ?? 'application/json';

  const upstream = await fetch(target, {
    method: request.method,
    headers: { 'Content-Type': forwardedContentType },
    body,
  });

  const contentType = upstream.headers.get('Content-Type') ?? 'application/json';
  const responseBody = upstream.status === 204 ? null : await upstream.text();

  return new NextResponse(responseBody, {
    status: upstream.status,
    headers: { 'Content-Type': contentType },
  });
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
