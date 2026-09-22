import type { ReactElement, ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';

/**
 * Рендер с провайдерами страничных тестов: свежий QueryClient без ретраев
 * (ошибки всплывают сразу) и MemoryRouter.
 */
export function renderWithProviders(
  ui: ReactElement,
  {
    initialEntries = ['/'],
    wrapper,
  }: { initialEntries?: string[]; wrapper?: (children: ReactNode) => ReactElement } = {},
) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  // `wrapper` (обычно `AuthProvider`) — внутри QueryClientProvider, а не
  // снаружи: AuthContext сам зовёт useQueryClient() (сброс кэша
  // на выходе), и снаружи провайдера этот hook падает.
  const routed = <MemoryRouter initialEntries={initialEntries}>{ui}</MemoryRouter>;
  const tree = <QueryClientProvider client={queryClient}>{wrapper ? wrapper(routed) : routed}</QueryClientProvider>;
  return render(tree);
}
