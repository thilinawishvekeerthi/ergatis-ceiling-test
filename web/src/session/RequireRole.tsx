import type { ReactNode } from 'react';
import { Alert } from '@mui/material';
import type { Role } from '../api/types';
import { labels } from '../labels';
import { useSession } from './context';

export function RequireRole({ role, children }: { role: Role; children: ReactNode }) {
  const { session } = useSession();
  if (!session.roles.includes(role)) return <Alert severity="warning" role="alert">{labels.noAccess}</Alert>;
  return <>{children}</>;
}
