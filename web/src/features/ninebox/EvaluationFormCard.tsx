import { useEffect } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  FormControl,
  FormControlLabel,
  FormHelperText,
  FormLabel,
  Radio,
  RadioGroup,
  Typography,
} from '@mui/material';
import { useEvaluate } from '../../api/hooks';
import { ApiError } from '../../api/errors';
import type { EmployeePlacement, NineBoxGrid, RubricLevel } from '../../api/types';
import { labels } from '../../labels';
import { evaluationSchema, type EvaluationForm } from './schema';

const t = labels.nineBox;

function LevelField({
  name,
  legend,
  levels,
  value,
  onChange,
  error,
}: {
  name: string;
  legend: string;
  levels: RubricLevel[];
  value: number | undefined;
  onChange: (v: number) => void;
  error?: string;
}) {
  return (
    <FormControl error={!!error} component="fieldset" sx={{ mb: 2, width: '100%' }}>
      <FormLabel component="legend" sx={{ fontWeight: 600, color: 'text.primary' }}>{legend}</FormLabel>
      <RadioGroup name={name} value={value ?? ''} onChange={(e) => onChange(Number(e.target.value))}>
        {levels.map((l) => (
          <FormControlLabel
            key={l.level}
            value={l.level}
            control={<Radio size="small" />}
            label={
              <span>
                <strong>{l.level} · {l.label}</strong>
                {l.description ? <Typography component="span" variant="body2" color="text.secondary"> — {l.description}</Typography> : null}
              </span>
            }
          />
        ))}
      </RadioGroup>
      {error && <FormHelperText>{error}</FormHelperText>}
    </FormControl>
  );
}

/** FR-2 and FR-3: pick performance and potential with their rubric, then save. The server computes the box. */
export function EvaluationFormCard({ employee, grid }: { employee: EmployeePlacement; grid: NineBoxGrid }) {
  const evaluate = useEvaluate();
  const { control, handleSubmit, reset, setError, formState } = useForm<EvaluationForm>({
    resolver: zodResolver(evaluationSchema),
    defaultValues: { performance: undefined, potential: undefined },
  });

  const { reset: resetMutation } = evaluate;
  useEffect(() => {
    reset({ performance: undefined, potential: undefined });
    resetMutation();
  }, [employee.employeeId, reset, resetMutation]);

  const onSubmit = handleSubmit(async (values) => {
    try {
      await evaluate.mutateAsync({ employeeId: employee.employeeId, ...values });
    } catch (e) {
      if (e instanceof ApiError && e.fieldErrors) {
        for (const [field, messages] of Object.entries(e.fieldErrors)) {
          if (field === 'performance' || field === 'potential') setError(field, { message: messages[0] });
        }
      }
    }
  });

  const result = evaluate.data;
  const resultColour = result ? grid.cells.find((c) => c.boxNumber === result.boxNumber)?.colour : undefined;

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6" component="h3">{t.evaluate} {employee.name}</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          {[employee.position, employee.department].filter(Boolean).join(' · ')}
        </Typography>
        <form onSubmit={onSubmit} noValidate aria-label={`${t.evaluate} ${employee.name}`}>
          <Controller
            name="performance"
            control={control}
            render={({ field, fieldState }) => (
              <LevelField name="performance" legend={t.performance} levels={grid.performanceLevels}
                value={field.value} onChange={field.onChange} error={fieldState.error?.message} />
            )}
          />
          <Controller
            name="potential"
            control={control}
            render={({ field, fieldState }) => (
              <LevelField name="potential" legend={t.potential} levels={grid.potentialLevels}
                value={field.value} onChange={field.onChange} error={fieldState.error?.message} />
            )}
          />
          <Box sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
            <Button type="submit" variant="contained" disabled={formState.isSubmitting}>{t.submit}</Button>
          </Box>
        </form>
        {evaluate.isError && !(evaluate.error instanceof ApiError && evaluate.error.fieldErrors) && (
          <Alert severity="error" sx={{ mt: 2 }}>{evaluate.error.message}</Alert>
        )}
        {result && (
          <Alert severity="success" sx={{ mt: 2, borderLeft: `8px solid ${resultColour ?? '#2f5d62'}` }} role="status">
            {t.saved}: <strong>{result.label}</strong> · {t.box} {result.boxNumber} · {t.score} {result.talentScore} · {t.bands[result.band] ?? result.band}
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
