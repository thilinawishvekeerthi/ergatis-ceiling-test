import { useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, CircularProgress, TextField } from '@mui/material';
import { useQueryClient } from '@tanstack/react-query';
import { keys, useSaveScale, useScale } from '../../api/hooks';
import { labels } from '../../labels';
import { SCALE_MAX, SCALE_MIN, scaleSchema, type ScaleForm } from './schemas';
import { SaveStatus } from './SaveStatus';
import { VersionActions } from './VersionActions';

export function ScaleEditor() {
  const { id = '' } = useParams();
  const query = useScale(id);
  const save = useSaveScale(id);
  const qc = useQueryClient();
  const form = useForm<ScaleForm>({ resolver: zodResolver(scaleSchema), defaultValues: { levels: [] } });
  const { fields, append, remove } = useFieldArray({ control: form.control, name: 'levels' });
  const { errors, isDirty } = form.formState;

  useEffect(() => {
    // A background refetch must not throw away edits the user has not saved yet.
    if (isDirty) return;
    if (query.data?.payload) form.reset({ levels: query.data.payload.levels.map(({ label, minScore, band }) => ({ label, minScore, band })) });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- isDirty is read on purpose, not a trigger
  }, [query.data, form]);

  if (query.isLoading) return <CircularProgress aria-label={labels.loading} />;
  if (query.isError || !query.data) return <Alert severity="error">{labels.loadFailed}</Alert>;
  const editable = query.data.status === 'Draft';

  const submit = form.handleSubmit((values) =>
    save.mutate({ levels: values.levels.map((l, i) => ({ ...l, level: i + 1 })) }, { onSuccess: () => form.reset(values) }),
  );

  return (
    <form onSubmit={submit} noValidate aria-labelledby="scale-title">
      <h2 id="scale-title">{query.data.code} (version {query.data.versionNo}, {query.data.status})</h2>
      {!editable && <Alert severity="info">Only drafts can be edited. Create a new version to change this scale.</Alert>}
      <table>
        <thead>
          <tr><th scope="col">Level</th><th scope="col">Label</th><th scope="col">Minimum score</th><th scope="col">Reporting band</th><th /></tr>
        </thead>
        <tbody>
          {fields.map((field, i) => (
            <tr key={field.id}>
              <th scope="row">{i + 1}</th>
              <td>
                <TextField size="small" label={`Label for level ${i + 1}`} disabled={!editable}
                  {...form.register(`levels.${i}.label`)}
                  error={!!errors.levels?.[i]?.label} helperText={errors.levels?.[i]?.label?.message} />
              </td>
              <td>
                <TextField size="small" type="number" label={`Minimum score for level ${i + 1}`} disabled={!editable}
                  {...form.register(`levels.${i}.minScore`, { valueAsNumber: true })}
                  error={!!errors.levels?.[i]?.minScore} helperText={errors.levels?.[i]?.minScore?.message} />
              </td>
              <td>
                <TextField size="small" label={`Band for level ${i + 1}`} disabled={!editable}
                  {...form.register(`levels.${i}.band`)}
                  error={!!errors.levels?.[i]?.band} helperText={errors.levels?.[i]?.band?.message} />
              </td>
              <td>
                {editable && fields.length > SCALE_MIN && (
                  <Button onClick={() => remove(i)} aria-label={`Remove level ${i + 1}`}>Remove</Button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {errors.levels?.message && <Alert severity="error">{errors.levels.message}</Alert>}
      {errors.levels?.root?.message && <Alert severity="error">{errors.levels.root.message}</Alert>}
      {editable && (
        <p>
          <Button disabled={fields.length >= SCALE_MAX}
            onClick={() => append({ label: '', minScore: (form.getValues('levels').at(-1)?.minScore ?? 0) + 1, band: '' })}>
            Add level
          </Button>
          <Button type="submit" variant="contained" disabled={save.isPending}>{labels.save}</Button>
        </p>
      )}
      <SaveStatus error={save.error} saved={save.isSuccess}
        onReload={() => { save.reset(); void qc.invalidateQueries({ queryKey: keys.version(id) }); }} />
      <VersionActions type="Scale" version={query.data} basePath="/admin/scales" unsaved={isDirty} />
    </form>
  );
}
