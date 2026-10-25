import { useMemo, useState, type KeyboardEvent } from 'react';
import type { CellRef, GridCellDefinition, GridDefinition, Placement } from '../api/types';

export interface NineBoxProps {
  grid: GridDefinition;
  placements: Placement[];
  /** When set, each person gets a "Move" button; picking a destination cell calls this. */
  onMove?: (employeeId: string, to: CellRef) => void;
  /** Accessible name for the whole grid. */
  label?: string;
}

const key = (c: CellRef) => `${c.column}:${c.row}`;

/**
 * Generic N x M talent grid. Dimensions, labels and colours come from the grid definition;
 * nothing assumes 3 x 3. The highest row is drawn at the top, column 0 on the left.
 * Keyboard: arrow keys move between cells, Home/End jump along the row, Enter/Space picks
 * a destination while a move is armed, Escape cancels.
 */
export function NineBox({ grid, placements, onMove, label = 'Talent grid' }: NineBoxProps) {
  const cellMap = useMemo(() => new Map(grid.cells.map((c) => [key(c), c])), [grid.cells]);
  const columns = useMemo(() => Math.max(0, ...grid.cells.map((c) => c.column)) + 1, [grid.cells]);
  const rowIndexes = useMemo(
    () => [...new Set(grid.cells.map((c) => c.row))].sort((a, b) => b - a),
    [grid.cells],
  );
  const people = useMemo(() => {
    const by = new Map<string, Placement[]>();
    for (const p of placements) by.set(key(p), [...(by.get(key(p)) ?? []), p]);
    return by;
  }, [placements]);

  const first = rowIndexes[0] !== undefined ? { column: 0, row: rowIndexes[0] } : { column: 0, row: 0 };
  const [active, setActive] = useState<CellRef>(first);
  const [moving, setMoving] = useState<Placement | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const [refs] = useState(() => new Map<string, HTMLDivElement>());

  const focusCell = (target: CellRef) => {
    if (!cellMap.has(key(target))) return;
    setActive(target);
    refs.get(key(target))?.focus();
  };

  const choose = (cell: GridCellDefinition) => {
    if (!moving) return;
    onMove?.(moving.employeeId, { column: cell.column, row: cell.row });
    setAnnouncement(`${moving.name} moved to ${cell.label}`);
    setMoving(null);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>, cell: GridCellDefinition) => {
    const step = (dc: number, dr: number) => {
      event.preventDefault();
      focusCell({ column: cell.column + dc, row: cell.row + dr });
    };
    switch (event.key) {
      case 'ArrowRight': return step(1, 0);
      case 'ArrowLeft': return step(-1, 0);
      case 'ArrowUp': return step(0, 1);
      case 'ArrowDown': return step(0, -1);
      case 'Home': return step(-cell.column, 0);
      case 'End': return step(columns - 1 - cell.column, 0);
      case 'Enter':
      case ' ':
        if (moving && event.target === event.currentTarget) {
          event.preventDefault();
          choose(cell);
        }
        return;
      case 'Escape':
        if (moving) {
          setMoving(null);
          setAnnouncement('Move cancelled');
        }
        return;
    }
  };

  return (
    <div>
      <div role="grid" aria-label={label} style={{ display: 'grid', gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))`, gap: 4 }}>
        {rowIndexes.map((row) => (
          <div role="row" key={row} style={{ display: 'contents' }}>
            {Array.from({ length: columns }, (_, column) => cellMap.get(key({ column, row }))).map((cell, i) => {
              if (!cell) return <div key={i} role="presentation" />;
              const here = people.get(key(cell)) ?? [];
              const isActive = active.column === cell.column && active.row === cell.row;
              return (
                <div
                  key={key(cell)}
                  role="gridcell"
                  aria-label={`${cell.label}, ${here.length} ${here.length === 1 ? 'person' : 'people'}`}
                  tabIndex={isActive ? 0 : -1}
                  ref={(el) => {
                    if (el) refs.set(key(cell), el);
                    else refs.delete(key(cell));
                  }}
                  onFocus={() => setActive({ column: cell.column, row: cell.row })}
                  onKeyDown={(e) => onKeyDown(e, cell)}
                  onClick={() => choose(cell)}
                  data-band={cell.band}
                  style={{
                    background: cell.colour,
                    minHeight: 96,
                    padding: 8,
                    borderRadius: 4,
                    color: '#1b1b1b',
                    cursor: moving ? 'copy' : 'default',
                    outlineOffset: 2,
                  }}
                >
                  <strong style={{ display: 'block', marginBottom: 4 }} aria-hidden="true">{cell.label}</strong>
                  <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
                    {here.map((p) => (
                      <li key={p.employeeId} style={{ display: 'flex', gap: 6, alignItems: 'center', justifyContent: 'space-between' }}>
                        <span>{p.name}</span>
                        {onMove && (
                          <button
                            type="button"
                            aria-label={`Move ${p.name}`}
                            aria-pressed={moving?.employeeId === p.employeeId}
                            onClick={(e) => {
                              e.stopPropagation();
                              setMoving(moving?.employeeId === p.employeeId ? null : p);
                              setAnnouncement(`Choose a cell for ${p.name}`);
                            }}
                          >
                            Move
                          </button>
                        )}
                      </li>
                    ))}
                  </ul>
                </div>
              );
            })}
          </div>
        ))}
      </div>
      <div role="status" aria-live="polite" style={{ position: 'absolute', left: -9999 }}>{announcement}</div>
    </div>
  );
}
