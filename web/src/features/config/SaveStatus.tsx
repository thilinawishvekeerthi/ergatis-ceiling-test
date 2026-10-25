import { Alert, Button } from '@mui/material';
import { ApiError } from '../../api/errors';
import { labels } from '../../labels';

export function SaveStatus({ error, saved, onReload }: { error: unknown; saved: boolean; onReload: () => void }) {
  if (error instanceof ApiError) {
    if (error.isConflict)
      return (
        <Alert severity="warning" action={<Button onClick={onReload}>{labels.reload}</Button>}>
          {labels.conflict}
        </Alert>
      );
    if (error.isForbidden) return <Alert severity="error">{labels.noAccess}</Alert>;
    return <Alert severity="error">{error.message}</Alert>;
  }
  if (error) return <Alert severity="error">{labels.loadFailed}</Alert>;
  if (saved) return <Alert severity="success">{labels.saved}</Alert>;
  return null;
}
