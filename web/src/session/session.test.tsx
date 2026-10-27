import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AppRoutes } from '../AppRoutes';
import { server } from '../test/server';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient } from '@tanstack/react-query';
import { ApiProvider } from '../api/ApiProvider';
import { createApiClient } from '../api/client';
import { SessionProvider, toSession } from './SessionProvider';

const base = 'http://localhost';

/** Renders with the real client against MSW, the real session provider and a memory router. */
function renderApp(ui: ReactElement, { path = '/' }: { path?: string } = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const client = createApiClient({ baseUrl: base, getAntiForgeryToken: () => 'tok' });
  return render(
    <ApiProvider client={client} queryClient={queryClient}>
      <SessionProvider>
        <MemoryRouter initialEntries={[path]}>{ui}</MemoryRouter>
      </SessionProvider>
    </ApiProvider>,
  );
}

describe('session', () => {
  it('maps server role codes to portals and drops roles without a portal', () => {
    expect(toSession({ userId: 'u', roles: ['HR_ADMIN', 'TALENT_COMMITTEE', 'MANAGER'], hasEmployee: true }).roles)
      .toEqual(['hr-admin', 'manager']);
  });

  it('signs in through the BFF and then lists the portals of the user', async () => {
    let signedIn = false;
    server.use(
      http.get(`${base}/bff/user`, () =>
        signedIn ? HttpResponse.json({ userId: 'dev.manager', roles: ['MANAGER'], hasEmployee: true }) : new HttpResponse(null, { status: 401 })),
      http.post(`${base}/bff/fake-login`, async ({ request }) => {
        signedIn = ((await request.json()) as { subject: string }).subject === 'dev.manager';
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderApp(<AppRoutes />);
    await userEvent.click(await screen.findByRole('button', { name: /dev\.manager/ }));
    expect(await screen.findByRole('link', { name: 'My team' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'HR administration' })).not.toBeInTheDocument();
  });

  it('sends a signed-out user from a portal back to sign-in', async () => {
    server.use(http.get(`${base}/bff/user`, () => new HttpResponse(null, { status: 401 })));
    renderApp(<AppRoutes />, { path: '/admin/scales' });
    expect(await screen.findByText(/Development sign-in/)).toBeInTheDocument();
  });

  it('shows no access when the role is missing', async () => {
    server.use(http.get(`${base}/bff/user`, () => HttpResponse.json({ userId: 'dev.employee', roles: ['EMPLOYEE'], hasEmployee: true })));
    renderApp(<AppRoutes />, { path: '/admin/scales' });
    expect(await screen.findByRole('alert')).toHaveTextContent(/do not have access/);
  });
});
