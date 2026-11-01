/** Hand-written API types. They mirror the Talent.Config payloads; replace with generated types once the OpenAPI document exists (ADR-12). */

export type PlacementMode = 'Manual' | 'Calculated' | 'CalculatedWithOverride';
export type ConfigVersionStatus = 'Draft' | 'Published' | 'Locked' | 'Retired';

export interface ScaleLevel {
  level: number;
  label: string;
  minScore: number;
  band: string;
}

export interface GridCellDefinition {
  column: number;
  row: number;
  label: string;
  colour: string;
  band: string;
}

export interface GridDefinition {
  columnBounds: number[];
  rowBounds: number[];
  cells: GridCellDefinition[];
  mode?: PlacementMode;
  columnScaleVersionId?: string | null;
  rowScaleVersionId?: string | null;
}

export interface Placement {
  employeeId: string;
  name: string;
  column: number;
  row: number;
}

export interface CellRef {
  column: number;
  row: number;
}

export interface ConfigSummary {
  id: string;
  name: string;
  versionNo: number;
  status: ConfigVersionStatus;
}

export interface ScaleDetail extends ConfigSummary {
  levels: ScaleLevel[];
}

export interface GridDetail extends ConfigSummary {
  grid: GridDefinition;
}

export interface ScoreComponent {
  key: string;
  label: string;
  weight: number;
  score: number;
  note?: string;
}

export interface ScoreBreakdownData {
  title: string;
  total: number;
  max: number;
  components: ScoreComponent[];
}

export type CycleStatus = 'Draft' | 'Open' | 'Calibration' | 'Closed';
export type ReviewStatus = 'Pending' | 'Rated';

export interface Cycle {
  id: string;
  name: string;
  periodStart: string;
  periodEnd: string;
  status: CycleStatus;
  performanceScaleVersionId: string;
  potentialScaleVersionId: string;
  gridVersionId: string;
  openedAt: string | null;
  closedAt: string | null;
  /** Null when the caller may not see counts. */
  reviewCount: number | null;
  ratedCount: number | null;
}

/** Potential and the grid cell are null when the caller may not see them (an employee on their own review). */
export interface Review {
  id: string;
  cycleId: string;
  employeeId: string;
  employeeName: string | null;
  reviewerId: string;
  status: ReviewStatus;
  performanceLevel: number | null;
  performanceLabel: string | null;
  potentialLevel: number | null;
  comment: string | null;
  ratedAt: string | null;
  proposedColumn: number | null;
  proposedRow: number | null;
  finalColumn: number | null;
  finalRow: number | null;
  calibratedBy: string | null;
  calibrationReason: string | null;
}

export interface RatingInput {
  performanceLevel: number;
  potentialLevel: number;
  comment?: string;
  /** Only for grids that are placed by hand. */
  column?: number;
  row?: number;
}

export interface BoardPerson {
  reviewId: string;
  employeeId: string;
  name: string | null;
  column: number;
  row: number;
  calibrated: boolean;
  proposedColumn: number | null;
  proposedRow: number | null;
}

export interface Board {
  cycleId: string;
  cycleName: string;
  cycleStatus: CycleStatus;
  mode: PlacementMode;
  layout: { versionId: string; mode: PlacementMode; columns: number; rows: number; cells: GridCellDefinition[] };
  people: BoardPerson[];
}

export interface CareerJobSummary {
  id: string;
  code: string;
  title: string;
  level: number;
}

export interface CareerCompetency {
  competencyCode: string;
  level: number;
}

/** One requirement of a target job against the employee's level. gap is 0 when it is met. */
export interface RequirementProgress {
  competencyCode: string;
  requiredLevel: number;
  currentLevel: number;
  weight: number;
  met: boolean;
  gap: number;
}

/** steps is the fewest career moves to the job, or null when the employee has no current job. */
export interface Opportunity {
  job: CareerJobSummary;
  steps: number | null;
  matchPercent: number;
  requirements: RequirementProgress[];
}

export interface CareerCentreData {
  employeeId: string;
  currentJob: CareerJobSummary | null;
  competencies: CareerCompetency[];
  opportunities: Opportunity[];
}

export interface AppNotification {
  id: string;
  eventKey: string;
  title: string;
  body: string;
  /** A path inside the app, or null. */
  linkUrl: string | null;
  createdAt: string;
  isRead: boolean;
}

export interface Inbox {
  unreadCount: number;
  items: AppNotification[];
}

export type Role = 'employee' | 'manager' | 'hr-admin';

export interface Session {
  userId: string;
  displayName: string;
  roles: Role[];
}
