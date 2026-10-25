import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from './context';
import type { GridDefinition, ScaleLevel } from './types';

export const keys = {
  scales: ['config', 'scales'] as const,
  scale: (id: string) => ['config', 'scales', id] as const,
  grids: ['config', 'grids'] as const,
  grid: (id: string) => ['config', 'grids', id] as const,
};

export function useScales() {
  const api = useApi();
  return useQuery({ queryKey: keys.scales, queryFn: () => api.listScales() });
}

export function useScale(id: string) {
  const api = useApi();
  return useQuery({ queryKey: keys.scale(id), queryFn: () => api.getScale(id) });
}

export function useSaveScale(id: string) {
  const api = useApi();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (levels: ScaleLevel[]) => api.saveScaleDraft(id, levels),
    onSuccess: (data) => {
      qc.setQueryData(keys.scale(id), data);
      void qc.invalidateQueries({ queryKey: keys.scales });
    },
  });
}

export function useGrids() {
  const api = useApi();
  return useQuery({ queryKey: keys.grids, queryFn: () => api.listGrids() });
}

export function useGrid(id: string) {
  const api = useApi();
  return useQuery({ queryKey: keys.grid(id), queryFn: () => api.getGrid(id) });
}

export function useSaveGrid(id: string) {
  const api = useApi();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (grid: GridDefinition) => api.saveGridDraft(id, grid),
    onSuccess: (data) => {
      qc.setQueryData(keys.grid(id), data);
      void qc.invalidateQueries({ queryKey: keys.grids });
    },
  });
}
