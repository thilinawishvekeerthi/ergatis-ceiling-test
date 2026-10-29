import { ApiError } from './errors';
import type { ConfigSummary, ConfigVersionStatus, GridDefinition, GridDetail, ScaleDetail, ScaleLevel } from './types';

export interface ApiClientOptions {
  baseUrl?: string;
  /** Called on 401 so the host can redirect to /bff/login. */
  onUnauthenticated?: () => void;
  getAntiForgeryToken?: () => string | undefined | Promise<string | undefined>;
}

type Json = Record<string, unknown> | unknown[];

/**
 * Fetches the anti-forgery token from the BFF once and reuses it. The BFF rejects state-changing calls without it.
 * A failed fetch is not cached, so the next write tries again.
 */
export function createAntiForgeryTokenSource(baseUrl = ''): () => Promise<string | undefined> {
  let pending: Promise<string | undefined> | undefined;
  return () => {
    pending ??= fetch(`${baseUrl}/bff/antiforgery`, { credentials: 'same-origin', headers: { Accept: 'application/json' } })
      .then(async (response) => (response.ok ? ((await response.json()) as { requestToken?: string }).requestToken : undefined))
      .then((token) => {
        if (token === undefined) pending = undefined;
        return token;
      })
      .catch(() => {
        pending = undefined;
        return undefined;
      });
    return pending;
  };
}

/** Typed client for the same-origin BFF. Cookies carry the session; no tokens in JS. */
export function createApiClient(options: ApiClientOptions = {}) {
  const base = options.baseUrl ?? '';

  async function request<T>(method: string, path: string, body?: Json): Promise<T> {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (method !== 'GET') {
      const token = await options.getAntiForgeryToken?.();
      if (token) headers['X-XSRF-TOKEN'] = token;
    }
    const response = await fetch(`${base}${path}`, {
      method,
      headers,
      credentials: 'same-origin',
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) {
      if (response.status === 401) options.onUnauthenticated?.();
      let detail = response.statusText;
      try {
        const problem = (await response.json()) as { detail?: string; title?: string };
        detail = problem.detail ?? problem.title ?? detail;
      } catch {
        /* body was not ProblemDetails */
      }
      throw new ApiError(response.status, detail);
    }
    if (response.status === 204) return undefined as T;
    return (await response.json()) as T;
  }

  const configRoot = '/api/v1/config';

  /** What GET /versions/{id} and the publish/payload calls return. The payload is the scale or grid body. */
  interface VersionResponse<P> {
    id: string;
    code: string;
    versionNo: number;
    status: ConfigVersionStatus;
    payload: P;
  }

  /** The id used by the web is the version id. The version response has no display name, so the code stands in. */
  function summary<P>(v: VersionResponse<P>): ConfigSummary {
    return { id: v.id, name: v.code, versionNo: v.versionNo, status: v.status };
  }

  const toScale = (v: VersionResponse<{ levels: ScaleLevel[] }>): ScaleDetail => ({ ...summary(v), levels: v.payload.levels });
  const toGrid = (v: VersionResponse<GridDefinition>): GridDetail => ({ ...summary(v), grid: v.payload });

  return {
    listScales: () => request<ConfigSummary[]>('GET', `${configRoot}/Scale`),
    getScale: async (id: string) => toScale(await request('GET', `${configRoot}/versions/${id}`)),
    saveScaleDraft: async (id: string, levels: ScaleLevel[]) =>
      toScale(await request('PUT', `${configRoot}/versions/${id}/payload`, { payload: { levels } })),
    publishScale: async (id: string) => toScale(await request('POST', `${configRoot}/versions/${id}/publish`)),
    listGrids: () => request<ConfigSummary[]>('GET', `${configRoot}/Grid`),
    getGrid: async (id: string) => toGrid(await request('GET', `${configRoot}/versions/${id}`)),
    saveGridDraft: async (id: string, grid: GridDefinition) =>
      toGrid(await request('PUT', `${configRoot}/versions/${id}/payload`, { payload: { ...grid } })),
    publishGrid: async (id: string) => toGrid(await request('POST', `${configRoot}/versions/${id}/publish`)),
  };
}

export type ApiClient = ReturnType<typeof createApiClient>;
