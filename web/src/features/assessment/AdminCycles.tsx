import { Link as RouterLink } from 'react-router-dom';
import { Alert, Button, Chip, CircularProgress } from '@mui/material';
import type { Cycle } from '../../api/types';
import { labels } from '../../labels';
import { useCycles, useMoveCycle } from './hooks';

const nextStep: Partial<Record<Cycle['status'], { action: 'open' | 'calibration' | 'close'; label: string }>> = {
  Draft: { action: 'open', label: labels.assessment.open },
  Open: { action: 'calibration', label: labels.assessment.startCalibration },
  Calibration: { action: 'close', label: labels.assessment.close },
};

/** HR admin: where each cycle stands, the next step, and a link to its grid. */
export function AdminCycles() {
  const cycles = useCycles();
  const move = useMoveCycle();

  if (cycles.isLoading) return <CircularProgress aria-label={labels.loading} />;
  if (cycles.isError) return <Alert severity="error">{labels.loadFailed}</Alert>;
  if (!cycles.data?.length) return <p>{labels.assessment.noCycles}</p>;

  return (
    <section>
      <h2>{labels.nav.cycles}</h2>
      {move.isError && <Alert severity="error" sx={{ mb: 2 }}>{move.error.message}</Alert>}
      <table style={{ borderCollapse: 'collapse', width: '100%' }}>
        <thead>
          <tr>
            <th scope="col" align="left">Cycle</th>
            <th scope="col" align="left">Period</th>
            <th scope="col" align="left">Status</th>
            <th scope="col" align="left">Rated</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {cycles.data.map((c) => {
            const step = nextStep[c.status];
            return (
              <tr key={c.id}>
                <th scope="row" align="left">{c.name}</th>
                <td>{c.periodStart} to {c.periodEnd}</td>
                <td><Chip size="small" label={c.status} /></td>
                <td>{c.ratedCount ?? 0} of {c.reviewCount ?? 0}</td>
                <td align="right">
                  <Button size="small" component={RouterLink} to={`/admin/cycles/${c.id}/board`}>{labels.assessment.board}</Button>
                  {step && (
                    <Button size="small" variant="contained" disabled={move.isPending} onClick={() => move.mutate({ id: c.id, action: step.action })} aria-label={`${step.label}: ${c.name}`}>
                      {step.label}
                    </Button>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </section>
  );
}
