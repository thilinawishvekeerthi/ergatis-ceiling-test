import { useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { useFieldArray, useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, CircularProgress, TextField } from '@mui/material';
import { useQueryClient } from '@tanstack/react-query';
import { keys, useGrid, useSaveGrid } from '../../api/hooks';
import { NineBox } from '../../components/NineBox';
import { labels } from '../../labels';
import { GRID_MAX, GRID_MIN, gridSchema, type GridForm } from './schemas';
import { SaveStatus } from './SaveStatus';

type Cell = GridForm['cells'][number];
const blank = (column: number, row: number): Cell => ({ column, row, label: `Cell ${column + 1}, ${row + 1}`, colour: '#dddddd', band: 'unassigned' });

export function GridEditor() {
  const { id = '' } = useParams();
  const query = useGrid(id);
  const save = useSaveGrid(id);
  const qc = useQueryClient();
  const form = useForm<GridForm>({ resolver: zodResolver(gridSchema), defaultValues: { columnBounds: [], rowBounds: [], cells: [] } });
  const cols = useFieldArray({ control: form.control, name: 'columnBounds' });
  const rows = useFieldArray({ control: form.control, name: 'rowBounds' });
  const cells = useFieldArray({ control: form.control, name: 'cells' });
  const watched = useWatch({ control: form.control });
  const { errors } = form.formState;

  useEffect(() => {
    const g = query.data?.grid;
    if (g) form.reset({ columnBounds: g.columnBounds.map((value) => ({ value })), rowBounds: g.rowBounds.map((value) => ({ value })), cells: g.cells });
  }, [query.data, form]);

  if (query.isLoading) return <CircularProgress aria-label={labels.loading} />;
  if (query.isError || !query.data) return <Alert severity="error">{labels.loadFailed}</Alert>;
  const editable = query.data.status === 'Draft';

  const addColumn = () => {
    const n = cols.fields.length;
    cols.append({ value: (form.getValues('columnBounds').at(-1)?.value ?? 0) + 10 });
    for (let r = 0; r < rows.fields.length; r++) cells.append(blank(n, r));
  };
  const removeColumn = () => {
    const last = cols.fields.length - 1;
    cols.remove(last);
    const keep = form.getValues('cells').filter((c) => c.column !== last);
    cells.replace(keep);
  };
  const addRow = () => {
    const n = rows.fields.length;
    rows.append({ value: (form.getValues('rowBounds').at(-1)?.value ?? 0) + 10 });
    for (let c = 0; c < cols.fields.length; c++) cells.append(blank(c, n));
  };
  const removeRow = () => {
    const last = rows.fields.length - 1;
    rows.remove(last);
    cells.replace(form.getValues('cells').filter((c) => c.row !== last));
  };

  const submit = form.handleSubmit((v) =>
    save.mutate({
      ...query.data.grid,
      columnBounds: v.columnBounds.map((b) => b.value),
      rowBounds: v.rowBounds.map((b) => b.value),
      cells: v.cells,
    }),
  );

  const preview = { columnBounds: [], rowBounds: [], cells: (watched.cells ?? []).filter((c): c is Cell => !!c && !!c.label && !!c.colour) };

  return (
    <form onSubmit={submit} noValidate aria-labelledby="grid-title">
      <h2 id="grid-title">{query.data.name} (version {query.data.versionNo}, {query.data.status})</h2>
      {!editable && <Alert severity="info">Only drafts can be edited. Create a new version to change this grid.</Alert>}

      <fieldset disabled={!editable}>
        <legend>Axes</legend>
        <p>{cols.fields.length} columns by {rows.fields.length} rows</p>
        <Button onClick={addColumn} disabled={cols.fields.length >= GRID_MAX}>Add column</Button>
        <Button onClick={removeColumn} disabled={cols.fields.length <= GRID_MIN}>Remove column</Button>
        <Button onClick={addRow} disabled={rows.fields.length >= GRID_MAX}>Add row</Button>
        <Button onClick={removeRow} disabled={rows.fields.length <= GRID_MIN}>Remove row</Button>
        <div>
          {cols.fields.map((f, i) => (
            <TextField key={f.id} size="small" type="number" label={`Column ${i + 1} lower bound`}
              {...form.register(`columnBounds.${i}.value`, { valueAsNumber: true })}
              error={!!errors.columnBounds?.[i]?.value} helperText={errors.columnBounds?.[i]?.value?.message} />
          ))}
        </div>
        <div>
          {rows.fields.map((f, i) => (
            <TextField key={f.id} size="small" type="number" label={`Row ${i + 1} lower bound`}
              {...form.register(`rowBounds.${i}.value`, { valueAsNumber: true })}
              error={!!errors.rowBounds?.[i]?.value} helperText={errors.rowBounds?.[i]?.value?.message} />
          ))}
        </div>
      </fieldset>

      <fieldset disabled={!editable}>
        <legend>Cells</legend>
        {cells.fields.map((f, i) => (
          <div key={f.id} role="group" aria-label={`Cell column ${f.column + 1}, row ${f.row + 1}`}>
            <TextField size="small" label={`Label for column ${f.column + 1}, row ${f.row + 1}`}
              {...form.register(`cells.${i}.label`)}
              error={!!errors.cells?.[i]?.label} helperText={errors.cells?.[i]?.label?.message} />
            <TextField size="small" label={`Colour for column ${f.column + 1}, row ${f.row + 1}`}
              {...form.register(`cells.${i}.colour`)}
              error={!!errors.cells?.[i]?.colour} helperText={errors.cells?.[i]?.colour?.message} />
            <TextField size="small" label={`Band for column ${f.column + 1}, row ${f.row + 1}`}
              {...form.register(`cells.${i}.band`)}
              error={!!errors.cells?.[i]?.band} helperText={errors.cells?.[i]?.band?.message} />
          </div>
        ))}
      </fieldset>

      {errors.cells?.message && <Alert severity="error">{errors.cells.message}</Alert>}
      <h3>Preview</h3>
      <NineBox grid={preview} placements={[]} label="Grid preview" />
      {editable && <Button type="submit" variant="contained" disabled={save.isPending}>{labels.save}</Button>}
      <SaveStatus error={save.error} saved={save.isSuccess}
        onReload={() => { save.reset(); void qc.invalidateQueries({ queryKey: keys.grid(id) }); }} />
    </form>
  );
}
