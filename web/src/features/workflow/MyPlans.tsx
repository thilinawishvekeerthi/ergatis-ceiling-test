import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Alert, Button, Card, CardContent, Chip, CircularProgress, Stack, TextField, Typography } from '@mui/material';
import { useAct, useMyWorkflows, useStartWorkflow } from '../../api/hooks';
import { labels } from '../../labels';
import { SaveStatus } from '../config/SaveStatus';
import { WorkflowHistory } from './WorkflowHistory';

export const IDP_APPROVAL = 'IDP_APPROVAL';

export const planSchema = z.object({
  title: z.string().trim().min(3, 'At least 3 characters.').max(200, 'At most 200 characters.'),
  estimatedCost: z.number({ error: 'Enter a number.' }).min(0, 'Cannot be negative.').max(1_000_000, 'Too large.'),
});
export type PlanForm = z.infer<typeof planSchema>;

/**
 * Employee: write a development plan and send it for approval (workflow IDP_APPROVAL).
 * The server routes it to the employee's manager from the synced HR data, and to HR as well when it costs 1000 or more.
 */
export function MyPlans() {
  const plans = useMyWorkflows();
  const start = useStartWorkflow();
  const act = useAct();
  const form = useForm<PlanForm>({ resolver: zodResolver(planSchema), defaultValues: { title: '', estimatedCost: 0 } });
  const { errors } = form.formState;

  const submit = form.handleSubmit((v) =>
    start.mutate(
      {
        start: {
          definitionCode: IDP_APPROVAL,
          subjectType: 'Idp',
          context: { title: v.title, estimatedCost: String(v.estimatedCost) },
        },
        then: 'Submit',
      },
      { onSuccess: () => form.reset() },
    ),
  );

  const mine = (plans.data ?? []).filter((p) => p.definitionCode === IDP_APPROVAL);

  return (
    <section>
      <h2>{labels.nav.idp}</h2>
      <form onSubmit={submit} noValidate aria-label="New development plan">
        <Stack direction="row" spacing={2} sx={{ alignItems: 'flex-start', mb: 2 }}>
          <TextField size="small" label="Plan title" {...form.register('title')}
            error={!!errors.title} helperText={errors.title?.message} />
          <TextField size="small" type="number" label="Estimated cost" {...form.register('estimatedCost', { valueAsNumber: true })}
            error={!!errors.estimatedCost} helperText={errors.estimatedCost?.message} />
          <Button type="submit" variant="contained" disabled={start.isPending}>Submit for approval</Button>
        </Stack>
      </form>
      <SaveStatus error={start.error ?? act.error} saved={false} onReload={() => void plans.refetch()} />

      {plans.isLoading && <CircularProgress aria-label={labels.loading} />}
      {plans.isError && <Alert severity="error">{labels.loadFailed}</Alert>}
      {plans.isSuccess && mine.length === 0 && <p>{labels.emptyPlans}</p>}
      <Stack spacing={2}>
        {mine.map((p) => (
          <Card key={p.id} variant="outlined" component="article" aria-label={p.context.title ?? p.subjectId}>
            <CardContent>
              <Typography variant="h6" component="h3">
                {p.context.title} <Chip size="small" label={p.currentState} />
              </Typography>
              <Typography variant="body2">Estimated cost: {p.context.estimatedCost}</Typography>
              {p.openTask && <Typography variant="body2">Waiting for: {p.openTask.assignees.join(', ')}</Typography>}
              <WorkflowHistory history={p.history} />
              {/* As the starter, the employee can act only while the plan is with them (Draft). */}
              {!p.openTask && p.availableActions.map((a) => (
                <Button key={a} disabled={act.isPending} onClick={() => act.mutate({ id: p.id, action: a })}>{a}</Button>
              ))}
            </CardContent>
          </Card>
        ))}
      </Stack>
    </section>
  );
}
