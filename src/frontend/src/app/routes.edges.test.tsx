import { beforeEach, describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { AuthContext, type AuthContextValue } from '../features/auth/AuthContext';
import { sampleLanguages } from '../features/dictionaries/testFixtures';
import { server } from '../test/mocks/server';
import { authValue, LANGUAGES_READ, LANGUAGES_WRITE, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

/**
 * QA additions (mod-015, stage 7, qa.md § Additional scenarios A6–A8,
 * A11–A13, A15): edges of the real routes the stories do not spell out.
 */

const languagesUrl = 'http://localhost:5000/api/dictionaries/languages';
const administrator = [LANGUAGES_READ, LANGUAGES_WRITE];
const reader = [LANGUAGES_READ];

/** The real routes under a stub session that a test may renew. */
function renderRoutes(path: string, auth: AuthContextValue = authValue(administrator)) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const tree = (current: AuthContextValue) => (
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={current}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
          <LocationProbe />
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>
  );
  const view = render(tree(auth));
  return { ...view, renew: (next: AuthContextValue) => view.rerender(tree(next)) };
}

function location() {
  return screen.getByTestId('location').textContent;
}

beforeEach(() => {
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

describe('QA A6: addresses beside the dictionaries pages are unknown (BR-13)', () => {
  it.each(['/dictionaries', '/dictionaries/', '/dictionaries/languages/ru/extra'])(
    '%s answers «Страница не найдена» in the shell and keeps the address',
    async (path) => {
      renderRoutes(path);

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
      ).toBeInTheDocument();
      expect(screen.getByRole('banner')).toBeInTheDocument();
      expect(location()).toBe(path);
      expect(document.title).toBe('Страница не найдена — Mustask');
    },
  );
});

describe('QA A7: an unknown address is not found for a user with no code at all', () => {
  it('answers «Страница не найдена», not «Доступ ограничен»', async () => {
    renderRoutes('/no-such-page', authValue([]));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByText('Доступ ограничен')).not.toBeInTheDocument();
  });
});

describe('QA A8: the tab title follows the page after a not-found address', () => {
  it('reads «Главная — Mustask» once «Главная» is chosen in the navigation', async () => {
    const user = userEvent.setup();
    renderRoutes('/no-such-page');
    await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' });

    await user.click(
      within(screen.getByRole('navigation', { name: 'Основное меню' })).getByRole('link', {
        name: 'Главная',
      }),
    );

    expect(await screen.findByRole('heading', { level: 1, name: 'Главная' })).toBeInTheDocument();
    await waitFor(() => expect(document.title).toBe('Главная — Mustask'));
  });
});

describe('QA A11: a renewal that drops the write code refuses the open form (BR-8)', () => {
  it('turns «Добавить язык» into «Доступ ограничен» without a reload; settings stay for the reader', async () => {
    const view = renderRoutes('/dictionaries/languages/new');
    await screen.findByRole('heading', { level: 1, name: 'Добавить язык' });
    expect(screen.getByRole('navigation', { name: 'Путь к странице' })).toBeInTheDocument();

    view.renew(authValue(reader));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Доступ ограничен' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Добавить язык' })).not.toBeInTheDocument();
    expect(location()).toBe('/dictionaries/languages/new');
    await waitFor(() => expect(document.title).toBe('Доступ ограничен — Mustask'));
    // A full-page frame has no trail (US-2); the reader still holds the read code.
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).not.toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Меню настроек' })).toBeInTheDocument();
  });
});

describe('QA A12: «Настройки» in the rail while already on a settings page', () => {
  it('keeps «Языки» open and the settings navigation shown', async () => {
    const user = userEvent.setup();
    renderRoutes('/dictionaries/languages');
    await screen.findByRole('table', { name: 'Реестр языков' });

    await user.click(
      within(screen.getByRole('navigation', { name: 'Основное меню' })).getByRole('link', {
        name: 'Настройки',
      }),
    );

    expect(location()).toBe('/dictionaries/languages');
    expect(screen.getByRole('navigation', { name: 'Меню настроек' })).toBeInTheDocument();
    expect(screen.getByRole('table', { name: 'Реестр языков' })).toBeInTheDocument();
  });
});

describe('QA A13: the skip link on full-page frames (US-12)', () => {
  it.each([
    ['not found', '/no-such-page', authValue(administrator), 'Страница не найдена'],
    ['access denied', '/dictionaries/languages', authValue([]), 'Доступ ограничен'],
  ])('moves focus into the main region on %s', async (_state, path, auth, heading) => {
    const user = userEvent.setup();
    renderRoutes(path, auth);
    await screen.findByRole('heading', { level: 1, name: heading });

    await user.tab();
    expect(screen.getByRole('link', { name: 'Перейти к содержимому' })).toHaveFocus();
    await user.keyboard('{Enter}');

    expect(screen.getByRole('main')).toHaveFocus();
  });
});

describe('QA A15: a language code that does not exist', () => {
  it('keeps one title and a current tab title, and offers «Повторить»', async () => {
    renderRoutes('/dictionaries/languages/zz');

    const title = await screen.findByRole('heading', { level: 1, name: 'Язык: zz' });
    expect(screen.getAllByRole('heading', { level: 1 })).toEqual([title]);
    expect(await screen.findByRole('button', { name: 'Повторить' })).toBeInTheDocument();
    await waitFor(() => expect(document.title).toBe('Язык: zz — Mustask'));
  });
});
