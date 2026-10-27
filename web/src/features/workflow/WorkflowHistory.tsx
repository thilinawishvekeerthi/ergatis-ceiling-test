import type { WorkflowHistoryEntry } from '../../api/types';

/** The audit trail of one workflow, oldest first. */
export function WorkflowHistory({ history }: { history: WorkflowHistoryEntry[] }) {
  return (
    <ol aria-label="History">
      {history.map((h) => (
        <li key={h.sequence}>
          {h.action} by {h.actor}: {h.fromState ?? 'start'} to {h.toState}
          {h.comment ? ` ("${h.comment}")` : ''}
        </li>
      ))}
    </ol>
  );
}
