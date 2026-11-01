import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from '../../api/context';
import type { RatingInput } from '../../api/types';

export const assessmentKeys = {
  all: ['assessment'] as const,
  cycles: ['assessment', 'cycles'] as const,
  reviews: (cycleId: string) => ['assessment', 'reviews', cycleId] as const,
  board: (cycleId: string) => ['assessment', 'board', cycleId] as const,
};

export function useCycles() {
  const api = useApi();
  return useQuery({ queryKey: assessmentKeys.cycles, queryFn: () => api.listCycles() });
}

export function useReviews(cycleId: string | undefined) {
  const api = useApi();
  return useQuery({ queryKey: assessmentKeys.reviews(cycleId ?? ''), queryFn: () => api.listReviews(cycleId!), enabled: !!cycleId });
}

export function useBoard(cycleId: string | undefined) {
  const api = useApi();
  return useQuery({ queryKey: assessmentKeys.board(cycleId ?? ''), queryFn: () => api.getBoard(cycleId!), enabled: !!cycleId });
}

/** Every write refreshes all assessment data: a rating changes the list, the board and the cycle counts together. */
function useInvalidate() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: assessmentKeys.all });
}

export function useRate(reviewId: string) {
  const api = useApi();
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (rating: RatingInput) => api.rate(reviewId, rating), onSuccess: invalidate });
}

export function useCalibrate() {
  const api = useApi();
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (v: { reviewId: string; to: { column: number; row: number }; reason: string }) => api.calibrate(v.reviewId, v.to, v.reason),
    onSuccess: invalidate,
  });
}

export function useMoveCycle() {
  const api = useApi();
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (v: { id: string; action: 'open' | 'calibration' | 'close' }) =>
      v.action === 'open' ? api.openCycle(v.id) : v.action === 'calibration' ? api.startCalibration(v.id) : api.closeCycle(v.id),
    onSuccess: invalidate,
  });
}
