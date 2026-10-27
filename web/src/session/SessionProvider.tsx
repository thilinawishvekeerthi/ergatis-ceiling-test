import { useMemo, type ReactNode } from 'react';
import { Alert, CircularProgress } from '@mui/material';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from '../api/context';
import type { BffUser, Role, Session } from '../api/types';
import { labels } from '../labels';
import { SessionContext } from './context';

const portalRoles: Record<string, Role> = { EMPLOYEE: 'employee', MANAGER: 'manager', HR_ADMIN: 'hr-admin' };

/** Maps the server's role codes to the portals the app shows. Roles without a portal (for example TALENT_COMMITTEE) are ignored. */
export function toSession(user: BffUser): Session {
  return {
    userId: user.userId,
    displayName: user.userId,
    roles: [...new Set(user.roles.map((r) => portalRoles[r]).filter((r): r is Role => r !== undefined))],
  };
}

/**
 * Reads the signed-in user from GET /bff/user. The browser holds only the session cookie.
 * Hiding screens by role is a convenience: the server checks every call.
 */
export function SessionProvider({ children }: { children: ReactNode }) {
  const api = useApi();
  const qc = useQueryClient();
  const query = useQuery({ queryKey: ['session'], queryFn: () => api.getUser(), staleTime: Infinity });

  const value = useMemo(
    () => ({
      session: query.data ? toSession(query.data) : null,
      signIn: async (subject: string) => {
        await api.fakeLogin(subject);
        await qc.refetchQueries({ queryKey: ['session'] });
      },
      signOut: async () => {
        await api.logout();
        await qc.refetchQueries({ queryKey: ['session'] });
      },
    }),
    [api, qc, query.data],
  );

  if (query.isLoading) return <CircularProgress aria-label={labels.loading} />;
  if (query.isError) return <Alert severity="error">{labels.loadFailed}</Alert>;
  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}
