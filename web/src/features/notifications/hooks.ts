import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from '../../api/context';

export const notificationKeys = {
  all: ['notifications'] as const,
  unread: ['notifications', 'unread'] as const,
  inbox: ['notifications', 'inbox'] as const,
};

/** How often the bell asks for a fresh unread count. */
export const UNREAD_POLL_MS = 30_000;

export function useUnreadCount() {
  const api = useApi();
  return useQuery({ queryKey: notificationKeys.unread, queryFn: () => api.getUnreadCount(), refetchInterval: UNREAD_POLL_MS });
}

export function useInbox(enabled: boolean) {
  const api = useApi();
  return useQuery({ queryKey: notificationKeys.inbox, queryFn: () => api.getInbox(10), enabled });
}

export function useMarkRead() {
  const api = useApi();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.markNotificationRead(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: notificationKeys.all }),
  });
}

export function useMarkAllRead() {
  const api = useApi();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.markAllNotificationsRead(),
    onSuccess: () => qc.invalidateQueries({ queryKey: notificationKeys.all }),
  });
}
