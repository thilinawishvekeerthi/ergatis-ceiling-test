import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { NineBox } from './NineBox';
import type { GridDefinition, Placement } from '../api/types';

const grid: GridDefinition = {
  columnBounds: [0, 50, 100],
  rowBounds: [0, 50, 100],
  cells: [
    { column: 0, row: 0, label: 'Low', colour: '#f88', band: 'low' },
    { column: 1, row: 0, label: 'Core', colour: '#fd8', band: 'mid' },
    { column: 0, row: 1, label: 'Grow', colour: '#fd8', band: 'mid' },
    { column: 1, row: 1, label: 'Star', colour: '#8d8', band: 'high' },
  ],
};

const placements: Placement[] = [{ employeeId: 'e1', name: 'Ada Lovelace', column: 1, row: 1 }];

describe('NineBox', () => {
  it('renders one labelled cell per grid cell', () => {
    render(<NineBox grid={grid} placements={[]} />);
    expect(screen.getAllByRole('gridcell')).toHaveLength(4);
    expect(screen.getByRole('gridcell', { name: /star/i })).toBeInTheDocument();
  });

  it('shows an employee inside the cell they are placed in', () => {
    render(<NineBox grid={grid} placements={placements} />);
    const cell = screen.getByRole('gridcell', { name: /star/i });
    expect(within(cell).getByText('Ada Lovelace')).toBeInTheDocument();
  });

  it('supports arrow-key navigation between cells', async () => {
    const user = userEvent.setup();
    render(<NineBox grid={grid} placements={[]} />);
    const cells = screen.getAllByRole('gridcell');
    cells[0].focus();
    await user.keyboard('{ArrowRight}');
    expect(cells[1]).toHaveFocus();
  });

  it('reports a move when an employee is dropped via keyboard', async () => {
    const onMove = vi.fn();
    const user = userEvent.setup();
    render(<NineBox grid={grid} placements={placements} onMove={onMove} />);
    // Arm the move with Enter on the button, then pick the destination cell with Enter.
    screen.getByRole('button', { name: /move ada lovelace/i }).focus();
    await user.keyboard('{Enter}');
    screen.getByRole('gridcell', { name: /core/i }).focus();
    await user.keyboard('{Enter}');
    expect(onMove).toHaveBeenCalledWith('e1', { column: 1, row: 0 });
  });
});
