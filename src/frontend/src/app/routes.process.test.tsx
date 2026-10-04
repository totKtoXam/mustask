import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { AuthContext, type AuthContextValue } from '../features/auth/AuthContext';
import {
  DEMONSTRATION_ORGANIZATION_ID,
  PROCESS_DEFINITION_URL,
  sampleProcessDefinition,
} from '../features/process/testFixtures';
import { server } from '../test/mocks/server';
import { authValue, LANGUAGES_READ, LANGUAGES_WRITE, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

const PROCESS_ADDRESS = `/organizations/${DEMONSTRATION_ORGANIZATION_ID}/settings/process`;
const everyCode = [LANGUAGES_READ, LANGUAGES_WRITE, 'process.definition.read'];

const readUrls: string[] = [];
const onRequest = ({ request }: { request: Request }) => {
  if (request.url.includes('/api/organizations/')) {
    readUrls.push(request.url);
  }
};

beforeEach(() => {
  readUrls.length = 0;
  server.events.on('request:start', onRequest);
});

afterEach(() => {
  server.events.removeListener('request:start', onRequest);
});

function serveProcess() {
  server.use(http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(sampleProcessDefinition)));
}

function serveNotFound() {
  server.use(
    http.get(PROCESS_DEFINITION_URL, () =>
      HttpResponse.json(
        {
          status: 404,
          detail: 'Организация не найдена.',
          errorCode: 'organizations.organization.not_found',
        },
        { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
      ),
    ),
  );
}

/** The real routes under a stub session. */
function renderRoutes(path: string, auth: AuthContextValue = authValue(everyCode)) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={auth}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
          <LocationProbe />
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}

describe('mod-001 US-2 Scenario: A seeded process is shown in full — in the mod-015 shell', () => {
  it('renders inside the shell with exactly one h1 «Процесс» and the tab «Процесс — Mustask»', async () => {
    serveProcess();
    renderRoutes(PROCESS_ADDRESS);

    const main = screen.getByRole('main');
    expect(await within(main).findByRole('region', { name: 'Эпик' })).toBeInTheDocument();
    expect(screen.getByRole('banner')).toBeInTheDocument();
    const title = screen.getByRole('heading', { level: 1, name: 'Процесс' });
    expect(screen.getAllByRole('heading', { level: 1 })).toEqual([title]);
    expect(within(main).getAllByRole('region')).toHaveLength(5);
    await waitFor(() => expect(document.title).toBe('Процесс — Mustask'));
  });
});

describe('mod-001 US-2 Scenario: No visible entry or link leads to the screen before mod-002', () => {
  it('opening the address with a seeded identifier shows the process', async () => {
    serveProcess();
    renderRoutes(PROCESS_ADDRESS, authValue([]));

    expect(await screen.findByRole('region', { name: 'Задача' })).toBeInTheDocument();
    expect(readUrls).toEqual([
      `http://localhost:5000/api/organizations/${DEMONSTRATION_ORGANIZATION_ID}/process`,
    ]);
  });
});

describe('mod-001 US-2 Scenario: Every signed-in user sees the same definition', () => {
  it('shows the identical page to a user holding every code and to one holding none', async () => {
    serveProcess();
    const first = renderRoutes(PROCESS_ADDRESS, authValue(everyCode));
    await screen.findByRole('region', { name: 'Задача' });
    const withEveryCode = screen.getByRole('main').textContent;
    first.unmount();

    renderRoutes(PROCESS_ADDRESS, authValue([]));
    await screen.findByRole('region', { name: 'Задача' });

    expect(screen.getByRole('main').textContent).toBe(withEveryCode);
    expect(screen.queryByText('Доступ ограничен')).not.toBeInTheDocument();
  });
});

describe('mod-001 US-2 Scenario: An identifier with no process is not found — in the shell', () => {
  it('shows the frame with no «Меню настроек», no entry marked current and no trail; «На главную» opens «Главная»', async () => {
    serveNotFound();
    renderRoutes('/organizations/0b9f7f9e-4c55-4d5b-9a51-0d6c3a7e2f10/settings/process');

    expect(
      await within(screen.getByRole('main')).findByRole('heading', {
        level: 1,
        name: 'Организация не найдена',
      }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).not.toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).not.toBeInTheDocument();
    for (const navigation of screen.getAllByRole('navigation')) {
      expect(navigation.querySelectorAll('[aria-current]')).toHaveLength(0);
    }
    await waitFor(() => expect(document.title).toBe('Организация не найдена — Mustask'));

    await userEvent.click(screen.getByRole('link', { name: 'На главную' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Главная' })).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/home');
  });
});

describe('mod-001 US-2 Scenario: A visitor who is not signed in sees nothing', () => {
  it('gets the sign-in screen, no part of the page, and no request for the process', async () => {
    serveProcess();
    renderRoutes(
      PROCESS_ADDRESS,
      authValue([], { status: 'anonymous', reason: 'signed_out', displayName: '' }),
    );

    expect(await screen.findByRole('button', { name: 'Войти' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Процесс' })).not.toBeInTheDocument();
    expect(screen.queryByRole('main')).not.toBeInTheDocument();
    expect(screen.queryByText('Эпик')).not.toBeInTheDocument();
    expect(readUrls).toEqual([]);
  });
});
