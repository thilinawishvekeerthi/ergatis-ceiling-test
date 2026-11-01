import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../test/server';
import { board, cycle, review } from '../features/assessment/fixtures';
import { createApiClient } from './client';
import { ApiError } from './errors';

const base = 'http://localhost';
const client = createApiClient({ baseUrl: base, getAntiForgeryToken: () => 'tok' });

describe('assessment api client', () => {
  it('reads cycles, reviews and the board from /api/v1/assessment', async () => {
    server.use(
      http.get(`${base}/api/v1/assessment/cycles`, () => HttpResponse.json([cycle()])),
      http.get(`${base}/api/v1/assessment/cycles/c1/reviews`, () => HttpResponse.json([review()])),
      http.get(`${base}/api/v1/assessment/cycles/c1/board`, () => HttpResponse.json(board())),
    );
    expect((await client.listCycles())[0]?.name).toBe('2026 annual');
    expect((await client.listReviews('c1'))[0]?.employeeName).toBe('Ada Lovelace');
    expect((await client.getBoard('c1')).people).toHaveLength(2);
  });

  it('sends a rating as PUT with the anti-forgery header', async () => {
    let seen: { token: string | null; body: unknown } | undefined;
    server.use(
      http.put(`${base}/api/v1/assessment/reviews/r1/rating`, async ({ request }) => {
        seen = { token: request.headers.get('x-xsrf-token'), body: await request.json() };
        return HttpResponse.json(review({ status: 'Rated' }));
      }),
    );
    await client.rate('r1', { performanceLevel: 4, potentialLevel: 2, comment: 'Solid' });
    expect(seen).toEqual({ token: 'tok', body: { performanceLevel: 4, potentialLevel: 2, comment: 'Solid' } });
  });

  it('sends a calibration with the cell and the reason', async () => {
    let body: unknown;
    server.use(
      http.post(`${base}/api/v1/assessment/reviews/r1/calibrate`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(review());
      }),
    );
    await client.calibrate('r1', { column: 2, row: 2 }, 'Led the migration');
    expect(body).toEqual({ column: 2, row: 2, reason: 'Led the migration' });
  });

  it.each([
    ['openCycle', 'open'],
    ['startCalibration', 'calibration'],
    ['closeCycle', 'close'],
  ] as const)('%s posts to /cycles/{id}/%s', async (method, path) => {
    let called = '';
    server.use(http.post(`${base}/api/v1/assessment/cycles/c1/${path}`, ({ request }) => {
      called = request.headers.get('x-xsrf-token') ?? '';
      return HttpResponse.json(cycle());
    }));
    await client[method]('c1');
    expect(called).toBe('tok');
  });

  it('turns a problem response into an ApiError with the detail', async () => {
    server.use(http.put(`${base}/api/v1/assessment/reviews/r1/rating`, () =>
      HttpResponse.json({ detail: 'Cannot rate in a cycle that is Calibration.' }, { status: 409 })));
    const error = await client.rate('r1', { performanceLevel: 4, potentialLevel: 2 }).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).isConflict).toBe(true);
    expect((error as ApiError).message).toBe('Cannot rate in a cycle that is Calibration.');
  });
});
