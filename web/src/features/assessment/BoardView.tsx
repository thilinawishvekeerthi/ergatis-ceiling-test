import { useState } from 'react';
import { Alert, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { NineBox } from '../../components/NineBox';
import type { BoardPerson, CellRef, GridCellDefinition } from '../../api/types';
import { labels } from '../../labels';
import { useBoard, useCalibrate } from './hooks';
import { calibrationSchema, personName, type CalibrationForm } from './schemas';

/** The nine-box for one cycle. With `canCalibrate`, people can be moved while the cycle is in calibration. */
export function BoardView({ cycleId, canCalibrate }: { cycleId: string; canCalibrate: boolean }) {
  const q = useBoard(cycleId);
  const [pending, setPending] = useState<{ person: BoardPerson; to: GridCellDefinition }>();

  if (q.isLoading) return <CircularProgress aria-label={labels.loading} />;
  if (q.isError || !q.data) return <Alert severity="error">{labels.loadFailed}</Alert>;

  const board = q.data;
  const cellAt = (column: number | null, row: number | null) => board.layout.cells.find((c) => c.column === column && c.row === row);
  const movable = canCalibrate && board.cycleStatus === 'Calibration' && board.mode === 'CalculatedWithOverride';
  const placements = board.people.map((p) => ({
    employeeId: p.employeeId,
    name: personName(p.name, p.employeeId) + (p.calibrated ? ' ✱' : ''),
    column: p.column,
    row: p.row,
  }));

  const onMove = (employeeId: string, to: CellRef) => {
    const person = board.people.find((p) => p.employeeId === employeeId);
    const cell = cellAt(to.column, to.row);
    if (person && cell && (person.column !== to.column || person.row !== to.row)) setPending({ person, to: cell });
  };
  const changed = board.people.filter((p) => p.calibrated);

  return (
    <section>
      <h2>{board.cycleName} – {labels.assessment.board}</h2>
      {canCalibrate && !movable && <Alert severity="info" sx={{ mb: 2 }}>{labels.assessment.calibrateOff}</Alert>}
      {!board.people.length && <p>{labels.assessment.boardEmpty}</p>}
      <NineBox grid={{ columnBounds: [], rowBounds: [], cells: board.layout.cells }} placements={placements} onMove={movable ? onMove : undefined} label={`${board.cycleName} talent grid`} />
      <p><small>{labels.assessment.boardNote} ✱ = calibrated</small></p>
      {changed.length > 0 && (
        <>
          <h3>{labels.assessment.calibrations}</h3>
          <ul>
            {changed.map((p) => (
              <li key={p.reviewId}>
                {personName(p.name, p.employeeId)}: {cellAt(p.proposedColumn, p.proposedRow)?.label} → {cellAt(p.column, p.row)?.label}
              </li>
            ))}
          </ul>
        </>
      )}
      {pending && <CalibrateDialog person={pending.person} to={pending.to} onClose={() => setPending(undefined)} />}
    </section>
  );
}

function CalibrateDialog({ person, to, onClose }: { person: BoardPerson; to: GridCellDefinition; onClose: () => void }) {
  const calibrate = useCalibrate();
  const form = useForm<CalibrationForm>({ resolver: zodResolver(calibrationSchema), defaultValues: { reason: '' } });
  const submit = form.handleSubmit((values) =>
    calibrate.mutate({ reviewId: person.reviewId, to: { column: to.column, row: to.row }, reason: values.reason }, { onSuccess: onClose }),
  );
  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="sm" aria-labelledby="calibrate-title">
      <form onSubmit={submit} noValidate>
        <DialogTitle id="calibrate-title">{labels.assessment.calibrateTitle(personName(person.name, person.employeeId), to.label)}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {calibrate.isError && <Alert severity="error">{calibrate.error.message}</Alert>}
            <TextField label={labels.assessment.reason} multiline minRows={3} size="small" autoFocus
              error={!!form.formState.errors.reason} helperText={form.formState.errors.reason?.message} {...form.register('reason')} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>{labels.assessment.cancel}</Button>
          <Button type="submit" variant="contained" disabled={calibrate.isPending}>{labels.assessment.confirmMove}</Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
