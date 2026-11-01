import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../test/server';
import { board, cycle, performanceScale, potentialScale, review } from './fixtures';
import { ManagerAssessments } from './ManagerAssessments';
import { base, renderWithApi } from './testUtils';

function serve(opts: { status?: 'Open' | 'Calibration'; reviews?: ReturnType<typeof review>[] } = {}) {
  server.use(
    http.get(`${base}/api/v1/assessment/cycles`, () => HttpResponse.json([cycle({ status: opts.status ?? 'Open' })])),
    http.get(`${base}/api/v1/assessment/cycles/c1/reviews`, () => HttpResponse.json(opts.reviews ?? [review()])),
    http.get(`${base}/api/v1/assessment/cycles/c1/board`, () => HttpResponse.json(board())),
    http.get(`${base}/api/v1/config/versions/perf`, () => HttpResponse.json(performanceScale)),
    http.get(`${base}/api/v1/config/versions/pot`, () => HttpResponse.json(potentialScale)),
  );
}

describe('ManagerAssessments', () => {
  it('lists the people assigned to the manager with their status', async () => {
    serve({ reviews: [review(), review({ id: 'r2', employeeId: 'e2', employeeName: 'Grace Hopper', status: 'Rated', performanceLevel: 4, performanceLabel: 'Exceeds', potentialLevel: 3, finalColumn: 2, finalRow: 2 })] });
    renderWithApi(<ManagerAssessments />);

    expect(await screen.findByRole('rowheader', { name: 'Ada Lovelace' })).toBeInTheDocument();
    const grace = screen.getByRole('row', { name: /Grace Hopper/ });
    expect(within(grace).getByText('Rated')).toBeInTheDocument();
    expect(within(grace).getByText('4 – Exceeds')).toBeInTheDocument();
    expect(within(grace).getByText('Star')).toBeInTheDocument();
  });

  it('shows a short id when the employee has no synced name', async () => {
    serve({ reviews: [review({ employeeName: null })] });
    renderWithApi(<ManagerAssessments />);

    expect(await screen.findByRole('rowheader', { name: 'Employee 00a3' })).toBeInTheDocument();
  });

  it('explains when nobody is assigned', async () => {
    serve({ reviews: [] });
    renderWithApi(<ManagerAssessments />);

    expect(await screen.findByText('No one is assigned to you in this cycle.')).toBeInTheDocument();
  });

  it('disables rating once the cycle has left Open', async () => {
    serve({ status: 'Calibration' });
    renderWithApi(<ManagerAssessments />);

    expect(await screen.findByText('Ratings are closed for this cycle.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rate Ada Lovelace' })).toBeDisabled();
  });

  it('sends the chosen levels and comment, then closes the form', async () => {
    serve();
    let sent: unknown;
    server.use(http.put(`${base}/api/v1/assessment/reviews/r1/rating`, async ({ request }) => {
      sent = await request.json();
      return HttpResponse.json(review({ status: 'Rated' }));
    }));
    const user = userEvent.setup();
    renderWithApi(<ManagerAssessments />);

    await user.click(await screen.findByRole('button', { name: 'Rate Ada Lovelace' }));
    const dialog = await screen.findByRole('dialog', { name: 'Rate Ada Lovelace' });
    await within(dialog).findByRole('option', { name: '5 – Perf 5' });
    await user.selectOptions(within(dialog).getByLabelText('Performance'), '5');
    await user.selectOptions(within(dialog).getByLabelText('Potential'), '3');
    await user.type(within(dialog).getByLabelText('Comment'), 'Great year');
    await user.click(within(dialog).getByRole('button', { name: 'Save rating' }));

    await waitFor(() => expect(sent).toEqual({ performanceLevel: 5, potentialLevel: 3, comment: 'Great year' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('asks for both levels before sending anything', async () => {
    serve();
    let called = false;
    server.use(http.put(`${base}/api/v1/assessment/reviews/r1/rating`, () => {
      called = true;
      return HttpResponse.json(review());
    }));
    const user = userEvent.setup();
    renderWithApi(<ManagerAssessments />);

    await user.click(await screen.findByRole('button', { name: 'Rate Ada Lovelace' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Save rating' }));

    await waitFor(() => expect(within(dialog).getAllByText('Choose a level').length).toBeGreaterThanOrEqual(2));
    expect(called).toBe(false);
  });

  it('keeps the form open and shows the server message when the rating is refused', async () => {
    serve();
    server.use(http.put(`${base}/api/v1/assessment/reviews/r1/rating`, () =>
      HttpResponse.json({ detail: 'Only the assigned reviewer can rate this employee.' }, { status: 403 })));
    const user = userEvent.setup();
    renderWithApi(<ManagerAssessments />);

    await user.click(await screen.findByRole('button', { name: 'Rate Ada Lovelace' }));
    const dialog = await screen.findByRole('dialog');
    await within(dialog).findByRole('option', { name: '3 – Perf 3' });
    await user.selectOptions(within(dialog).getByLabelText('Performance'), '3');
    await user.selectOptions(within(dialog).getByLabelText('Potential'), '2');
    await user.click(within(dialog).getByRole('button', { name: 'Save rating' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Only the assigned reviewer can rate this employee.');
  });
});
