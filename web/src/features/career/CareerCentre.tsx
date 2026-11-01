import { Alert, Card, CardContent, Chip, CircularProgress, LinearProgress, Stack } from '@mui/material';
import type { Opportunity } from '../../api/types';
import { labels } from '../../labels';
import { useMyCareerCentre } from './hooks';

/** The employee Career Centre: where I am, what I can do, and how close I am to each job I could move to. */
export function CareerCentre() {
  const q = useMyCareerCentre();

  if (q.isLoading) return <CircularProgress aria-label={labels.loading} />;
  if (q.isError || !q.data) return <Alert severity="error">{labels.loadFailed}</Alert>;

  const { currentJob, competencies, opportunities } = q.data;
  return (
    <section>
      <h2>{labels.nav.career}</h2>

      <h3>{labels.career.currentJob}</h3>
      {currentJob ? <p><strong>{currentJob.title}</strong> ({labels.career.level(currentJob.level)})</p> : <Alert severity="info">{labels.career.noCurrentJob}</Alert>}

      <h3>{labels.career.competencies}</h3>
      {competencies.length === 0 ? (
        <p>{labels.career.noCompetencies}</p>
      ) : (
        <Stack direction="row" spacing={1} useFlexGap component="ul" sx={{ listStyle: 'none', p: 0, m: 0, flexWrap: 'wrap' }}>
          {competencies.map((c) => (
            <li key={c.competencyCode}><Chip label={`${c.competencyCode}: ${c.level}`} /></li>
          ))}
        </Stack>
      )}

      <h3>{labels.career.opportunities}</h3>
      {opportunities.length === 0 ? (
        <p>{labels.career.noOpportunities}</p>
      ) : (
        <Stack spacing={2} component="ul" sx={{ listStyle: 'none', p: 0, m: 0 }}>
          {opportunities.map((o) => <li key={o.job.id}><OpportunityCard opportunity={o} /></li>)}
        </Stack>
      )}
    </section>
  );
}

function OpportunityCard({ opportunity: o }: { opportunity: Opportunity }) {
  const gaps = o.requirements.filter((r) => !r.met).length;
  const titleId = `job-${o.job.id}`;
  return (
    <Card variant="outlined" component="article" aria-labelledby={titleId}>
      <CardContent>
        <Stack direction="row" spacing={2} sx={{ justifyContent: 'space-between', alignItems: 'baseline' }}>
          <h4 id={titleId} style={{ margin: 0 }}>{o.job.title}</h4>
          <span>{o.steps === null ? labels.career.openRole : labels.career.stepsAway(o.steps)}</span>
        </Stack>
        <Stack direction="row" spacing={2} sx={{ my: 1, alignItems: 'center' }}>
          <LinearProgress variant="determinate" value={o.matchPercent} sx={{ flexGrow: 1, height: 10, borderRadius: 5 }}
            aria-label={`${o.job.title} ${labels.career.match}`} />
          <strong>{o.matchPercent}% {labels.career.match}</strong>
        </Stack>
        {o.requirements.length > 0 && (
          <details>
            <summary>{labels.career.gaps(gaps)}</summary>
            <table style={{ borderCollapse: 'collapse', marginTop: 8 }}>
              <thead>
                <tr>
                  <th scope="col" align="left">{labels.career.competency}</th>
                  <th scope="col" align="left">{labels.career.required}</th>
                  <th scope="col" align="left">{labels.career.you}</th>
                  <th scope="col" align="left" />
                </tr>
              </thead>
              <tbody>
                {o.requirements.map((r) => (
                  <tr key={r.competencyCode}>
                    <th scope="row" align="left">{r.competencyCode}</th>
                    <td>{r.requiredLevel}</td>
                    <td>{r.currentLevel}</td>
                    <td>{r.met ? <Chip size="small" color="success" label={labels.career.met} /> : <Chip size="small" color="warning" label={labels.career.needs(r.gap)} />}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </details>
        )}
      </CardContent>
    </Card>
  );
}
