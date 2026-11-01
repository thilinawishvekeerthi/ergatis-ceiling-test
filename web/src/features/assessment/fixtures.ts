import type { Board, Cycle, GridCellDefinition, Review } from '../../api/types';

export const cycle = (over: Partial<Cycle> = {}): Cycle => ({
  id: 'c1',
  name: '2026 annual',
  periodStart: '2026-01-01',
  periodEnd: '2026-12-31',
  status: 'Open',
  performanceScaleVersionId: 'perf',
  potentialScaleVersionId: 'pot',
  gridVersionId: 'grid',
  openedAt: null,
  closedAt: null,
  reviewCount: 2,
  ratedCount: 1,
  ...over,
});

export const review = (over: Partial<Review> = {}): Review => ({
  id: 'r1',
  cycleId: 'c1',
  employeeId: '00000000-0000-0000-0000-0000000000a3',
  employeeName: 'Ada Lovelace',
  reviewerId: 'm1',
  status: 'Pending',
  performanceLevel: null,
  performanceLabel: null,
  potentialLevel: null,
  comment: null,
  ratedAt: null,
  proposedColumn: null,
  proposedRow: null,
  finalColumn: null,
  finalRow: null,
  calibratedBy: null,
  calibrationReason: null,
  ...over,
});

const labelsByCell = [
  ['Under Performer', 'Effective', 'Trusted Professional'],
  ['Dilemma', 'Core Employee', 'High Performer'],
  ['Enigma', 'Growth Employee', 'Star'],
];

export const nineCells: GridCellDefinition[] = [0, 1, 2].flatMap((row) =>
  [0, 1, 2].map((column) => ({ column, row, label: labelsByCell[row]![column]!, colour: '#dddddd', band: 'MEDIUM' })),
);

export const board = (over: Partial<Board> = {}): Board => ({
  cycleId: 'c1',
  cycleName: '2026 annual',
  cycleStatus: 'Calibration',
  mode: 'CalculatedWithOverride',
  layout: { versionId: 'grid', mode: 'CalculatedWithOverride', columns: 3, rows: 3, cells: nineCells },
  people: [
    { reviewId: 'r1', employeeId: 'e1', name: 'Ada Lovelace', column: 1, row: 1, calibrated: false, proposedColumn: 1, proposedRow: 1 },
    { reviewId: 'r2', employeeId: 'e2', name: 'Grace Hopper', column: 2, row: 2, calibrated: true, proposedColumn: 1, proposedRow: 1 },
  ],
  ...over,
});

export const performanceScale = {
  id: 'perf', code: 'PERFORMANCE_5', versionNo: 1, status: 'Locked',
  payload: { levels: [1, 2, 3, 4, 5].map((level) => ({ level, label: `Perf ${level}`, minScore: level, band: 'MEDIUM' })) },
};
export const potentialScale = {
  id: 'pot', code: 'POTENTIAL_3', versionNo: 1, status: 'Locked',
  payload: { levels: [1, 2, 3].map((level) => ({ level, label: `Pot ${level}`, minScore: level, band: 'MEDIUM' })) },
};
