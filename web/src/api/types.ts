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

export type Role = 'employee' | 'manager' | 'hr-admin';

export interface Session {
  userId: string;
  displayName: string;
  roles: Role[];
}

/** Nine-box evaluation API (/api/v1/assessment/nine-box). Mirrors docs/api/nine-box.openapi.yaml. */
export interface RubricLevel {
  level: number;
  label: string;
  description?: string | null;
}

export interface NineBoxCell extends GridCellDefinition {
  boxNumber: number;
}

export interface NineBoxGrid {
  gridVersionId: string;
  performanceLevels: RubricLevel[];
  potentialLevels: RubricLevel[];
  columnBounds: number[];
  rowBounds: number[];
  cells: NineBoxCell[];
}

export interface Evaluation {
  evaluationId: string;
  employeeId: string;
  performance: number;
  potential: number;
  boxNumber: number;
  label: string;
  talentScore: number;
  band: string;
  gridVersionId: string;
  evaluatedBy: string;
  evaluatedAt: string;
}

export interface EmployeePlacement {
  employeeId: string;
  employeeNumber: string;
  name: string;
  position?: string | null;
  department?: string | null;
  /** Null when never evaluated, or masked when the caller may not see talent-confidential data. */
  latest?: Evaluation | null;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface EvaluateInput {
  employeeId: string;
  performance: number;
  potential: number;
}
