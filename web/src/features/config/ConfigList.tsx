import { Link as RouterLink } from 'react-router-dom';
import { Alert, Chip, CircularProgress, List, ListItem, ListItemButton, ListItemText } from '@mui/material';
import type { ConfigSummary } from '../../api/types';
import { labels } from '../../labels';

export function ConfigList({
  items,
  isLoading,
  isError,
  empty,
  basePath,
}: {
  items: ConfigSummary[] | undefined;
  isLoading: boolean;
  isError: boolean;
  empty: string;
  basePath: string;
}) {
  if (isLoading) return <CircularProgress aria-label={labels.loading} />;
  if (isError) return <Alert severity="error">{labels.loadFailed}</Alert>;
  if (!items?.length) return <p>{empty}</p>;
  return (
    <List>
      {items.map((item) => (
        <ListItem key={item.definitionId} disablePadding secondaryAction={<Chip size="small" label={item.status} />}>
          <ListItemButton component={RouterLink} to={`${basePath}/${item.latestVersionId}`}>
            <ListItemText primary={item.name} secondary={`${item.code}, version ${item.versionNo}`} />
          </ListItemButton>
        </ListItem>
      ))}
    </List>
  );
}
