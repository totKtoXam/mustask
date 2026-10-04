import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { AuthContext, AuthProvider, type AuthContextValue } from '../features/auth/AuthContext';
import { sampleLanguages } from '../features/dictionaries/testFixtures';
import { setNavigateImpl } from '../shared/api/session';
import { server } from '../test/mocks/server';
import { authValue, LANGUAGES_READ, LANGUAGES_WRITE, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

const api = 'http://localhost:5000';
const languagesUrl = `${api}/api/dictionaries/languages`;
const administrator = [LANGUAGES_READ, LANGUAGES_WRITE];
const reader = [LANGUAGES_READ];

function queryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

/** The real routes under a stub session. */
function renderRoutes(path: string, auth: AuthContextValue = authValue(administrator)) {
  return render(
    <QueryClientProvider client={queryClient()}>
      <AuthContext.Provider value={auth}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
          <LocationProbe />
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}

const requests: string[] = [];
const onRequest = ({ request }: { request: Request }) => {
  requests.push(request.url);
};

beforeEach(() => {
  requests.length = 0;
  server.events.on('request:start', onRequest);
  server.use(
    http.get(languagesUrl, ({ request }) => {
      const filter = new URL(request.url).searchParams.get('filter') ?? '';
      const data = filter.startsWith('code=')
        ? sampleLanguages.filter((language) => `code=${language.code}` === filter)
        : sampleLanguages;
      return HttpResponse.json({ count: data.length, data });
    }),
  );
});

afterEach(() => {
  server.events.removeListener('request:start', onRequest);
});

describe('US-2 Scenario Outline: One title, and the tab says the same (BR-6)', () => {
  it.each([
    ['/home', 'Главная', 'Главная — Mustask'],
    ['/dictionaries/languages', 'Языки', 'Языки — Mustask'],
    ['/dictionaries/languages/new', 'Добавить язык', 'Добавить язык — Mustask'],
    ['/dictionaries/languages/ru', 'Язык: ru', 'Язык: ru — Mustask'],
    ['/no-such-page', 'Страница не найдена', 'Страница не найдена — Mustask'],
  ])(
    '%s has exactly one level-1 heading, «%s», and the tab reads «%s»',
    async (path, heading, tab) => {
      renderRoutes(path);

      const title = await screen.findByRole('heading', { level: 1, name: heading });
      expect(screen.getAllByRole('heading', { level: 1 })).toEqual([title]);
      await waitFor(() => expect(document.title).toBe(tab));
    },
  );

  it('a page refused by its code: «Доступ ограничен — Mustask»', async () => {
    renderRoutes('/dictionaries/languages/new', authValue(reader));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Доступ ограничен' }),
    ).toBeInTheDocument();
    expect(document.title).toBe('Доступ ограничен — Mustask');
  });
});

describe('US-11 Scenario: Home in the shell', () => {
  it.each(['/', '/home'])(
    '%s opens «Главная» in the shell with its welcome texts',
    async (path) => {
      renderRoutes(path);

      expect(await screen.findByRole('heading', { level: 1, name: 'Главная' })).toBeInTheDocument();
      expect(screen.getByRole('banner')).toBeInTheDocument();
      expect(screen.getByTestId('location')).toHaveTextContent('/home');
      expect(screen.getByText('Добро пожаловать в Mustask')).toBeInTheDocument();
      expect(
        screen.getByText(
          'Mustask — трекер задач. Разделы появятся в меню слева по мере их готовности.',
        ),
      ).toBeInTheDocument();
    },
  );
});

describe('US-7 Scenario: Not found (BR-13)', () => {
  it('keeps the address, answers in the shell, and «На главную» opens «Главная»', async () => {
    renderRoutes('/no-such-page');

    expect(
      within(screen.getByRole('main')).getByRole('heading', {
        level: 1,
        name: 'Страница не найдена',
      }),
    ).toBeInTheDocument();
    expect(screen.getByText('Проверьте адрес или вернитесь на главную.')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/no-such-page');
    expect(screen.getByRole('banner')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('link', { name: 'На главную' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Главная' })).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/home');
  });

  // qa.md D-2, on the real routes: beneath «Языки» but served by no route.
  it('shows no settings navigation and marks nothing current beneath a settings page', async () => {
    renderRoutes('/dictionaries/languages/ru/extra');

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).not.toBeInTheDocument();
    expect(
      screen.getByRole('navigation', { name: 'Основное меню' }).querySelectorAll('[aria-current]'),
    ).toHaveLength(0);
  });
});

describe('US-2 Scenario: Page trail (BR-6) — an address with a trailing slash', () => {
  // qa.md D-1, on the real routes: the slash does not make a page its own ancestor.
  it('`/home/` has no trail and «Главная» is marked `page`', async () => {
    renderRoutes('/home/');

    expect(await screen.findByRole('heading', { level: 1, name: 'Главная' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).not.toBeInTheDocument();
    expect(
      within(screen.getByRole('navigation', { name: 'Основное меню' })).getByRole('link', {
        name: 'Главная',
      }),
    ).toHaveAttribute('aria-current', 'page');
  });

  it('`/dictionaries/languages/` has the trail «Настройки / Справочники», no link, and «Языки» marked `page`', async () => {
    renderRoutes('/dictionaries/languages/');

    expect(await screen.findByRole('heading', { level: 1, name: 'Языки' })).toBeInTheDocument();
    const trail = screen.getByRole('navigation', { name: 'Путь к странице' });
    expect(trail).toHaveTextContent('Настройки/Справочники');
    expect(within(trail).queryByRole('link')).not.toBeInTheDocument();
    expect(
      within(screen.getByRole('navigation', { name: 'Меню настроек' })).getByRole('link', {
        name: 'Языки',
      }),
    ).toHaveAttribute('aria-current', 'page');
  });
});

describe('US-7 Scenario: Access denied, read-gated page', () => {
  it('keeps the shell, shows the canon’s texts and sends no languages request', async () => {
    renderRoutes('/dictionaries/languages', authValue([]));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Доступ ограничен' }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        'У вас нет прав для просмотра этого раздела. Обратитесь к администратору, чтобы запросить доступ.',
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole('banner')).toBeInTheDocument();
    expect(requests.filter((url) => url.startsWith(languagesUrl))).toEqual([]);
  });
});

describe('US-7 Scenario: Access denied, write-gated page', () => {
  it.each(['/dictionaries/languages/new', '/dictionaries/languages/ru'])(
    'a reader opening %s gets the same frame and no «Добавить язык» heading',
    async (path) => {
      renderRoutes(path, authValue(reader));

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Доступ ограничен' }),
      ).toBeInTheDocument();
      expect(screen.queryByRole('heading', { name: 'Добавить язык' })).not.toBeInTheDocument();
    },
  );
});

describe('US-9 Scenario: No leave guard in the MVP (D-22)', () => {
  it.each([
    ['«Главная» in the navigation', () => screen.getByRole('link', { name: 'Главная' }), '/home'],
    ['the product mark', () => screen.getByRole('link', { name: 'Mustask' }), '/home'],
    [
      'the trail’s «Языки»',
      () =>
        within(screen.getByRole('navigation', { name: 'Путь к странице' })).getByRole('link', {
          name: 'Языки',
        }),
      '/dictionaries/languages',
    ],
  ])(
    'leaving «Добавить язык» with typed input by %s opens the page at once and drops the input',
    async (_way, target, path) => {
      const user = userEvent.setup();
      renderRoutes('/dictionaries/languages/new');
      await user.type(await screen.findByLabelText('Эндоним'), 'Несохранённый ввод');

      await user.click(target());

      expect(screen.getByTestId('location')).toHaveTextContent(path);
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(screen.queryByDisplayValue('Несохранённый ввод')).not.toBeInTheDocument();
    },
  );
});

describe('US-2 Scenario: User menu and sign-out — through the real session', () => {
  afterEach(() => {
    setNavigateImpl((url) => window.location.assign(url));
  });

  it('«Выйти» starts the sign-out the former top-bar button did', async () => {
    const navigate = vi.fn();
    setNavigateImpl(navigate);
    render(
      <QueryClientProvider client={queryClient()}>
        <AuthProvider>
          <MemoryRouter initialEntries={['/home']}>
            <AppRoutes />
          </MemoryRouter>
        </AuthProvider>
      </QueryClientProvider>,
    );

    await userEvent.click(await screen.findByRole('button', { name: /^Профиль/ }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Выйти' }));

    await waitFor(() =>
      expect(navigate).toHaveBeenCalledWith(
        'http://keycloak.invalid/realms/mustask/protocol/openid-connect/logout',
      ),
    );
  });

  it('shows «Выход не завершён…» when the auth service does not answer, as today', async () => {
    server.use(http.post(`${api}/api/session/logout`, () => HttpResponse.error()));
    render(
      <QueryClientProvider client={queryClient()}>
        <AuthProvider>
          <MemoryRouter initialEntries={['/home']}>
            <AppRoutes />
          </MemoryRouter>
        </AuthProvider>
      </QueryClientProvider>,
    );

    await userEvent.click(await screen.findByRole('button', { name: /^Профиль/ }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Выйти' }));

    expect(
      await screen.findByText('Выход не завершён: сервис аутентификации недоступен.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Повторить выход' })).toBeInTheDocument();
  });
});
