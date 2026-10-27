import { useMemo, useState, type ReactNode } from 'react';
import type { Role, Session } from '../api/types';
import { SessionContext } from './context';

/**
 * Fake session while the BFF is not ready. This is the only place that knows the session
 * is fake: swap it for a provider that reads GET /bff/user and nothing else changes.
 * Hiding screens here is a convenience; the server enforces access.
 */
export function SessionProvider({ initial, children }: { initial?: Partial<Session>; children: ReactNode }) {
  const [roles, setRoles] = useState<Role[]>(initial?.roles ?? ['employee']);
  const value = useMemo(
    () => ({
      session: { userId: initial?.userId ?? 'dev-user', displayName: initial?.displayName ?? 'Dev User', roles },
      setRoles,
    }),
    [initial?.userId, initial?.displayName, roles],
  );
  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}
