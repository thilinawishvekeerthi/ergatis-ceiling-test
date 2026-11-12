import { http, HttpResponse } from 'msw';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { ApiProvider } from '../../api/ApiProvider';
import { createApiClient } from '../../api/client';
import type { EmployeePlacement, Evaluation, NineBoxGrid } from '../../api/types';
import { server } from '../../test/server';
import { NineBoxPage } from './NineBoxPage';
import { evaluationSchema } from './schema';

const base = 'http://localhost';
const root = `${base}/api/v1/assessment/nine-box`;

const labelsByBox = ['Risk', 'Effective', 'Trusted pro', 'Inconsistent', 'Core player', 'High performer', 'Potential gem', 'High potential', 'Star'];
const grid: NineBoxGrid = {
  gridVersionId: 'g1',
  performanceLevels: [1, 2, 3].map((level) => ({ level, label: ['Low', 'Medium', 'High'][level - 1], description: `Perf rubric ${level}` })),
  potentialLevels: [1, 2, 3].map((level) => ({ level, label: ['Low', 'Medium', 'High'][level - 1], description: `Pot rubric ${level}` })),
  columnBounds: [1, 2, 3],
  rowBounds: [1, 2, 3],
  cells: labelsByBox.map((label, i) => ({ boxNumber: i + 1, column: i % 3, row: Math.floor(i / 3), label, colour: '#cccccc', band: 'HIGH' })),
};

const ada: EmployeePlacement = { employeeId: 'e1', employeeNumber: 'E00001', name: 'Ada Lovelace', position: 'Analyst', department: 'Data', latest: null };

const star: Evaluation = {
  evaluationId: 'v1', employeeId: 'e1', performance: 3, potential: 3, boxNumber: 9, label: 'Star', talentScore: 9, band: 'HIGH',
  gridVersionId: 'g1', evaluatedBy: 'dev.admin', evaluatedAt: '2026-11-12T10:00:00Z',
};

function renderPage() {
  const client = createApiClient({ baseUrl: base, getAntiForgeryToken: () => 'tok' });
  return render(<ApiProvider client={client}><NineBoxPage /></ApiProvider>);
}

describe('evaluation schema', () => {
  it('accepts levels 1 to 3 and rejects anything else', () => {
    expect(evaluationSchema.safeParse({ performance: 3, potential: 1 }).success).toBe(true);
    expect(evaluationSchema.safeParse({ performance: 0, potential: 1 }).success).toBe(false);
    expect(evaluationSchema.safeParse({ performance: 2, potential: 4 }).success).toBe(false);
    expect(evaluationSchema.safeParse({ performance: 2 }).success).toBe(false);
  });
});

describe('NineBoxPage', () => {
  it('evaluates the chosen employee and shows the saved placement', async () => {
    let latest: Evaluation | null = null;
    let body: unknown;
    server.use(
      http.get(`${root}/grid`, () => HttpResponse.json(grid)),
      http.get(`${root}/employees`, () => HttpResponse.json({ items: [{ ...ada, latest }], page: 1, pageSize: 100, totalCount: 1 })),
      http.get(`${root}/employees/e1/evaluations`, () =>
        HttpResponse.json({ items: latest ? [latest] : [], page: 1, pageSize: 20, totalCount: latest ? 1 : 0 })),
      http.post(`${root}/evaluations`, async ({ request }) => {
        body = await request.json();
        latest = star;
        return HttpResponse.json(star, { status: 201 });
      }),
    );
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByText('Ada Lovelace'));
    const form = screen.getByRole('form', { name: 'Evaluate Ada Lovelace' });
    expect(within(form).getByText(/Perf rubric 3/)).toBeInTheDocument();
    await user.click(within(within(form).getByRole('group', { name: 'Performance' })).getByRole('radio', { name: /3 · High/ }));
    await user.click(within(within(form).getByRole('group', { name: 'Potential' })).getByRole('radio', { name: /3 · High/ }));
    await user.click(within(form).getByRole('button', { name: 'Save evaluation' }));

    expect((await screen.findByText(/^Saved/)).closest('[role="status"]')).toHaveTextContent('Star · box 9 · score 9 · High band');
    expect(body).toEqual({ employeeId: 'e1', performance: 3, potential: 3 });
    // The grid and the history both show the new placement once the queries refresh.
    expect(await screen.findByRole('gridcell', { name: 'Star, 1 person' })).toBeInTheDocument();
    expect(await screen.findByRole('table', { name: 'Evaluation history' })).toHaveTextContent('dev.admin');
  });

  it('shows the field message when nothing is chosen, without calling the API', async () => {
    server.use(
      http.get(`${root}/grid`, () => HttpResponse.json(grid)),
      http.get(`${root}/employees`, () => HttpResponse.json({ items: [ada], page: 1, pageSize: 100, totalCount: 1 })),
      http.get(`${root}/employees/e1/evaluations`, () => HttpResponse.json({ items: [], page: 1, pageSize: 20, totalCount: 0 })),
    );
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByText('Ada Lovelace'));
    await user.click(screen.getByRole('button', { name: 'Save evaluation' }));

    expect(await screen.findByText('Choose a Performance level')).toBeInTheDocument();
    expect(screen.getByText('Choose a Potential level')).toBeInTheDocument();
  });

  it('shows the no-access message on 403', async () => {
    server.use(
      http.get(`${root}/grid`, () => HttpResponse.json({ detail: 'No' }, { status: 403 })),
      http.get(`${root}/employees`, () => HttpResponse.json({ detail: 'No' }, { status: 403 })),
    );
    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('You do not have access');
  });
});
