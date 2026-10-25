import { NavLink, Outlet } from 'react-router-dom';
import { AppBar, Box, Toolbar, Typography } from '@mui/material';
import type { Role } from '../api/types';
import { labels } from '../labels';
import { RequireRole } from '../session/RequireRole';
import { useSession } from '../session/context';

export interface PortalLink {
  to: string;
  label: string;
}

export function PortalLayout({ role, links }: { role: Role; links: PortalLink[] }) {
  const { session } = useSession();
  return (
    <RequireRole role={role}>
      <AppBar position="static" color="default" elevation={0}>
        <Toolbar>
          <Typography variant="h6" component="h1" sx={{ mr: 4 }}>{labels.portals[role].title}</Typography>
          <nav aria-label={`${labels.portals[role].title} navigation`}>
            {links.map((l) => (
              <NavLink key={l.to} to={l.to} style={{ marginRight: 16 }}>{l.label}</NavLink>
            ))}
          </nav>
          <Box sx={{ flexGrow: 1 }} />
          <Typography variant="body2">{labels.switchRole} {session.displayName}</Typography>
        </Toolbar>
      </AppBar>
      <Box component="main" sx={{ p: 3 }}><Outlet /></Box>
    </RequireRole>
  );
}
