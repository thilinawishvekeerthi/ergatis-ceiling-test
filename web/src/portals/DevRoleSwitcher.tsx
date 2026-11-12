import { useNavigate } from 'react-router-dom';
import type { Role } from '../api/types';
import { useSession } from '../session/context';

const home: Record<Role, string> = { employee: '/employee', manager: '/manager', 'hr-admin': '/admin' };

/** Users seeded by the host in Development (appsettings.Development.json, Security:Seed). */
const devSubject: Record<Role, string> = { employee: 'dev.employee', manager: 'dev.manager', 'hr-admin': 'dev.admin' };

/** Landing page for the fake session: pick a role to enter its portal. */
export function Landing() {
  const { session, setRoles } = useSession();
  const navigate = useNavigate();
  // The role picker is a development stand-in for login. A production build must never offer it.
  if (!import.meta.env.DEV) {
    return (
      <main style={{ padding: 24 }}>
        <h1>Talent</h1>
        <p><a href="/bff/login">Sign in</a></p>
      </main>
    );
  }
  return (
    <main style={{ padding: 24 }}>
      <h1>Talent</h1>
      <p>Development session. Choose who you are signed in as.</p>
      <ul>
        {(Object.keys(home) as Role[]).map((r) => (
          <li key={r}>
            <button
              type="button"
              onClick={async () => {
                // The host's fake login (Development only) sets the session cookie, so API calls run as this user.
                await fetch('/bff/fake-login', {
                  method: 'POST',
                  credentials: 'same-origin',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ subject: devSubject[r] }),
                });
                setRoles([r]);
                navigate(home[r]);
              }}
            >
              Continue as {r} ({devSubject[r]})
            </button>
            {session.roles.includes(r) ? ' (current)' : ''}
          </li>
        ))}
      </ul>
    </main>
  );
}
