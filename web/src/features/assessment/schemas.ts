import { z } from 'zod';
import { labels } from '../../labels';

/** Selects hold strings. The form turns them into numbers on submit. */
export const ratingSchema = z.object({
  performanceLevel: z.string().min(1, labels.assessment.chooseLevel),
  potentialLevel: z.string().min(1, labels.assessment.chooseLevel),
  cell: z.string().optional(),
  comment: z.string().max(2000, 'The comment can be at most 2000 characters.').optional(),
});
export type RatingForm = z.infer<typeof ratingSchema>;

export const calibrationSchema = z.object({
  reason: z.string().trim().min(1, labels.assessment.reasonRequired).max(2000, 'The reason can be at most 2000 characters.'),
});
export type CalibrationForm = z.infer<typeof calibrationSchema>;

/** Employee names come from the synced HR copy. Until it has a row, show a short id so people can still be told apart. */
export const personName = (name: string | null, employeeId: string) => name ?? `Employee ${employeeId.slice(-4)}`;
