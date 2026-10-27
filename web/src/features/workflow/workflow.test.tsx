import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { InboxItem, WorkflowInstance } from '../../api/types';
import { server } from '../../test/server';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient } from '@tanstack/react-query';
import { ApiProvider } from '../../api/ApiProvider';
import { createApiClient } from '../../api/client';
import { SessionProvider } from '../../session/SessionProvider';

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
import { Approvals } from './Approvals';
import { MyPlans } from './MyPlans';

const user = (roles: string[]) => http.get(`${base}/bff/user`, () => HttpResponse.json({ userId: 'u', roles, hasEmployee: true }));

const instance = (over: Partial<WorkflowInstance> = {}): WorkflowInstance => ({
  id: 'w1', definitionVersionId: 'v1', definitionCode: 'IDP_APPROVAL', subjectType: 'Idp', subjectId: 's1',
  currentState: 'PendingManager', status: 'Active', availableActions: ['Approve', 'Reject'],
  openTask: { id: 't1', instanceId: 'w1', state: 'PendingManager', status: 'Pending', dueAt: null, escalationLevel: 0, assignees: ['dev.manager'] },
  history: [{ sequence: 1, fromState: 'Draft', toState: 'PendingManager', action: 'Submit', actor: 'dev.employee', comment: null, occurredAt: '2026-10-27T10:00:00Z' }],
  startedBy: 'dev.employee', startedAt: '2026-10-27T10:00:00Z', context: { title: 'AWS course', estimatedCost: '400' },
  ...over,
});

describe('MyPlans', () => {
  it('starts an IDP approval about the caller and submits it', async () => {
    const calls: unknown[] = [];
    let created = false;
    server.use(
      user(['EMPLOYEE']),
      http.get(`${base}/api/v1/workflows/mine`, () => HttpResponse.json(created ? [instance()] : [])),
      http.post(`${base}/api/v1/workflows`, async ({ request }) => {
        calls.push(await request.json());
        return HttpResponse.json(instance({ currentState: 'Draft', openTask: null }), { status: 201 });
      }),
      http.post(`${base}/api/v1/workflows/w1/actions`, async ({ request }) => {
        calls.push(await request.json());
        created = true;
        return HttpResponse.json(instance());
      }),
    );
    renderApp(<MyPlans />);
    expect(await screen.findByText(/no development plans/)).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText('Plan title'), 'AWS course');
    await userEvent.clear(screen.getByLabelText('Estimated cost'));
    await userEvent.type(screen.getByLabelText('Estimated cost'), '400');
    await userEvent.click(screen.getByRole('button', { name: 'Submit for approval' }));

    expect(await screen.findByRole('article', { name: 'AWS course' })).toHaveTextContent('Waiting for: dev.manager');
    expect(calls).toEqual([
      { definitionCode: 'IDP_APPROVAL', subjectType: 'Idp', context: { title: 'AWS course', estimatedCost: '400' } },
      { action: 'Submit', comment: null },
    ]);
  });

  it('does not send a plan without a title', async () => {
    server.use(user(['EMPLOYEE']), http.get(`${base}/api/v1/workflows/mine`, () => HttpResponse.json([])));
    renderApp(<MyPlans />);
    await userEvent.click(await screen.findByRole('button', { name: 'Submit for approval' }));
    expect(await screen.findByText('At least 3 characters.')).toBeInTheDocument();
  });
});

describe('Approvals', () => {
  const item: InboxItem = {
    task: instance().openTask!, definitionCode: 'IDP_APPROVAL', subjectType: 'Idp', subjectId: 's1',
    startedBy: 'dev.employee', context: { title: 'AWS course', estimatedCost: '400' },
  };

  it('needs a comment to reject, then approves with one click', async () => {
    const actions: unknown[] = [];
    let done = false;
    server.use(
      user(['MANAGER']),
      http.get(`${base}/api/v1/workflows/inbox`, () => HttpResponse.json(done ? [] : [item])),
      http.post(`${base}/api/v1/workflows/w1/actions`, async ({ request }) => {
        actions.push(await request.json());
        done = true;
        return HttpResponse.json(instance({ currentState: 'Approved', status: 'Completed', openTask: null }));
      }),
    );
    renderApp(<Approvals />);
    const card = await screen.findByRole('article', { name: 'AWS course' });
    expect(card).toHaveTextContent('from dev.employee, estimated cost 400');

    await userEvent.click(screen.getByRole('button', { name: 'Reject' }));
    expect(screen.getByText('A comment is required to reject.')).toBeInTheDocument();
    expect(actions).toEqual([]);

    await userEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(await screen.findByText('Nothing waiting for you.')).toBeInTheDocument();
    expect(actions).toEqual([{ action: 'Approve', comment: '' }]);
  });
});
