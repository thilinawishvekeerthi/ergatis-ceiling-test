import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, Button, Stack } from '@mui/material';
import type { Role } from '../api/types';
import { labels } from '../labels';
import { useSession } from '../session/context';

export const portalHome: Record<Role, string> = { employee: '/employee', manager: '/manager', 'hr-admin': '/admin' };

/** Development users seeded by the backend (appsettings.Development.json). */
const devUsers = [
  { subject: 'dev.employee', label: 'Employee (dev.employee)' },
  { subject: 'dev.manager', label: 'Manager of dev.employee (dev.manager)' },
  { subject: 'dev.admin', label: 'HR administrator (dev.admin)' },
];

/** Sign-in page, then the list of portals the user may enter. */
export function Landing() {
  const { session, signIn, signOut } = useSession();
  const [error, setError] = useState(false);

  if (session) {
    return (
      <main style={{ padding: 24 }}>
        <h1>{labels.appName}</h1>
        <p>{labels.switchRole} {session.displayName}</p>
        {session.roles.length === 0 && <Alert severity="info">{labels.noAccess}</Alert>}
        <ul>
          {session.roles.map((r) => (
            <li key={r}><Link to={portalHome[r]}>{labels.portals[r].title}</Link></li>
          ))}
        </ul>
        <Button onClick={() => void signOut()}>{labels.signOut}</Button>
      </main>
    );
  }

  // The fake sign-in is a development stand-in. A production build only offers the identity provider.
  if (!import.meta.env.DEV) {
    return (
      <main style={{ padding: 24 }}>
        <h1>{labels.appName}</h1>
        <p><a href="/bff/login">{labels.signIn}</a></p>
      </main>
    );
  }

  return (
    <main style={{ padding: 24 }}>
      <h1>{labels.appName}</h1>
      <p>Development sign-in. Choose who you are.</p>
      {error && <Alert severity="error">{labels.signInFailed}</Alert>}
      <Stack spacing={1} sx={{ maxWidth: 360 }}>
        {devUsers.map((u) => (
          <Button key={u.subject} variant="outlined"
            onClick={() => signIn(u.subject).then(() => setError(false), () => setError(true))}>
            Sign in as {u.label}
          </Button>
        ))}
      </Stack>
    </main>
  );
}
