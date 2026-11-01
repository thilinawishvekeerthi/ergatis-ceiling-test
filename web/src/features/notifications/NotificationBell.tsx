import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Badge, Box, Button, CircularProgress, IconButton, List, ListItem, ListItemButton, ListItemText, Popover, Typography } from '@mui/material';
import type { AppNotification } from '../../api/types';
import { labels } from '../../labels';
import { useInbox, useMarkAllRead, useMarkRead, useUnreadCount } from './hooks';

/** The counts above this show as "99+" so the badge stays small. */
const MAX_BADGE = 99;

function BellIcon() {
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" aria-hidden="true" focusable="false" fill="currentColor">
      <path d="M12 22a2.5 2.5 0 0 0 2.45-2h-4.9A2.5 2.5 0 0 0 12 22Zm7-6V11a7 7 0 0 0-5.5-6.83V3.5a1.5 1.5 0 0 0-3 0v.67A7 7 0 0 0 5 11v5l-2 2v1h18v-1l-2-2Z" />
    </svg>
  );
}

/** The bell in the portal header: the unread count, and a list of the latest alerts that opens on click. */
export function NotificationBell() {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const unread = useUnreadCount();
  const inbox = useInbox(anchor !== null);
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();
  const navigate = useNavigate();
  const count = unread.data ?? 0;

  const open = (n: AppNotification) => {
    if (!n.isRead) markRead.mutate(n.id);
    setAnchor(null);
    if (n.linkUrl) navigate(n.linkUrl);
  };

  return (
    <>
      <IconButton color="inherit" aria-label={labels.notifications.bell(count)} aria-haspopup="dialog" aria-expanded={anchor !== null} onClick={(e) => setAnchor(e.currentTarget)}>
        <Badge badgeContent={count} max={MAX_BADGE} color="error" invisible={count === 0}>
          <BellIcon />
        </Badge>
      </IconButton>
      <Popover open={anchor !== null} anchorEl={anchor} onClose={() => setAnchor(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }} transformOrigin={{ vertical: 'top', horizontal: 'right' }}>
        <Box role="dialog" aria-label={labels.notifications.title} sx={{ width: 360, maxWidth: '90vw' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 2, pt: 1.5 }}>
            <Typography variant="subtitle1" component="h2">{labels.notifications.title}</Typography>
            <Button size="small" disabled={count === 0 || markAll.isPending} onClick={() => markAll.mutate()}>{labels.notifications.markAllRead}</Button>
          </Box>
          {inbox.isLoading && <Box sx={{ p: 2 }}><CircularProgress size={24} aria-label={labels.loading} /></Box>}
          {inbox.isError && <Alert severity="error" sx={{ m: 2 }}>{labels.notifications.loadFailed}</Alert>}
          {inbox.data && inbox.data.items.length === 0 && <Typography sx={{ p: 2 }}>{labels.notifications.empty}</Typography>}
          {inbox.data && inbox.data.items.length > 0 && (
            <List dense disablePadding>
              {inbox.data.items.map((n) => (
                <ListItem key={n.id} disablePadding divider>
                  <ListItemButton onClick={() => open(n)} aria-label={`${n.isRead ? '' : `${labels.notifications.unread}: `}${n.title}`}>
                    <ListItemText
                      primary={n.title}
                      secondary={<>{n.body}<br />{new Date(n.createdAt).toLocaleString()}</>}
                      slotProps={{ primary: { sx: { fontWeight: n.isRead ? 400 : 700 } } }}
                    />
                  </ListItemButton>
                </ListItem>
              ))}
            </List>
          )}
        </Box>
      </Popover>
    </>
  );
}
