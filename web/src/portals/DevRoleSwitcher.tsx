import { Link } from 'react-router-dom';
import type { Role } from '../api/types';
import { useSession } from '../session/context';

const home: Record<Role, string> = { employee: '/employee', manager: '/manager', 'hr-admin': '/admin' };

/** Landing page for the fake session: pick a role to enter its portal. */
export function Landing() {
  const { session, setRoles } = useSession();
  return (
    <main style={{ padding: 24 }}>
      <h1>Talent</h1>
      <p>Development session. Choose who you are signed in as.</p>
      <ul>
        {(Object.keys(home) as Role[]).map((r) => (
          <li key={r}>
            <Link to={home[r]} onClick={() => setRoles([r])}>Continue as {r}</Link>
            {session.roles.includes(r) ? ' (current)' : ''}
          </li>
        ))}
      </ul>
    </main>
  );
}
