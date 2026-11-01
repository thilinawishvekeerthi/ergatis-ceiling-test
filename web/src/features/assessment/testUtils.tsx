import type { ReactElement } from 'react';
import { QueryClient } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ApiProvider } from '../../api/ApiProvider';
import { createApiClient } from '../../api/client';

export const base = 'http://localhost';

/** Renders with the real API client pointed at MSW, a fresh query cache and a memory router. */
export function renderWithApi(ui: ReactElement, route = '/') {
  const client = createApiClient({ baseUrl: base, getAntiForgeryToken: () => 'tok' });
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <ApiProvider client={client} queryClient={queryClient}>
      <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
    </ApiProvider>,
  );
}
