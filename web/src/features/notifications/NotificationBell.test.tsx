import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { useLocation } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import type { AppNotification } from '../../api/types';
import { server } from '../../test/server';
import { base, renderWithApi } from '../assessment/testUtils';
import { NotificationBell } from './NotificationBell';

const alert = (over: Partial<AppNotification> = {}): AppNotification => ({
  id: 'n1',
  eventKey: 'assessment.cycle-opened',
  title: '2026 annual is open',
  body: 'Please rate the people assigned to you.',
  linkUrl: '/manager',
  createdAt: '2026-03-01T08:00:00Z',
  isRead: false,
  ...over,
});

function serve(state: { items: AppNotification[] }) {
  const unread = () => state.items.filter((n) => !n.isRead).length;
  server.use(
    http.get(`${base}/api/v1/notifications/me/unread-count`, () => HttpResponse.json({ count: unread() })),
    http.get(`${base}/api/v1/notifications/me`, () => HttpResponse.json({ unreadCount: unread(), items: state.items })),
    http.post(`${base}/api/v1/notifications/me/read-all`, () => {
      state.items = state.items.map((n) => ({ ...n, isRead: true }));
      return HttpResponse.json({ count: 0 });
    }),
    http.post(`${base}/api/v1/notifications/me/:id/read`, ({ params }) => {
      state.items = state.items.map((n) => (n.id === params.id ? { ...n, isRead: true } : n));
      return HttpResponse.json(state.items.find((n) => n.id === params.id));
    }),
  );
}

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>;
}

const renderBell = () => renderWithApi(<><NotificationBell /><Where /></>, '/start');

describe('NotificationBell', () => {
  it('shows the unread count on the bell and says it in the label', async () => {
    serve({ items: [alert(), alert({ id: 'n2' })] });
    renderBell();

    expect(await screen.findByRole('button', { name: 'Notifications, 2 unread' })).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('shows no badge when everything is read', async () => {
    serve({ items: [alert({ isRead: true })] });
    renderBell();

    const bell = await screen.findByRole('button', { name: 'Notifications' });
    expect(bell).not.toHaveTextContent('0');
  });

  it('caps a large count at 99+', async () => {
    server.use(http.get(`${base}/api/v1/notifications/me/unread-count`, () => HttpResponse.json({ count: 250 })));
    renderBell();

    expect(await screen.findByText('99+')).toBeInTheDocument();
  });

  it('still shows a plain bell when the count cannot be loaded', async () => {
    server.use(http.get(`${base}/api/v1/notifications/me/unread-count`, () => new HttpResponse(null, { status: 500 })));
    renderBell();

    expect(await screen.findByRole('button', { name: 'Notifications' })).toBeInTheDocument();
  });

  it('opens a list of the latest alerts, with unread ones marked', async () => {
    serve({ items: [alert(), alert({ id: 'n2', title: 'Your career profile was updated', isRead: true })] });
    const user = userEvent.setup();
    renderBell();

    await user.click(await screen.findByRole('button', { name: /Notifications/ }));

    const dialog = await screen.findByRole('dialog', { name: 'Notifications' });
    expect(await within(dialog).findByRole('button', { name: 'Unread: 2026 annual is open' })).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Your career profile was updated' })).toBeInTheDocument();
  });

  it('says so when there is nothing to show', async () => {
    serve({ items: [] });
    const user = userEvent.setup();
    renderBell();

    await user.click(await screen.findByRole('button', { name: /Notifications/ }));

    expect(await screen.findByText('No notifications yet.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Mark all as read' })).toBeDisabled();
  });

  it('opening an alert marks it read, goes to its link and closes the list', async () => {
    const state = { items: [alert()] };
    serve(state);
    const user = userEvent.setup();
    renderBell();

    await user.click(await screen.findByRole('button', { name: /Notifications, 1 unread/ }));
    await user.click(await screen.findByRole('button', { name: 'Unread: 2026 annual is open' }));

    expect(screen.getByTestId('where')).toHaveTextContent('/manager');
    await waitFor(() => expect(state.items[0]?.isRead).toBe(true));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByRole('button', { name: 'Notifications' })).toBeInTheDocument();
  });

  it('does not navigate when an alert has no link', async () => {
    serve({ items: [alert({ linkUrl: null })] });
    const user = userEvent.setup();
    renderBell();

    await user.click(await screen.findByRole('button', { name: /Notifications/ }));
    await user.click(await screen.findByRole('button', { name: /2026 annual is open/ }));

    expect(screen.getByTestId('where')).toHaveTextContent('/start');
  });

  it('marks everything read and the count drops to nothing', async () => {
    serve({ items: [alert(), alert({ id: 'n2' })] });
    const user = userEvent.setup();
    renderBell();

    await user.click(await screen.findByRole('button', { name: 'Notifications, 2 unread' }));
    await user.click(await screen.findByRole('button', { name: 'Mark all as read' }));

    // The list stays open, so the bell sits behind it: look it up including hidden elements.
    expect(await screen.findByRole('button', { name: 'Notifications', hidden: true })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Mark all as read' })).toBeDisabled());
  });

  it('shows an error inside the list when the alerts cannot be loaded', async () => {
    server.use(
      http.get(`${base}/api/v1/notifications/me/unread-count`, () => HttpResponse.json({ count: 1 })),
      http.get(`${base}/api/v1/notifications/me`, () => new HttpResponse(null, { status: 500 })),
    );
    const user = userEvent.setup();
    renderBell();

    await user.click(await screen.findByRole('button', { name: /Notifications/ }));

    expect(await screen.findByText('Could not load your notifications.')).toBeInTheDocument();
  });
});
