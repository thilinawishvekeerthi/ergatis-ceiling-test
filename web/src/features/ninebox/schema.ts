import { z } from 'zod';

/** Mirrors NineBoxRules on the server: each level is a whole number from 1 to 3. The server checks again. */
export const LEVEL_MIN = 1;
export const LEVEL_MAX = 3;

const level = (what: string) =>
  z
    .number({ error: `Choose a ${what} level` })
    .int()
    .min(LEVEL_MIN, `${what} must be between ${LEVEL_MIN} and ${LEVEL_MAX}`)
    .max(LEVEL_MAX, `${what} must be between ${LEVEL_MIN} and ${LEVEL_MAX}`);

export const evaluationSchema = z.object({
  performance: level('Performance'),
  potential: level('Potential'),
});

export type EvaluationForm = z.infer<typeof evaluationSchema>;
