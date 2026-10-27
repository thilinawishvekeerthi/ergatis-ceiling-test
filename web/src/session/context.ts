import { createContext, useContext } from 'react';
import type { Role, Session } from '../api/types';

export interface SessionState {
  session: Session;
  /** Fake-session only: lets a developer try each portal. Replaced by BFF login. */
  setRoles: (roles: Role[]) => void;
}

export const SessionContext = createContext<SessionState | null>(null);

export function useSession(): SessionState {
  const value = useContext(SessionContext);
  if (!value) throw new Error('useSession must be used inside SessionProvider');
  return value;
}
