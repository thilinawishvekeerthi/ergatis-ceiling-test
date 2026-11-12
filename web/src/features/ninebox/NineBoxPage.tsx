import { useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  List,
  ListItemButton,
  ListItemText,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useEvaluations, useNineBoxEmployees, useNineBoxGrid } from '../../api/hooks';
import { ApiError } from '../../api/errors';
import type { NineBoxGrid, Placement } from '../../api/types';
import { NineBox } from '../../components/NineBox';
import { labels } from '../../labels';
import { EvaluationFormCard } from './EvaluationFormCard';

const t = labels.nineBox;

function colourOf(grid: NineBoxGrid | undefined, boxNumber: number | undefined) {
  return grid?.cells.find((c) => c.boxNumber === boxNumber)?.colour;
}

function History({ employeeId }: { employeeId: string }) {
  const q = useEvaluations(employeeId);
  if (q.isLoading) return <CircularProgress size={20} aria-label={labels.loading} />;
  if (q.isError) return <Alert severity="error">{labels.loadFailed}</Alert>;
  const items = q.data?.items ?? [];
  if (items.length === 0) return <Typography color="text.secondary">{t.noHistory}</Typography>;
  return (
    <Table size="small" aria-label={t.history}>
      <TableHead>
        <TableRow>
          <TableCell>{t.when}</TableCell>
          <TableCell>{t.reviewer}</TableCell>
          <TableCell align="center">{t.performance}</TableCell>
          <TableCell align="center">{t.potential}</TableCell>
          <TableCell>{t.result}</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {items.map((e, i) => (
          <TableRow key={e.evaluationId} selected={i === 0}>
            <TableCell>{new Date(e.evaluatedAt).toLocaleString()}</TableCell>
            <TableCell>{e.evaluatedBy}</TableCell>
            <TableCell align="center">{e.performance}</TableCell>
            <TableCell align="center">{e.potential}</TableCell>
            <TableCell>{e.label} · {t.score} {e.talentScore}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/** FR-1 to FR-5 on one screen: employee list, evaluation form, grid of latest placements, history. */
export function NineBoxPage() {
  const grid = useNineBoxGrid();
  const employees = useNineBoxEmployees();
  const [selectedId, setSelectedId] = useState<string>();

  const rows = useMemo(() => employees.data?.items ?? [], [employees.data]);
  const selected = rows.find((e) => e.employeeId === selectedId);
  const placements: Placement[] = useMemo(
    () =>
      rows.flatMap((e) =>
        e.latest ? [{ employeeId: e.employeeId, name: e.name, column: e.latest.performance - 1, row: e.latest.potential - 1 }] : [],
      ),
    [rows],
  );

  if (grid.isLoading || employees.isLoading) return <CircularProgress aria-label={labels.loading} />;
  const failure = grid.error ?? employees.error;
  if (failure) {
    return (
      <Alert severity={failure instanceof ApiError && failure.isForbidden ? 'warning' : 'error'} role="alert">
        {failure instanceof ApiError && failure.isForbidden ? labels.noAccess : labels.loadFailed}
      </Alert>
    );
  }

  return (
    <section>
      <Typography variant="h5" component="h2">{t.title}</Typography>
      <Typography color="text.secondary" sx={{ mb: 3 }}>{t.intro}</Typography>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '320px 1fr' }, gap: 3, alignItems: 'start' }}>
        <Paper variant="outlined">
          <Typography variant="subtitle1" component="h3" sx={{ px: 2, pt: 1.5, fontWeight: 600 }}>
            {t.employees} ({employees.data?.totalCount ?? 0})
          </Typography>
          <List dense aria-label={t.employees}>
            {rows.map((e) => (
              <ListItemButton key={e.employeeId} selected={e.employeeId === selectedId} onClick={() => setSelectedId(e.employeeId)}>
                <ListItemText primary={e.name} secondary={[e.position, e.department].filter(Boolean).join(' · ')} />
                <Chip
                  size="small"
                  label={e.latest ? e.latest.label : t.notEvaluated}
                  sx={{ ml: 1, bgcolor: colourOf(grid.data, e.latest?.boxNumber) ?? 'action.hover', color: '#1b1b1b' }}
                />
              </ListItemButton>
            ))}
          </List>
        </Paper>

        <Box sx={{ display: 'grid', gap: 3 }}>
          {selected && grid.data ? (
            <EvaluationFormCard employee={selected} grid={grid.data} />
          ) : (
            <Alert severity="info">{t.chooseEmployee}</Alert>
          )}

          {grid.data && (
            <Paper variant="outlined" sx={{ p: 2 }}>
              <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 600, mb: 1 }}>{t.grid}</Typography>
              <Box sx={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: 1, alignItems: 'center' }}>
                <Typography variant="caption" sx={{ writingMode: 'vertical-rl', transform: 'rotate(180deg)', textAlign: 'center' }}>
                  {t.potentialAxis}
                </Typography>
                <NineBox grid={grid.data} placements={placements} label={t.grid} />
                <span />
                <Typography variant="caption" sx={{ textAlign: 'center' }}>{t.performanceAxis}</Typography>
              </Box>
            </Paper>
          )}

          {selected && (
            <Paper variant="outlined" sx={{ p: 2 }}>
              <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 600, mb: 1 }}>{t.history}: {selected.name}</Typography>
              <History employeeId={selected.employeeId} />
            </Paper>
          )}
        </Box>
      </Box>
    </section>
  );
}
