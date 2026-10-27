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

export type ConfigType = 'Scale' | 'Grid';

/** One row of GET /api/v1/config/{type}: a definition and its newest version, whatever its status. */
export interface ConfigSummary {
  definitionId: string;
  type: string;
  code: string;
  name: string;
  latestVersionId: string;
  versionNo: number;
  status: ConfigVersionStatus;
}

/** GET /api/v1/config/versions/{id}. Payload, checksum and publisher are masked (null) for roles without Internal access. */
export interface ConfigVersion<P> {
  id: string;
  definitionId: string;
  type: string;
  code: string;
  versionNo: number;
  status: ConfigVersionStatus;
  payload: P | null;
  checksum: string | null;
  publishedBy: string | null;
  publishedAt: string | null;
}

export interface ScalePayload {
  levels: ScaleLevel[];
}

export type ScaleDetail = ConfigVersion<ScalePayload>;
export type GridDetail = ConfigVersion<GridDefinition>;

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

/** GET /bff/user. Roles are the server's role codes, for example HR_ADMIN. */
export interface BffUser {
  userId: string;
  roles: string[];
  hasEmployee: boolean;
}

export interface Session {
  userId: string;
  displayName: string;
  roles: Role[];
}

export type WorkflowStatus = 'Active' | 'Completed';

export interface ApprovalTask {
  id: string;
  instanceId: string;
  state: string;
  status: 'Pending' | 'Completed';
  dueAt: string | null;
  escalationLevel: number;
  assignees: string[];
}

export interface WorkflowHistoryEntry {
  sequence: number;
  fromState: string | null;
  toState: string;
  action: string;
  actor: string;
  comment: string | null;
  occurredAt: string;
}

export interface WorkflowInstance {
  id: string;
  definitionVersionId: string;
  definitionCode: string;
  subjectType: string;
  subjectId: string;
  currentState: string;
  status: WorkflowStatus;
  availableActions: string[];
  openTask: ApprovalTask | null;
  history: WorkflowHistoryEntry[];
  startedBy: string;
  startedAt: string;
  context: Record<string, string>;
}

export interface InboxItem {
  task: ApprovalTask;
  definitionCode: string;
  subjectType: string;
  subjectId: string;
  startedBy: string;
  context: Record<string, string>;
}

export interface StartWorkflow {
  definitionCode: string;
  subjectType: string;
  subjectId?: string;
  context: Record<string, string>;
}
