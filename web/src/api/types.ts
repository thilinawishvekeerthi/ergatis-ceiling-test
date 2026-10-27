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
