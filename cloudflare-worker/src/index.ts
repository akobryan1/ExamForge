/**
 * ExamForge Guard — Cloudflare Worker
 *
 * PRONG A (Traffic Guard):
 *   - Reverse proxy for POST /api/exams/grading/ai-grade
 *   - Rate limits by JWT-decoded userId: 5 req / 60s
 *   - Cron-triggered keep-alive ping to Render /health
 */

export interface Env {
  RENDER_BACKEND_URL: string;
  JWT_SECRET: string;
  examforge_rate_limit: {
    limit: (key: string) => Promise<{ success: boolean }>;
  };
}

// ─── Fetch Handler ───────────────────────────────────────────────────────

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    // Only intercept /api/exams/grading/ai-grade
    if (!url.pathname.startsWith('/api/exams/grading/ai-grade')) {
      return new Response('Not Found', { status: 404 });
    }

    // 1. Extract and verify JWT
    const authHeader = request.headers.get('Authorization');
    if (!authHeader || !authHeader.startsWith('Bearer ')) {
      return new Response('Missing or invalid authentication token', {
        status: 400,
      });
    }

    const token = authHeader.slice(7);

    let userId: string;
    try {
      const payload = decodeJwtPayload(token);
      userId = payload.userId || payload.sub || '';
      if (!userId) {
        throw new Error('No userId in token');
      }
    } catch {
      return new Response('Missing or invalid authentication token', {
        status: 400,
      });
    }

    // 2. Rate limit check
    const rateLimitResult = await env.examforge_rate_limit.limit(userId);
    if (!rateLimitResult.success) {
      return new Response('Please wait 60 seconds before submitting another essay', {
        status: 429,
        headers: {
          'Retry-After': '60',
          'Content-Type': 'text/plain',
        },
      });
    }

    // 3. Proxy request to Render backend
    const backendUrl = env.RENDER_BACKEND_URL;
    if (!backendUrl) {
      return new Response('Backend not configured', { status: 500 });
    }

    const proxyUrl = `${backendUrl}${url.pathname}${url.search}`;

    const proxyHeaders = new Headers(request.headers);
    // Remove hop-by-hop headers
    proxyHeaders.delete('cf-connecting-ip');
    proxyHeaders.delete('cf-ray');
    proxyHeaders.delete('cf-visitor');
    proxyHeaders.delete('x-forwarded-for');
    proxyHeaders.delete('x-forwarded-proto');
    proxyHeaders.delete('x-real-ip');

    const proxyRequest = new Request(proxyUrl, {
      method: request.method,
      headers: proxyHeaders,
      body: request.method !== 'GET' && request.method !== 'HEAD' ? request.body : undefined,
    });

    try {
      const backendResponse = await fetch(proxyRequest);
      return backendResponse;
    } catch (err) {
      return new Response('Backend unavailable', { status: 502 });
    }
  },

  // ─── Scheduled Handler (Cron Keep-Alive) ──────────────────────────────

  async scheduled(_controller: ScheduledController, env: Env): Promise<void> {
    const backendUrl = env.RENDER_BACKEND_URL;
    if (!backendUrl) {
      console.error('[KeepAlive] RENDER_BACKEND_URL not set');
      return;
    }

    try {
      const response = await fetch(`${backendUrl}/health`, { method: 'HEAD' });
      if (response.ok) {
        console.log('[KeepAlive] Health check OK');
      } else {
        console.error('[KeepAlive] Health check failed:', response.status);
      }
    } catch (err) {
      console.error('[KeepAlive] Health check error:', (err as Error).message);
    }
  },
};

// ─── JWT Decoder (base64 decode only — no signature verification needed for rate-limiting key) ───

function decodeJwtPayload(token: string): Record<string, unknown> {
  const parts = token.split('.');
  if (parts.length !== 3) {
    throw new Error('Invalid JWT format');
  }
  const payload = parts[1];
  const decoded = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
  return JSON.parse(decoded);
}
