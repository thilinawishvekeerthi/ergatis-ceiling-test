import { z } from 'zod';

/** Mirrors the rules in Talent.Config (ScaleDefinition, GridDefinition, GridPayload). */
export const SCALE_MIN = 2;
export const SCALE_MAX = 10;
export const GRID_MIN = 2;
export const GRID_MAX = 5;

const text = (what: string) => z.string().trim().min(1, `${what} is required`);

export const scaleSchema = z
  .object({
    levels: z
      .array(
        z.object({
          label: text('Label'),
          minScore: z.number({ error: 'Enter a number' }),
          band: text('Reporting band'),
        }),
      )
      .min(SCALE_MIN, `A scale needs at least ${SCALE_MIN} levels`)
      .max(SCALE_MAX, `A scale can have at most ${SCALE_MAX} levels`),
  })
  .superRefine((value, ctx) => {
    value.levels.forEach((level, i) => {
      const prev = value.levels[i - 1];
      if (prev && level.minScore <= prev.minScore) {
        ctx.addIssue({ code: 'custom', path: ['levels', i, 'minScore'], message: 'Minimum scores must go up with each level' });
      }
    });
  });
export type ScaleForm = z.infer<typeof scaleSchema>;

const bounds = (axis: string) =>
  z
    .array(z.object({ value: z.number({ error: 'Enter a number' }) }))
    .min(GRID_MIN, `The ${axis} axis needs at least ${GRID_MIN} levels`)
    .max(GRID_MAX, `The ${axis} axis can have at most ${GRID_MAX} levels`)
    .superRefine((items, ctx) => {
      items.forEach((item, i) => {
        const prev = items[i - 1];
        if (prev && item.value <= prev.value) {
          ctx.addIssue({ code: 'custom', path: [i, 'value'], message: 'Bounds must go up with each level' });
        }
      });
    });

export const gridSchema = z
  .object({
    columnBounds: bounds('column'),
    rowBounds: bounds('row'),
    cells: z.array(
      z.object({
        column: z.number(),
        row: z.number(),
        label: text('Label'),
        colour: z.string().regex(/^#[0-9a-fA-F]{6}$/, 'Use a colour like #1a2b3c'),
        band: text('Reporting band'),
      }),
    ),
  })
  .refine(
    (g) => {
      // Each (column, row) position must be defined exactly once; a duplicate plus a gap is not enough.
      const positions = new Set(g.cells.map((c) => `${c.column}:${c.row}`));
      return (
        g.cells.length === g.columnBounds.length * g.rowBounds.length &&
        positions.size === g.cells.length &&
        g.cells.every((c) => Number.isInteger(c.column) && c.column >= 0 && c.column < g.columnBounds.length &&
          Number.isInteger(c.row) && c.row >= 0 && c.row < g.rowBounds.length)
      );
    },
    { message: 'Every cell needs a definition', path: ['cells'] },
  );
export type GridForm = z.infer<typeof gridSchema>;
