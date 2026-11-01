import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../test/server';
import { BoardView } from './BoardView';
import { board, review } from './fixtures';
import { base, renderWithApi } from './testUtils';

const serveBoard = (b = board()) => server.use(http.get(`${base}/api/v1/assessment/cycles/c1/board`, () => HttpResponse.json(b)));

describe('BoardView', () => {
  it('draws each person in their cell and marks calibrated ones', async () => {
    serveBoard();
    renderWithApi(<BoardView cycleId="c1" canCalibrate />);

    const core = await screen.findByRole('gridcell', { name: /Core Employee/ });
    expect(within(core).getByText('Ada Lovelace')).toBeInTheDocument();
    const star = screen.getByRole('gridcell', { name: /Star/ });
    expect(within(star).getByText('Grace Hopper ✱')).toBeInTheDocument();
  });

  it('lists calibration changes as from → to', async () => {
    serveBoard();
    renderWithApi(<BoardView cycleId="c1" canCalibrate />);

    expect(await screen.findByText('Grace Hopper: Core Employee → Star')).toBeInTheDocument();
  });

  it('offers Move buttons only to a calibrator while the cycle is in calibration', async () => {
    serveBoard();
    renderWithApi(<BoardView cycleId="c1" canCalibrate />);

    expect(await screen.findByRole('button', { name: 'Move Ada Lovelace' })).toBeInTheDocument();
  });

  it('is read only for a manager', async () => {
    serveBoard();
    renderWithApi(<BoardView cycleId="c1" canCalibrate={false} />);

    await screen.findByRole('gridcell', { name: /Core Employee/ });
    expect(screen.queryByRole('button', { name: /Move/ })).not.toBeInTheDocument();
  });

  it('is read only, with a reason, when the cycle is not in calibration', async () => {
    serveBoard(board({ cycleStatus: 'Open' }));
    renderWithApi(<BoardView cycleId="c1" canCalibrate />);

    expect(await screen.findByText(/only while the cycle is in calibration/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Move/ })).not.toBeInTheDocument();
  });

  it('is read only when the grid does not allow an override', async () => {
    serveBoard(board({ mode: 'Calculated' }));
    renderWithApi(<BoardView cycleId="c1" canCalibrate />);

    await screen.findByRole('gridcell', { name: /Core Employee/ });
    expect(screen.queryByRole('button', { name: /Move/ })).not.toBeInTheDocument();
  });

  it('moves a person: pick Move, pick the cell, give a reason, and the reason is sent', async () => {
    serveBoard();
    let sent: unknown;
    server.use(http.post(`${base}/api/v1/assessment/reviews/r1/calibrate`, async ({ request }) => {
      sent = await request.json();
      return HttpResponse.json(review());
    }));
    const user = userEvent.setup();
    renderWithApi(<BoardView cycleId="c1" canCalibrate />);

    await user.click(await screen.findByRole('button', { name: 'Move Ada Lovelace' }));
    await user.click(screen.getByRole('gridcell', { name: /High Performer/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Move Ada Lovelace to High Performer' });
    await user.type(within(dialog).getByLabelText('Reason for the change'), 'Delivered the platform launch');
    await user.click(within(dialog).getByRole('button', { name: 'Move' }));

    await waitFor(() => expect(sent).toEqual({ column: 2, row: 1, reason: 'Delivered the platform launch' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('will not send a move without a reason', async () => {
    serveBoard();
    let called = false;
    server.use(http.post(`${base}/api/v1/assessment/reviews/r1/calibrate`, () => {
      called = true;
      return HttpResponse.json(review());
    }));
    const user = userEvent.setup();
    renderWithApi(<BoardView cycleId="c1" canCalibrate />);

    await user.click(await screen.findByRole('button', { name: 'Move Ada Lovelace' }));
    await user.click(screen.getByRole('gridcell', { name: /High Performer/ }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Move' }));

    expect(await within(dialog).findByText('A reason is required.')).toBeInTheDocument();
    expect(called).toBe(false);
  });

  it('shows the server message when the move is refused', async () => {
    serveBoard();
    server.use(http.post(`${base}/api/v1/assessment/reviews/r1/calibrate`, () =>
      HttpResponse.json({ detail: 'You do not have permission to do this.' }, { status: 403 })));
    const user = userEvent.setup();
    renderWithApi(<BoardView cycleId="c1" canCalibrate />);

    await user.click(await screen.findByRole('button', { name: 'Move Ada Lovelace' }));
    await user.click(screen.getByRole('gridcell', { name: /High Performer/ }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Reason for the change'), 'Because');
    await user.click(within(dialog).getByRole('button', { name: 'Move' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('You do not have permission to do this.');
  });

  it('says so when nobody is rated', async () => {
    serveBoard(board({ people: [] }));
    renderWithApi(<BoardView cycleId="c1" canCalibrate={false} />);

    expect(await screen.findByText('No one has been rated yet.')).toBeInTheDocument();
  });

  it('shows an error when the board cannot be loaded', async () => {
    server.use(http.get(`${base}/api/v1/assessment/cycles/c1/board`, () => new HttpResponse(null, { status: 403 })));
    renderWithApi(<BoardView cycleId="c1" canCalibrate={false} />);

    expect(await screen.findByText('Could not load this page. Try again.')).toBeInTheDocument();
  });
});
