import { ApiError } from './errors';
import type {
  BffUser,
  ConfigSummary,
  ConfigType,
  GridDefinition,
  GridDetail,
  InboxItem,
  ScaleDetail,
  ScalePayload,
  StartWorkflow,
  WorkflowInstance,
} from './types';

export interface ApiClientOptions {
  baseUrl?: string;
  /** Called on 401 so the host can redirect to /bff/login. */
  onUnauthenticated?: () => void;
  getAntiForgeryToken?: () => string | undefined | Promise<string | undefined>;
  /** Called after sign-in and sign-out: the anti-forgery token belongs to one user. */
  onSessionChanged?: () => void;
}

type Json = Record<string, unknown> | unknown[];

/**
 * Fetches the anti-forgery token from the BFF once and reuses it. The BFF rejects state-changing calls without it.
 * A failed fetch is not cached, so the next write tries again.
 */
export function createAntiForgeryTokenSource(baseUrl = ''): (() => Promise<string | undefined>) & { reset: () => void } {
  let pending: Promise<string | undefined> | undefined;
  const source = () => {
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
  return Object.assign(source, { reset: () => (pending = undefined) });
}

/** Typed client for the same-origin BFF. Cookies carry the session; no tokens in JS. */
export function createApiClient(options: ApiClientOptions = {}) {
  const base = options.baseUrl ?? '';

  async function request<T>(method: string, path: string, body?: Json, quiet401 = false): Promise<T> {
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
      if (response.status === 401 && !quiet401) options.onUnauthenticated?.();
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

  const config = (type: ConfigType) => `/api/v1/config/${type}`;
  const version = (id: string) => `/api/v1/config/versions/${encodeURIComponent(id)}`;

  return {
    /** The signed-in user, or null when there is no session. Never redirects. */
    getUser: async (): Promise<BffUser | null> => {
      try {
        return await request<BffUser>('GET', '/bff/user', undefined, true);
      } catch (e) {
        if (e instanceof ApiError && e.isUnauthenticated) return null;
        throw e;
      }
    },
    /** Development login (Authentication:Mode Fake). Production signs in through /bff/login. */
    fakeLogin: async (subject: string) => {
      await request<void>('POST', '/bff/fake-login', { subject });
      options.onSessionChanged?.();
    },
    logout: async () => {
      await request<void>('POST', '/bff/logout');
      options.onSessionChanged?.();
    },

    listConfigs: (type: ConfigType) => request<ConfigSummary[]>('GET', config(type)),
    getScale: (id: string) => request<ScaleDetail>('GET', version(id)),
    getGrid: (id: string) => request<GridDetail>('GET', version(id)),
    saveScaleDraft: (id: string, payload: ScalePayload) => request<ScaleDetail>('PUT', `${version(id)}/payload`, { payload: { ...payload } }),
    saveGridDraft: (id: string, grid: GridDefinition) => request<GridDetail>('PUT', `${version(id)}/payload`, { payload: { ...grid } }),
    publish: (id: string) => request<ScaleDetail | GridDetail>('POST', `${version(id)}/publish`),
    /** Copies the newest version of a published config into a new draft. */
    newVersion: (type: ConfigType, code: string) =>
      request<ScaleDetail | GridDetail>('POST', `${config(type)}/${encodeURIComponent(code)}/versions`),

    myWorkflows: () => request<WorkflowInstance[]>('GET', '/api/v1/workflows/mine'),
    inbox: () => request<InboxItem[]>('GET', '/api/v1/workflows/inbox'),
    getWorkflow: (id: string) => request<WorkflowInstance>('GET', `/api/v1/workflows/${encodeURIComponent(id)}`),
    startWorkflow: (start: StartWorkflow) => request<WorkflowInstance>('POST', '/api/v1/workflows', { ...start }),
    act: (id: string, action: string, comment?: string) =>
      request<WorkflowInstance>('POST', `/api/v1/workflows/${encodeURIComponent(id)}/actions`, { action, comment: comment ?? null }),
  };
}

export type ApiClient = ReturnType<typeof createApiClient>;
