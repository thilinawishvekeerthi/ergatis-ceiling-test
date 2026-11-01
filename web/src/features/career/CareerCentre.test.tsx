import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { CareerCentreData, Opportunity } from '../../api/types';
import { server } from '../../test/server';
import { base, renderWithApi } from '../assessment/testUtils';
import { CareerCentre } from './CareerCentre';

const job = (id: string, title: string, level: number) => ({ id, code: title.toUpperCase().replace(/ /g, '_'), title, level });

const opportunity = (over: Partial<Opportunity> = {}): Opportunity => ({
  job: job('j2', 'Senior Developer', 4),
  steps: 1,
  matchPercent: 62.5,
  requirements: [
    { competencyCode: 'LEADERSHIP', requiredLevel: 3, currentLevel: 1, weight: 1, met: false, gap: 2 },
    { competencyCode: 'SQL', requiredLevel: 3, currentLevel: 3, weight: 1, met: true, gap: 0 },
  ],
  ...over,
});

const centre = (over: Partial<CareerCentreData> = {}): CareerCentreData => ({
  employeeId: 'e1',
  currentJob: job('j1', 'Developer', 3),
  competencies: [{ competencyCode: 'LEADERSHIP', level: 1 }, { competencyCode: 'SQL', level: 3 }],
  opportunities: [opportunity()],
  ...over,
});

const serve = (data: CareerCentreData) => server.use(http.get(`${base}/api/v1/career/me/centre`, () => HttpResponse.json(data)));

describe('CareerCentre', () => {
  it('shows the current job and competencies', async () => {
    serve(centre());
    renderWithApi(<CareerCentre />);

    expect(await screen.findByText('Developer')).toBeInTheDocument();
    expect(screen.getByText('(Level 3)')).toBeInTheDocument();
    expect(screen.getByText('LEADERSHIP: 1')).toBeInTheDocument();
    expect(screen.getByText('SQL: 3')).toBeInTheDocument();
  });

  it('shows each opportunity with its steps, % match and gap count', async () => {
    serve(centre());
    renderWithApi(<CareerCentre />);

    const card = await screen.findByRole('article', { name: 'Senior Developer' });
    expect(within(card).getByText('Next step')).toBeInTheDocument();
    expect(within(card).getByText('62.5% match')).toBeInTheDocument();
    expect(within(card).getByRole('progressbar', { name: 'Senior Developer match' })).toHaveAttribute('aria-valuenow', '62.5');
    expect(within(card).getByText('1 gap')).toBeInTheDocument();
  });

  it('lists every requirement, saying what is met and what is still needed', async () => {
    serve(centre());
    renderWithApi(<CareerCentre />);

    const card = await screen.findByRole('article', { name: 'Senior Developer' });
    const leadership = within(card).getByRole('row', { name: /LEADERSHIP/ });
    expect(within(leadership).getByText('2 more')).toBeInTheDocument();
    const sql = within(card).getByRole('row', { name: /SQL/ });
    expect(within(sql).getByText('Met')).toBeInTheDocument();
  });

  it('words the distance in steps', async () => {
    serve(centre({ opportunities: [opportunity({ steps: 1 }), opportunity({ job: job('j3', 'Principal', 6), steps: 3, matchPercent: 20, requirements: [] })] }));
    renderWithApi(<CareerCentre />);

    const far = await screen.findByRole('article', { name: 'Principal' });
    expect(within(far).getByText('3 steps away')).toBeInTheDocument();
    expect(within(far).queryByText(/gap/)).not.toBeInTheDocument();
  });

  it('says the role is open to anyone when the employee has no current job', async () => {
    serve(centre({ currentJob: null, opportunities: [opportunity({ steps: null })] }));
    renderWithApi(<CareerCentre />);

    expect(await screen.findByText(/No current job is set/)).toBeInTheDocument();
    const card = screen.getByRole('article', { name: 'Senior Developer' });
    expect(within(card).getByText('Open role')).toBeInTheDocument();
  });

  it('has friendly empty states', async () => {
    serve(centre({ competencies: [], opportunities: [] }));
    renderWithApi(<CareerCentre />);

    expect(await screen.findByText('No competency levels are recorded for you yet.')).toBeInTheDocument();
    expect(screen.getByText('No career moves are set up from your current job yet.')).toBeInTheDocument();
  });

  it('shows an error when the centre cannot be loaded', async () => {
    server.use(http.get(`${base}/api/v1/career/me/centre`, () => new HttpResponse(null, { status: 403 })));
    renderWithApi(<CareerCentre />);

    expect(await screen.findByText('Could not load this page. Try again.')).toBeInTheDocument();
  });
});
