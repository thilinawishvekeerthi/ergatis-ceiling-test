import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { server } from '../test/server';
import { createAntiForgeryTokenSource, createApiClient } from './client';
import { ApiError } from './errors';

const base = 'http://localhost';
const v1 = `${base}/api/v1/config`;

describe('api client', () => {
  it('sends the anti-forgery header on PUT but not GET', async () => {
    const seen: Record<string, string | null> = {};
    server.use(
      http.get(`${v1}/Scale`, ({ request }) => {
        seen.get = request.headers.get('x-xsrf-token');
        return HttpResponse.json([]);
      }),
      http.put(`${v1}/versions/s1/payload`, async ({ request }) => {
        seen.put = request.headers.get('x-xsrf-token');
        seen.body = JSON.stringify(await request.json());
        return HttpResponse.json({});
      }),
    );
    const client = createApiClient({ baseUrl: base, getAntiForgeryToken: () => 'tok' });
    await client.listConfigs('Scale');
    await client.saveScaleDraft('s1', { levels: [] });
    expect(seen.get).toBeNull();
    expect(seen.put).toBe('tok');
    expect(seen.body).toBe('{"payload":{"levels":[]}}');
  });

  it('calls onUnauthenticated on 401 and throws ApiError', async () => {
    server.use(http.get(`${v1}/Scale`, () => new HttpResponse(null, { status: 401 })));
    const onUnauthenticated = vi.fn();
    const client = createApiClient({ baseUrl: base, onUnauthenticated });
    await expect(client.listConfigs('Scale')).rejects.toMatchObject({ status: 401, isUnauthenticated: true });
    expect(onUnauthenticated).toHaveBeenCalledOnce();
  });

  it('flags 403 and 409 and reads the problem detail', async () => {
    server.use(
      http.get(`${v1}/Scale`, () => HttpResponse.json({ detail: 'No access' }, { status: 403 })),
      http.get(`${v1}/Grid`, () => HttpResponse.json({ detail: 'Changed by someone' }, { status: 409 })),
    );
    const client = createApiClient({ baseUrl: base });
    const forbidden = await client.listConfigs('Scale').catch((e: unknown) => e);
    expect(forbidden).toBeInstanceOf(ApiError);
    expect(forbidden).toMatchObject({ isForbidden: true, message: 'No access' });
    const conflict = await client.listConfigs('Grid').catch((e: unknown) => e);
    expect(conflict).toMatchObject({ isConflict: true, message: 'Changed by someone' });
  });

  it('returns undefined for 204', async () => {
    server.use(http.post(`${base}/bff/logout`, () => new HttpResponse(null, { status: 204 })));
    const client = createApiClient({ baseUrl: base });
    await expect(client.logout()).resolves.toBeUndefined();
  });

  it('getUser returns null without a session and does not redirect', async () => {
    server.use(http.get(`${base}/bff/user`, () => new HttpResponse(null, { status: 401 })));
    const onUnauthenticated = vi.fn();
    const client = createApiClient({ baseUrl: base, onUnauthenticated });
    await expect(client.getUser()).resolves.toBeNull();
    expect(onUnauthenticated).not.toHaveBeenCalled();
  });

  it('tells the host when the session changes so the anti-forgery token is dropped', async () => {
    server.use(http.post(`${base}/bff/fake-login`, () => new HttpResponse(null, { status: 204 })));
    const onSessionChanged = vi.fn();
    await createApiClient({ baseUrl: base, onSessionChanged }).fakeLogin('dev.employee');
    expect(onSessionChanged).toHaveBeenCalledOnce();
  });

  it('posts workflow actions with the comment', async () => {
    let body: unknown;
    server.use(
      http.post(`${base}/api/v1/workflows/w1/actions`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ id: 'w1' });
      }),
    );
    await createApiClient({ baseUrl: base }).act('w1', 'Reject', 'Too costly');
    expect(body).toEqual({ action: 'Reject', comment: 'Too costly' });
  });
});

describe('anti-forgery token source', () => {
  it('fetches once, and again after reset', async () => {
    let calls = 0;
    server.use(
      http.get(`${base}/bff/antiforgery`, () => {
        calls += 1;
        return HttpResponse.json({ requestToken: `t${calls}` });
      }),
    );
    const token = createAntiForgeryTokenSource(base);
    expect(await token()).toBe('t1');
    expect(await token()).toBe('t1');
    token.reset();
    expect(await token()).toBe('t2');
  });
});
