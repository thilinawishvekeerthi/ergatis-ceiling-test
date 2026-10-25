import { ApiError } from './errors';
import type { ConfigSummary, GridDefinition, GridDetail, ScaleDetail, ScaleLevel } from './types';

export interface ApiClientOptions {
  baseUrl?: string;
  /** Called on 401 so the host can redirect to /bff/login. */
  onUnauthenticated?: () => void;
  getAntiForgeryToken?: () => string | undefined;
}

type Json = Record<string, unknown> | unknown[];

/** Typed client for the same-origin BFF. Cookies carry the session; no tokens in JS. */
export function createApiClient(options: ApiClientOptions = {}) {
  const base = options.baseUrl ?? '';

  async function request<T>(method: string, path: string, body?: Json): Promise<T> {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (method !== 'GET') {
      const token = options.getAntiForgeryToken?.();
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

  return {
    listScales: () => request<ConfigSummary[]>('GET', '/api/config/scales'),
    getScale: (id: string) => request<ScaleDetail>('GET', `/api/config/scales/${id}`),
    saveScaleDraft: (id: string, levels: ScaleLevel[]) =>
      request<ScaleDetail>('PUT', `/api/config/scales/${id}/draft`, { levels }),
    publishScale: (id: string) => request<ScaleDetail>('POST', `/api/config/scales/${id}/publish`),
    listGrids: () => request<ConfigSummary[]>('GET', '/api/config/grids'),
    getGrid: (id: string) => request<GridDetail>('GET', `/api/config/grids/${id}`),
    saveGridDraft: (id: string, grid: GridDefinition) =>
      request<GridDetail>('PUT', `/api/config/grids/${id}/draft`, { ...grid }),
    publishGrid: (id: string) => request<GridDetail>('POST', `/api/config/grids/${id}/publish`),
  };
}

export type ApiClient = ReturnType<typeof createApiClient>;
