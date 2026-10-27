import { useState } from 'react';
import { Alert, Button, Card, CardContent, CircularProgress, Stack, TextField, Typography } from '@mui/material';
import { useAct, useInbox } from '../../api/hooks';
import type { InboxItem } from '../../api/types';
import { labels } from '../../labels';
import { SaveStatus } from '../config/SaveStatus';

/** Manager and HR: approval tasks waiting for the signed-in user, oldest due date first. */
export function Approvals() {
  const inbox = useInbox();
  return (
    <section>
      <h2>{labels.nav.approvals}</h2>
      {inbox.isLoading && <CircularProgress aria-label={labels.loading} />}
      {inbox.isError && <Alert severity="error">{labels.loadFailed}</Alert>}
      {inbox.isSuccess && inbox.data.length === 0 && <p>{labels.emptyInbox}</p>}
      <Stack spacing={2}>
        {inbox.data?.map((item) => <Task key={item.task.id} item={item} />)}
      </Stack>
    </section>
  );
}

function Task({ item }: { item: InboxItem }) {
  const act = useAct();
  const [comment, setComment] = useState('');
  const [needComment, setNeedComment] = useState(false);
  const title = item.context.title ?? `${item.subjectType} ${item.subjectId}`;

  const reject = () => {
    if (!comment.trim()) {
      setNeedComment(true);
      return;
    }
    act.mutate({ id: item.task.instanceId, action: 'Reject', comment });
  };

  return (
    <Card variant="outlined" component="article" aria-label={title}>
      <CardContent>
        <Typography variant="h6" component="h3">{title}</Typography>
        <Typography variant="body2">
          {item.definitionCode}, step {item.task.state}, from {item.startedBy}
          {item.context.estimatedCost !== undefined && `, estimated cost ${item.context.estimatedCost}`}
        </Typography>
        {item.task.dueAt && <Typography variant="body2">Due {new Date(item.task.dueAt).toLocaleString()}</Typography>}
        <Stack direction="row" spacing={1} sx={{ mt: 1, alignItems: 'flex-start' }}>
          <TextField size="small" label={labels.comment} value={comment}
            onChange={(e) => { setComment(e.target.value); setNeedComment(false); }}
            error={needComment} helperText={needComment ? labels.commentRequired : undefined} />
          <Button variant="contained" disabled={act.isPending}
            onClick={() => act.mutate({ id: item.task.instanceId, action: 'Approve', comment })}>
            {labels.approve}
          </Button>
          <Button color="error" disabled={act.isPending} onClick={reject}>{labels.reject}</Button>
        </Stack>
        <SaveStatus error={act.error} saved={false} onReload={() => act.reset()} />
      </CardContent>
    </Card>
  );
}
