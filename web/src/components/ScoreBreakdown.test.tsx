import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ScoreBreakdown, contribution } from './ScoreBreakdown';

const data = {
  title: 'Profile match',
  total: 78.5,
  max: 100,
  components: [
    { key: 'skills', label: 'Skills', weight: 0.6, score: 80 },
    { key: 'exp', label: 'Experience', weight: 0.4, score: 77.5, note: 'Capped at 10 years' },
  ],
};

describe('ScoreBreakdown', () => {
  it('shows a row per component with weight, score and contribution', () => {
    render(<ScoreBreakdown data={data} />);
    const row = screen.getByRole('row', { name: /skills/i });
    expect(within(row).getByText('60%')).toBeInTheDocument();
    expect(within(row).getByText('80')).toBeInTheDocument();
    expect(within(row).getByText('48')).toBeInTheDocument();
    expect(screen.getByText('Capped at 10 years')).toBeInTheDocument();
  });

  it('shows the total out of the maximum', () => {
    render(<ScoreBreakdown data={data} />);
    expect(screen.getByText('78.5')).toBeInTheDocument();
    expect(screen.getByText(/out of 100/)).toBeInTheDocument();
  });

  it('rounds a contribution to 2 decimals', () => {
    expect(contribution(0.333, 10)).toBe(3.33);
  });
});
