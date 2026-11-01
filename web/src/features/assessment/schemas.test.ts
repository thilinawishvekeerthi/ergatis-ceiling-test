import { describe, expect, it } from 'vitest';
import { calibrationSchema, personName, ratingSchema } from './schemas';

describe('rating schema', () => {
  it('needs both levels', () => {
    const result = ratingSchema.safeParse({ performanceLevel: '', potentialLevel: '2' });
    expect(result.success).toBe(false);
    expect(result.error?.issues.map((i) => i.path[0])).toEqual(['performanceLevel']);
  });

  it('accepts levels with a comment', () => {
    expect(ratingSchema.safeParse({ performanceLevel: '4', potentialLevel: '2', comment: 'Strong year' }).success).toBe(true);
  });

  it('rejects a comment over 2000 characters', () => {
    expect(ratingSchema.safeParse({ performanceLevel: '4', potentialLevel: '2', comment: 'x'.repeat(2001) }).success).toBe(false);
  });
});

describe('calibration schema', () => {
  it('needs a reason, ignoring blanks', () => {
    expect(calibrationSchema.safeParse({ reason: '   ' }).success).toBe(false);
    expect(calibrationSchema.safeParse({ reason: 'Led the migration' }).success).toBe(true);
  });
});

describe('personName', () => {
  it('uses the name when known and a short id otherwise', () => {
    expect(personName('Ada Lovelace', 'x')).toBe('Ada Lovelace');
    expect(personName(null, '00000000-0000-0000-0000-0000000000a3')).toBe('Employee 00a3');
  });
});
