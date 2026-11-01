import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../test/server';
import { AdminCycles } from './AdminCycles';
import { cycle } from './fixtures';
import { base, renderWithApi } from './testUtils';

describe('AdminCycles', () => {
  it('offers the next lifecycle step for each status', async () => {
    server.use(http.get(`${base}/api/v1/assessment/cycles`, () => HttpResponse.json([
      cycle({ id: 'a', name: 'Draft cycle', status: 'Draft' }),
      cycle({ id: 'b', name: 'Open cycle', status: 'Open' }),
      cycle({ id: 'c', name: 'Calibrating cycle', status: 'Calibration' }),
      cycle({ id: 'd', name: 'Closed cycle', status: 'Closed' }),
    ])));
    renderWithApi(<AdminCycles />);

    expect(await screen.findByRole('button', { name: 'Open cycle: Draft cycle' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Start calibration: Open cycle' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Close cycle: Calibrating cycle' })).toBeInTheDocument();
    const closed = screen.getByRole('row', { name: /Closed cycle/ });
    expect(within(closed).queryByRole('button')).not.toBeInTheDocument();
    expect(within(closed).getByRole('link', { name: 'Grid' })).toHaveAttribute('href', '/admin/cycles/d/board');
  });

  it('shows how many reviews are rated', async () => {
    server.use(http.get(`${base}/api/v1/assessment/cycles`, () => HttpResponse.json([cycle({ ratedCount: 3, reviewCount: 5 })])));
    renderWithApi(<AdminCycles />);

    expect(await screen.findByText('3 of 5')).toBeInTheDocument();
  });

  it('calls the lifecycle endpoint and reloads the list', async () => {
    let status: 'Draft' | 'Open' = 'Draft';
    server.use(
      http.get(`${base}/api/v1/assessment/cycles`, () => HttpResponse.json([cycle({ status })])),
      http.post(`${base}/api/v1/assessment/cycles/c1/open`, () => {
        status = 'Open';
        return HttpResponse.json(cycle({ status }));
      }),
    );
    const user = userEvent.setup();
    renderWithApi(<AdminCycles />);

    await user.click(await screen.findByRole('button', { name: 'Open cycle: 2026 annual' }));

    expect(await screen.findByRole('button', { name: 'Start calibration: 2026 annual' })).toBeInTheDocument();
  });

  it('shows the server message when a step is refused', async () => {
    server.use(
      http.get(`${base}/api/v1/assessment/cycles`, () => HttpResponse.json([cycle({ status: 'Draft' })])),
      http.post(`${base}/api/v1/assessment/cycles/c1/open`, () => HttpResponse.json({ detail: 'Cannot open a cycle that is Open.' }, { status: 409 })),
    );
    const user = userEvent.setup();
    renderWithApi(<AdminCycles />);

    await user.click(await screen.findByRole('button', { name: /Open cycle/ }));

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Cannot open a cycle that is Open.'));
  });

  it('says so when there are no cycles', async () => {
    server.use(http.get(`${base}/api/v1/assessment/cycles`, () => HttpResponse.json([])));
    renderWithApi(<AdminCycles />);

    expect(await screen.findByText('There are no review cycles yet.')).toBeInTheDocument();
  });
});
