import { useNavigate } from 'react-router-dom';
import { Alert, Button, Stack } from '@mui/material';
import { usePublish, useNewVersion } from '../../api/hooks';
import type { ConfigType, ConfigVersion } from '../../api/types';
import { labels } from '../../labels';
import { SaveStatus } from './SaveStatus';

/**
 * The version lifecycle buttons (ADR-02). A draft can be published once it is saved.
 * A published or locked version is never edited: a change starts a new draft copied from it.
 */
export function VersionActions({
  type,
  version,
  basePath,
  unsaved,
}: {
  type: ConfigType;
  version: ConfigVersion<unknown>;
  basePath: string;
  unsaved: boolean;
}) {
  const navigate = useNavigate();
  const publish = usePublish(type, version.id);
  const newVersion = useNewVersion(type);

  return (
    <Stack spacing={1} sx={{ my: 2 }}>
      <div>
        {version.status === 'Draft' ? (
          <Button variant="outlined" disabled={unsaved || publish.isPending} onClick={() => publish.mutate()}>
            {labels.publish}
          </Button>
        ) : (
          <Button variant="outlined" disabled={newVersion.isPending}
            onClick={() => newVersion.mutate(version.code, { onSuccess: (draft) => navigate(`${basePath}/${draft.id}`) })}>
            {labels.newVersion}
          </Button>
        )}
      </div>
      {publish.isSuccess && <Alert severity="success">{labels.published}</Alert>}
      <SaveStatus error={publish.error ?? newVersion.error} saved={false} onReload={() => publish.reset()} />
    </Stack>
  );
}
