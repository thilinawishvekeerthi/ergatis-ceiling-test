import { useNavigate } from 'react-router-dom';
import type { Role } from '../api/types';
import { useSession } from '../session/context';

const home: Record<Role, string> = { employee: '/employee', manager: '/manager', 'hr-admin': '/admin' };
/** The fake-login accounts the development host seeds (appsettings.Development.json). */
const devSubject: Record<Role, string> = { employee: 'dev.employee', manager: 'dev.manager', 'hr-admin': 'dev.admin' };

/** Signs in through the development-only fake login so the API sees a real session. */
async function devLogin(role: Role) {
  const response = await fetch('/bff/fake-login', {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ subject: devSubject[role] }),
  });
  return response.ok;
}

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
            <a
              href={home[r]}
              onClick={(e) => {
                e.preventDefault();
                // Without the host running there is no session to create, but the screens still open.
                void devLogin(r).catch(() => false).finally(() => {
                  setRoles([r]);
                  navigate(home[r]);
                });
              }}
            >
              Continue as {r}
            </a>
            {session.roles.includes(r) ? ' (current)' : ''}
          </li>
        ))}
      </ul>
    </main>
  );
}
