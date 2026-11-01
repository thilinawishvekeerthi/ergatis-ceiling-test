import { useState } from 'react';
import { Alert, Button, Chip, CircularProgress, MenuItem, TextField } from '@mui/material';
import type { Cycle, Review } from '../../api/types';
import { labels } from '../../labels';
import { useBoard, useCycles, useReviews } from './hooks';
import { RateDialog } from './RateDialog';
import { personName } from './schemas';

/** The manager's screen: pick a cycle, see the people assigned to you, rate them while the cycle is open. */
export function ManagerAssessments() {
  const cycles = useCycles();
  const [selected, setSelected] = useState<string>();
  const [rating, setRating] = useState<Review>();

  if (cycles.isLoading) return <CircularProgress aria-label={labels.loading} />;
  if (cycles.isError) return <Alert severity="error">{labels.loadFailed}</Alert>;
  if (!cycles.data?.length) return <p>{labels.assessment.noCycles}</p>;

  const cycle = cycles.data.find((c) => c.id === selected) ?? cycles.data.find((c) => c.status === 'Open') ?? cycles.data[0]!;

  return (
    <section>
      <h2>{labels.nav.assessments}</h2>
      <TextField select label={labels.assessment.cycle} size="small" value={cycle.id} onChange={(e) => setSelected(e.target.value)} sx={{ minWidth: 280 }}>
        {cycles.data.map((c) => <MenuItem key={c.id} value={c.id}>{c.name} ({c.status})</MenuItem>)}
      </TextField>
      <TeamReviews cycle={cycle} onRate={setRating} />
      {rating && <RateDialogHost cycle={cycle} review={rating} onClose={() => setRating(undefined)} />}
    </section>
  );
}

function RateDialogHost({ cycle, review, onClose }: { cycle: Cycle; review: Review; onClose: () => void }) {
  const board = useBoard(cycle.id);
  return <RateDialog cycle={cycle} review={review} board={board.data} onClose={onClose} />;
}

function TeamReviews({ cycle, onRate }: { cycle: Cycle; onRate: (review: Review) => void }) {
  const reviews = useReviews(cycle.id);
  const board = useBoard(cycle.id);

  if (reviews.isLoading) return <CircularProgress aria-label={labels.loading} />;
  if (reviews.isError) return <Alert severity="error">{labels.loadFailed}</Alert>;
  if (!reviews.data?.length) return <p>{labels.assessment.noReviews}</p>;

  const cellLabel = (r: Review) =>
    board.data?.layout.cells.find((c) => c.column === r.finalColumn && c.row === r.finalRow)?.label ?? '';
  const canRate = cycle.status === 'Open';

  return (
    <>
      {!canRate && <Alert severity="info" sx={{ my: 2 }}>{labels.assessment.ratingClosed}</Alert>}
      <table style={{ marginTop: 16, borderCollapse: 'collapse', width: '100%' }}>
        <thead>
          <tr>
            <th scope="col" align="left">Employee</th>
            <th scope="col" align="left">Status</th>
            <th scope="col" align="left">{labels.assessment.performance}</th>
            <th scope="col" align="left">{labels.assessment.potential}</th>
            <th scope="col" align="left">{labels.assessment.gridPosition}</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {reviews.data.map((r) => {
            const name = personName(r.employeeName, r.employeeId);
            return (
              <tr key={r.id}>
                <th scope="row" align="left">{name}</th>
                <td><Chip size="small" label={r.status} color={r.status === 'Rated' ? 'success' : 'default'} /></td>
                <td>{r.performanceLevel === null ? '–' : `${r.performanceLevel} – ${r.performanceLabel ?? ''}`}</td>
                <td>{r.potentialLevel ?? '–'}</td>
                <td>{cellLabel(r) || '–'}</td>
                <td align="right">
                  <Button size="small" variant="outlined" disabled={!canRate} onClick={() => onRate(r)} aria-label={`${labels.assessment.rate} ${name}`}>
                    {labels.assessment.rate}
                  </Button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </>
  );
}
