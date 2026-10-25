import { useMemo, type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createApiClient, type ApiClient } from './client';
import { ApiContext } from './context';

export function ApiProvider({
  client,
  queryClient,
  children,
}: {
  client?: ApiClient;
  queryClient?: QueryClient;
  children: ReactNode;
}) {
  const api = useMemo(
    () => client ?? createApiClient({ onUnauthenticated: () => window.location.assign('/bff/login') }),
    [client],
  );
  const qc = useMemo(
    () => queryClient ?? new QueryClient({ defaultOptions: { queries: { retry: false } } }),
    [queryClient],
  );
  return (
    <QueryClientProvider client={qc}>
      <ApiContext.Provider value={api}>{children}</ApiContext.Provider>
    </QueryClientProvider>
  );
}
