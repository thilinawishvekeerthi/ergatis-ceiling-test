import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { Alert, CircularProgress, MenuItem, TextField } from '@mui/material';
import { labels } from '../../labels';
import { BoardView } from './BoardView';
import { useCycles } from './hooks';

/** /manager/board: the grid of the people assigned to the manager. Read only. */
export function ManagerBoard() {
  const cycles = useCycles();
  const [selected, setSelected] = useState<string>();
  if (cycles.isLoading) return <CircularProgress aria-label={labels.loading} />;
  if (cycles.isError) return <Alert severity="error">{labels.loadFailed}</Alert>;
  if (!cycles.data?.length) return <p>{labels.assessment.noCycles}</p>;
  const cycle = cycles.data.find((c) => c.id === selected) ?? cycles.data.find((c) => c.status === 'Open') ?? cycles.data[0]!;
  return (
    <section>
      <TextField select label={labels.assessment.cycle} size="small" value={cycle.id} onChange={(e) => setSelected(e.target.value)} sx={{ minWidth: 280, mb: 2 }}>
        {cycles.data.map((c) => <MenuItem key={c.id} value={c.id}>{c.name} ({c.status})</MenuItem>)}
      </TextField>
      <BoardView cycleId={cycle.id} canCalibrate={false} />
    </section>
  );
}

/** /admin/cycles/:id/board: everyone rated, with calibration while the cycle is in calibration. */
export function AdminBoard() {
  const { id = '' } = useParams();
  return <BoardView cycleId={id} canCalibrate />;
}
