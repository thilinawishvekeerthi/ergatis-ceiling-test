import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material';
import { useScale } from '../../api/hooks';
import type { Board, Cycle, Review } from '../../api/types';
import { labels } from '../../labels';
import { useRate } from './hooks';
import { personName, ratingSchema, type RatingForm } from './schemas';

/** Rating form for one review. The levels come from the scales the cycle pinned. */
export function RateDialog({ cycle, review, board, onClose }: { cycle: Cycle; review: Review; board?: Board; onClose: () => void }) {
  const performance = useScale(cycle.performanceScaleVersionId);
  const potential = useScale(cycle.potentialScaleVersionId);
  const rate = useRate(review.id);
  const manual = board?.mode === 'Manual';
  const form = useForm<RatingForm>({
    resolver: zodResolver(ratingSchema),
    defaultValues: {
      performanceLevel: review.performanceLevel?.toString() ?? '',
      potentialLevel: review.potentialLevel?.toString() ?? '',
      comment: review.comment ?? '',
      cell: review.finalColumn !== null && review.finalRow !== null ? `${review.finalColumn}:${review.finalRow}` : '',
    },
  });
  const { errors } = form.formState;
  const name = personName(review.employeeName, review.employeeId);

  const submit = form.handleSubmit((values) => {
    const [column, row] = (values.cell ?? '').split(':').map(Number);
    rate.mutate(
      {
        performanceLevel: Number(values.performanceLevel),
        potentialLevel: Number(values.potentialLevel),
        comment: values.comment?.trim() || undefined,
        ...(manual && values.cell ? { column, row } : {}),
      },
      { onSuccess: onClose },
    );
  });

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="sm" aria-labelledby="rate-title">
      <form onSubmit={submit} noValidate>
        <DialogTitle id="rate-title">{labels.assessment.rateTitle(name)}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {rate.isError && <Alert severity="error">{rate.error.message}</Alert>}
            <TextField select label={labels.assessment.performance} size="small" defaultValue={form.getValues('performanceLevel')}
              error={!!errors.performanceLevel} helperText={errors.performanceLevel?.message}
              slotProps={{ select: { native: true }, inputLabel: { shrink: true } }} {...form.register('performanceLevel')}>
              <option value="">{labels.assessment.chooseLevel}</option>
              {performance.data?.levels.map((l) => <option key={l.level} value={l.level}>{l.level} – {l.label}</option>)}
            </TextField>
            <TextField select label={labels.assessment.potential} size="small" defaultValue={form.getValues('potentialLevel')}
              error={!!errors.potentialLevel} helperText={errors.potentialLevel?.message}
              slotProps={{ select: { native: true }, inputLabel: { shrink: true } }} {...form.register('potentialLevel')}>
              <option value="">{labels.assessment.chooseLevel}</option>
              {potential.data?.levels.map((l) => <option key={l.level} value={l.level}>{l.level} – {l.label}</option>)}
            </TextField>
            {manual && (
              <TextField select label={labels.assessment.gridCell} size="small" defaultValue={form.getValues('cell')}
                slotProps={{ select: { native: true }, inputLabel: { shrink: true } }} {...form.register('cell')}>
                <option value="">{labels.assessment.chooseLevel}</option>
                {board?.layout.cells.map((c) => <option key={`${c.column}:${c.row}`} value={`${c.column}:${c.row}`}>{c.label}</option>)}
              </TextField>
            )}
            <TextField label={labels.assessment.comment} size="small" multiline minRows={3}
              error={!!errors.comment} helperText={errors.comment?.message} {...form.register('comment')} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>{labels.assessment.cancel}</Button>
          <Button type="submit" variant="contained" disabled={rate.isPending}>{labels.assessment.submitRating}</Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
