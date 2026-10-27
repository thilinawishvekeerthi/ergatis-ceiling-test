import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from './context';
import type { ConfigType, GridDefinition, ScalePayload, StartWorkflow } from './types';

export const keys = {
  configs: (type: ConfigType) => ['config', type] as const,
  version: (id: string) => ['config', 'version', id] as const,
  myWorkflows: ['workflows', 'mine'] as const,
  inbox: ['workflows', 'inbox'] as const,
};

export function useConfigs(type: ConfigType) {
  const api = useApi();
  return useQuery({ queryKey: keys.configs(type), queryFn: () => api.listConfigs(type) });
}

export function useScale(id: string) {
  const api = useApi();
  return useQuery({ queryKey: keys.version(id), queryFn: () => api.getScale(id) });
}

export function useGrid(id: string) {
  const api = useApi();
  return useQuery({ queryKey: keys.version(id), queryFn: () => api.getGrid(id) });
}

export function useSaveScale(id: string) {
  const api = useApi();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: ScalePayload) => api.saveScaleDraft(id, payload),
    onSuccess: (data) => {
      qc.setQueryData(keys.version(id), data);
      void qc.invalidateQueries({ queryKey: keys.configs('Scale') });
    },
  });
}

export function useSaveGrid(id: string) {
  const api = useApi();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (grid: GridDefinition) => api.saveGridDraft(id, grid),
    onSuccess: (data) => {
      qc.setQueryData(keys.version(id), data);
      void qc.invalidateQueries({ queryKey: keys.configs('Grid') });
    },
  });
}

export function usePublish(type: ConfigType, id: string) {
  const api = useApi();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.publish(id),
    onSuccess: (data) => {
      qc.setQueryData(keys.version(id), data);
      void qc.invalidateQueries({ queryKey: keys.configs(type) });
    },
  });
}

export function useNewVersion(type: ConfigType) {
  const api = useApi();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (code: string) => api.newVersion(type, code),
    onSuccess: () => void qc.invalidateQueries({ queryKey: keys.configs(type) }),
  });
}

export function useMyWorkflows() {
  const api = useApi();
  return useQuery({ queryKey: keys.myWorkflows, queryFn: () => api.myWorkflows() });
}

export function useInbox() {
  const api = useApi();
  return useQuery({ queryKey: keys.inbox, queryFn: () => api.inbox() });
}

/** Starts a workflow and, when given, applies a first action (for example Submit) in one step for the user. */
export function useStartWorkflow() {
  const api = useApi();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ start, then }: { start: StartWorkflow; then?: string }) => {
      const started = await api.startWorkflow(start);
      return then ? api.act(started.id, then) : started;
    },
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['workflows'] }),
  });
}

export function useAct() {
  const api = useApi();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, action, comment }: { id: string; action: string; comment?: string }) => api.act(id, action, comment),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['workflows'] }),
  });
}
