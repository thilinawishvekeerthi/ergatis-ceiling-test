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
      http.get(`${base}/api/v1/config/Scale`, ({ request }) => {
        seen.get = request.headers.get('x-xsrf-token');
        return HttpResponse.json([]);
      }),
      http.put(`${base}/api/v1/config/versions/s1/payload`, ({ request }) => {
        seen.put = request.headers.get('x-xsrf-token');
        return HttpResponse.json({ id: 's1', code: 'PERF', versionNo: 1, status: 'Draft', payload: { levels: [] } });
      }),
    );
    const client = createApiClient({ baseUrl: base, getAntiForgeryToken: () => 'tok' });
    await client.listScales();
    await client.saveScaleDraft('s1', []);
    expect(seen.get).toBeNull();
    expect(seen.put).toBe('tok');
  });

  it('calls onUnauthenticated on 401 and throws ApiError', async () => {
    server.use(http.get(`${base}/api/v1/config/Scale`, () => new HttpResponse(null, { status: 401 })));
    const onUnauthenticated = vi.fn();
    const client = createApiClient({ baseUrl: base, onUnauthenticated });
    await expect(client.listScales()).rejects.toMatchObject({ status: 401, isUnauthenticated: true });
    expect(onUnauthenticated).toHaveBeenCalledOnce();
  });

  it('flags 403 and 409 and reads the problem detail', async () => {
    server.use(
      http.get(`${base}/api/v1/config/Scale`, () => HttpResponse.json({ detail: 'No access' }, { status: 403 })),
      http.get(`${base}/api/v1/config/Grid`, () => HttpResponse.json({ detail: 'Changed by someone' }, { status: 409 })),
    );
    const client = createApiClient({ baseUrl: base });
    const forbidden = await client.listScales().catch((e: unknown) => e);
    expect(forbidden).toBeInstanceOf(ApiError);
    expect(forbidden).toMatchObject({ isForbidden: true, message: 'No access' });
    const conflict = await client.listGrids().catch((e: unknown) => e);
    expect(conflict).toMatchObject({ isConflict: true, message: 'Changed by someone' });
  });

  it('maps a published version response to a scale', async () => {
    server.use(
      http.post(`${base}/api/v1/config/versions/s1/publish`, () =>
        HttpResponse.json({ id: 's1', code: 'PERF', versionNo: 2, status: 'Published', payload: { levels: [] } }),
      ),
    );
    const client = createApiClient({ baseUrl: base });
    await expect(client.publishScale('s1')).resolves.toEqual({ id: 's1', name: 'PERF', versionNo: 2, status: 'Published', levels: [] });
  });

  it('sends the draft body wrapped in payload', async () => {
    let body: unknown;
    server.use(
      http.put(`${base}/api/v1/config/versions/g1/payload`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ id: 'g1', code: 'NB', versionNo: 1, status: 'Draft', payload: { columnBounds: [], rowBounds: [], cells: [] } });
      }),
    );
    const client = createApiClient({ baseUrl: base });
    const grid = { columnBounds: [], rowBounds: [], cells: [] };
    const saved = await client.saveGridDraft('g1', grid);
    expect(body).toEqual({ payload: grid });
    expect(saved.grid).toEqual(grid);
  });
});
