import { createContext, useContext } from 'react';
import type { Session } from '../api/types';

export interface SessionState {
  /** Null when nobody is signed in. */
  session: Session | null;
  /** Development sign-in through the BFF fake login. Production uses /bff/login. */
  signIn: (subject: string) => Promise<void>;
  signOut: () => Promise<void>;
}

export const SessionContext = createContext<SessionState | null>(null);

export function useSession(): SessionState {
  const value = useContext(SessionContext);
  if (!value) throw new Error('useSession must be used inside SessionProvider');
  return value;
}
