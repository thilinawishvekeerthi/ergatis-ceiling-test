import { useMemo, type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createAntiForgeryTokenSource, createApiClient, type ApiClient } from './client';
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
  const qc = useMemo(
    () => queryClient ?? new QueryClient({ defaultOptions: { queries: { retry: false } } }),
    [queryClient],
  );
  const api = useMemo(() => {
    if (client) return client;
    const token = createAntiForgeryTokenSource();
    return createApiClient({
      // The session ended on the server: drop cached data and show the sign-in page.
      onUnauthenticated: () => void qc.resetQueries({ queryKey: ['session'] }),
      getAntiForgeryToken: token,
      onSessionChanged: () => {
        token.reset();
        // Another user's data must not survive a sign-in. The session query refetches itself.
        qc.removeQueries({ predicate: (q) => q.queryKey[0] !== 'session' });
      },
    });
  }, [client, qc]);
  return (
    <QueryClientProvider client={qc}>
      <ApiContext.Provider value={api}>{children}</ApiContext.Provider>
    </QueryClientProvider>
  );
}
