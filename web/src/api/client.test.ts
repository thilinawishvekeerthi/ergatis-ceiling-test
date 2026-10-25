import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { server } from '../test/server';
import { createApiClient } from './client';
import { ApiError } from './errors';

const base = 'http://localhost';

describe('api client', () => {
  it('sends the anti-forgery header on PUT but not GET', async () => {
    const seen: Record<string, string | null> = {};
    server.use(
      http.get(`${base}/api/config/scales`, ({ request }) => {
        seen.get = request.headers.get('x-xsrf-token');
        return HttpResponse.json([]);
      }),
      http.put(`${base}/api/config/scales/s1/draft`, ({ request }) => {
        seen.put = request.headers.get('x-xsrf-token');
        return HttpResponse.json({});
      }),
    );
    const client = createApiClient({ baseUrl: base, getAntiForgeryToken: () => 'tok' });
    await client.listScales();
    await client.saveScaleDraft('s1', []);
    expect(seen.get).toBeNull();
    expect(seen.put).toBe('tok');
  });

  it('calls onUnauthenticated on 401 and throws ApiError', async () => {
    server.use(http.get(`${base}/api/config/scales`, () => new HttpResponse(null, { status: 401 })));
    const onUnauthenticated = vi.fn();
    const client = createApiClient({ baseUrl: base, onUnauthenticated });
    await expect(client.listScales()).rejects.toMatchObject({ status: 401, isUnauthenticated: true });
    expect(onUnauthenticated).toHaveBeenCalledOnce();
  });

  it('flags 403 and 409 and reads the problem detail', async () => {
    server.use(
      http.get(`${base}/api/config/scales`, () => HttpResponse.json({ detail: 'No access' }, { status: 403 })),
      http.get(`${base}/api/config/grids`, () => HttpResponse.json({ detail: 'Changed by someone' }, { status: 409 })),
    );
    const client = createApiClient({ baseUrl: base });
    const forbidden = await client.listScales().catch((e: unknown) => e);
    expect(forbidden).toBeInstanceOf(ApiError);
    expect(forbidden).toMatchObject({ isForbidden: true, message: 'No access' });
    const conflict = await client.listGrids().catch((e: unknown) => e);
    expect(conflict).toMatchObject({ isConflict: true, message: 'Changed by someone' });
  });

  it('returns undefined for 204', async () => {
    server.use(http.post(`${base}/api/config/scales/s1/publish`, () => new HttpResponse(null, { status: 204 })));
    const client = createApiClient({ baseUrl: base });
    await expect(client.publishScale('s1')).resolves.toBeUndefined();
  });
});
