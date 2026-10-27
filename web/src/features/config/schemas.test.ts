import { describe, expect, it } from 'vitest';
import { gridSchema, scaleSchema } from './schemas';

const level = (minScore: number) => ({ label: `L${minScore}`, minScore, band: 'b' });
const scale = (n: number) => ({ levels: Array.from({ length: n }, (_, i) => level(i)) });

const cell = (column: number, row: number) => ({ column, row, label: 'x', colour: '#a1b2c3', band: 'b' });
const grid = (cols: number[], rows: number[]) => ({
  columnBounds: cols.map((value) => ({ value })),
  rowBounds: rows.map((value) => ({ value })),
  cells: cols.flatMap((_, c) => rows.map((_, r) => cell(c, r))),
});

describe('scaleSchema', () => {
  it('accepts 2 to 10 ascending levels', () => {
    expect(scaleSchema.safeParse(scale(2)).success).toBe(true);
    expect(scaleSchema.safeParse(scale(10)).success).toBe(true);
  });

  it('rejects 1 and 11 levels', () => {
    expect(scaleSchema.safeParse(scale(1)).success).toBe(false);
    expect(scaleSchema.safeParse(scale(11)).success).toBe(false);
  });

  it('rejects minimum scores that do not go up', () => {
    const result = scaleSchema.safeParse({ levels: [level(5), level(5)] });
    expect(result.success).toBe(false);
  });
});

describe('gridSchema', () => {
  it('accepts a 3x3 grid with a cell for every position', () => {
    expect(gridSchema.safeParse(grid([0, 50, 100], [0, 50, 100])).success).toBe(true);
  });

  it('rejects an axis with 1 or 6 bounds', () => {
    expect(gridSchema.safeParse(grid([0], [0, 50])).success).toBe(false);
    expect(gridSchema.safeParse(grid([0, 1, 2, 3, 4, 5], [0, 50])).success).toBe(false);
  });

  it('rejects bounds that do not go up', () => {
    expect(gridSchema.safeParse(grid([0, 50, 50], [0, 50])).success).toBe(false);
  });

  it('rejects a bad colour', () => {
    const g = grid([0, 50], [0, 50]);
    g.cells[0].colour = 'red';
    expect(gridSchema.safeParse(g).success).toBe(false);
  });

  it('rejects a duplicated position even when the cell count is right', () => {
    const g = grid([0, 50], [0, 50]);
    g.cells[3] = { ...g.cells[0] };
    expect(gridSchema.safeParse(g).success).toBe(false);
  });

  it('rejects a cell outside the grid', () => {
    const g = grid([0, 50], [0, 50]);
    g.cells[3] = { ...g.cells[3], column: 5 };
    expect(gridSchema.safeParse(g).success).toBe(false);
  });

  it('rejects a missing cell', () => {
    const g = grid([0, 50], [0, 50]);
    g.cells.pop();
    expect(gridSchema.safeParse(g).success).toBe(false);
  });
});
