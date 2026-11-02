import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AuthContext, type AuthContextValue } from '../features/auth/AuthContext';
import { emptyDataset, ids, type Dataset } from '../features/organizations/testFixtures';
import { serveOrganizations } from '../features/organizations/testApi';
import { authValue, LANGUAGES_READ, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

const CREATE = 'organizations.organization.create';

/** The real routes, the real shell and the real context source, under a stub session. */
function renderApp(path: string, auth: AuthContextValue = authValue([LANGUAGES_READ, CREATE])) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={{ ...auth, subject: 'user-1' }}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
          <LocationProbe />
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}

function switcher() {
  return screen.getByRole('group', { name: 'Контекст работы' });
}

/** The names of the four selectors, left to right, as their buttons read them. */
async function levels(): Promise<string[]> {
  return within(switcher())
    .getAllByRole('combobox')
    .map((button) => button.getAttribute('aria-label') ?? '');
}

async function expectLevels(expected: string[]) {
  await waitFor(async () => expect((await levels()).join(' / ')).toEqual(expected.join(' / ')));
}

const ALFA_ALL = ['Организация: Альфа', 'Департамент: все', 'Команда: все', 'Проект: все'];

async function choose(selector: RegExp, entry: string | RegExp) {
  await userEvent.click(within(switcher()).getByRole('combobox', { name: selector }));
  await userEvent.click(await screen.findByRole('option', { name: entry }));
}

function location() {
  return screen.getByTestId('location').textContent;
}

function settingsNavigation() {
  return screen.queryByRole('navigation', { name: 'Меню настроек' });
}

function serve(data?: Dataset) {
  return serveOrganizations(data);
}

describe('mod-002 US-8 Scenario: The selectors list active organizations', () => {
  it('shows the organization chosen, «все» in the rest, the organizations in the order received and «Все организации» last, nothing deactivated', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/overview`);

    await expectLevels(ALFA_ALL);
    await userEvent.click(within(switcher()).getByRole('combobox', { name: 'Организация: Альфа' }));
    const options = (await screen.findAllByRole('option')).map((item) => item.textContent);
    expect(options).toEqual(['Альфа', 'Бета', 'Все организации']);
  });
});

describe('mod-002 US-8 Scenario: «Все организации» opens the list', () => {
  it('opens the active tab; no entry is current there', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/overview`);
    await expectLevels(ALFA_ALL);

    await userEvent.click(within(switcher()).getByRole('combobox', { name: 'Организация: Альфа' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Все организации' }));

    expect(await screen.findByRole('table', { name: 'Активные организации' })).toBeInTheDocument();
    expect(location()).toBe('/organizations');
    expect(settingsNavigation()).not.toBeInTheDocument();
    expect(
      screen.getByRole('navigation', { name: 'Основное меню' }).querySelectorAll('[aria-current]'),
    ).toHaveLength(0);
  });
});

describe('mod-002 US-8 Scenario: «Нет организаций» leads to the list', () => {
  it('links to the list with no selector, where «Вы пока не состоите ни в одной организации» shows', async () => {
    serve(emptyDataset());
    renderApp('/home');

    const link = await screen.findByRole('link', { name: 'Нет организаций' });
    expect(screen.queryByRole('group', { name: 'Контекст работы' })).not.toBeInTheDocument();
    await userEvent.click(link);

    expect(
      await screen.findByRole('heading', {
        level: 2,
        name: 'Вы пока не состоите ни в одной организации',
      }),
    ).toBeInTheDocument();
  });
});

describe('mod-002 US-8 Scenario: Settings through the shell’s navigation', () => {
  it('«Меню настроек» holds «Организация» → «Общие», «Процесс» and «Справочники» → «Языки»; «Настройки» opens the organization’s «Общие»', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/overview`);
    await expectLevels(ALFA_ALL);

    const settings = within(screen.getByRole('navigation', { name: 'Основное меню' })).getByRole(
      'link',
      { name: 'Настройки' },
    );
    expect(settings).toHaveAttribute('href', `/organizations/${ids.alfa}/settings/general`);
    await userEvent.click(settings);

    expect(await screen.findByText('Альфа', { selector: 'dd *' })).toBeInTheDocument();
    const navigation = settingsNavigation()!;
    expect(
      within(navigation)
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(['Общие', 'Процесс', 'Языки']);
    expect(within(navigation).getByText('Организация')).toBeInTheDocument();
    expect(within(navigation).getByRole('link', { name: 'Общие' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.getByRole('navigation', { name: 'Путь к странице' })).toHaveTextContent(
      'Настройки/Организация',
    );
  });

  it('adds one «Параметры» group for the project chosen', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/projects/${ids.prm}/settings`);

    await screen.findByRole('combobox', { name: 'Проект: Пример проекта' });
    await waitFor(() => expect(settingsNavigation()).toBeInTheDocument());
    const navigation = settingsNavigation()!;
    expect(within(navigation).getByText('Проект')).toBeInTheDocument();
    expect(within(navigation).getByRole('link', { name: 'Параметры' })).toHaveAttribute(
      'aria-current',
      'page',
    );
  });
});

describe('mod-002 US-8 Scenario: A project choice on a project-level page', () => {
  it('on «Команды» of «Пример проекта», «Аналитика» opens the «Команды» of «Аналитика»', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/projects/${ids.prm}/teams`);
    await screen.findByRole('combobox', { name: 'Проект: Пример проекта' });

    await choose(/^Проект:/, /^Аналитика/);

    await waitFor(() =>
      expect(location()).toBe(`/organizations/${ids.alfa}/projects/${ids.anl}/teams`),
    );
    await screen.findByRole('combobox', { name: 'Проект: Аналитика' });
  });

  it('an organization choice opens that organization’s «Обзор»', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/projects/${ids.prm}/general`);
    await screen.findByRole('combobox', { name: 'Проект: Пример проекта' });

    await choose(/^Организация:/, 'Бета');

    await waitFor(() => expect(location()).toBe(`/organizations/${ids.beta}/overview`));
    await expectLevels(['Организация: Бета', 'Департамент: все', 'Команда: все', 'Проект: все']);
  });
});

// feat-028 BR-OL-12: a team's page is the organization's, beneath «Команды»; feat-043 puts it at
// the team's own prefix and the team selector shows it.
describe('feat-028 Scenario: A page addressing one team', () => {
  it('shows the team in its selector, and an organization choice opens that organization’s «Обзор»', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/teams/${ids.platformTeam}/overview`);
    await screen.findByRole('heading', { level: 1, name: 'Команда: Платформа' });
    await screen.findByRole('combobox', { name: 'Команда: Платформа' });

    await choose(/^Организация:/, 'Бета');

    await waitFor(() => expect(location()).toBe(`/organizations/${ids.beta}/overview`));
  });
});

describe('mod-002 US-8 Scenario: On an organization-level page the object choices open their menus', () => {
  it('an organization choice opens the new organization’s «Обзор»', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/settings/general`);
    await expectLevels(ALFA_ALL);

    await choose(/^Организация:/, 'Бета');

    await waitFor(() => expect(location()).toBe(`/organizations/${ids.beta}/overview`));
  });
});

describe('mod-002 US-8 Scenario: Own changes without a reload', () => {
  it('a created organization is listed and set by its address', async () => {
    serve();
    renderApp('/organizations');
    await screen.findByRole('table', { name: 'Активные организации' });

    await userEvent.click(screen.getByRole('button', { name: 'Создать организацию' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая организация' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Дельта');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Организация «Дельта» создана.')).toBeInTheDocument();
    await waitFor(() =>
      expect(
        within(switcher()).getByRole('combobox', { name: /^Организация:/ }),
      ).toHaveAccessibleName('Организация: Дельта'),
    );
  });

  it('deactivating «Альфа» from its «Общие»: the deactivated list opens and the selector falls to «Бета»', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/settings/general`);
    await expectLevels(ALFA_ALL);

    await userEvent.click(await screen.findByRole('button', { name: 'Деактивировать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Деактивировать организацию?' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Деактивировать' }));

    await waitFor(() => expect(location()).toBe('/organizations/deactivated'));
    await expectLevels(['Организация: Бета', 'Департамент: все', 'Команда: все', 'Проект: все']);
  });
});

describe('mod-002 design § 12: the navigation around each not-found frame', () => {
  it('«Команда не найдена» keeps the shell and shows no trail', async () => {
    const api = serve();
    api.data.teams.find((team) => team.id === ids.platformTeam)!.deleted = true;
    renderApp(`/organizations/${ids.alfa}/teams/${ids.platformTeam}/overview`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда не найдена' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Основное меню' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).not.toBeInTheDocument();
  });

  it('«Организация не найдена» shows no «Меню настроек», no entry current and keeps the selectors', async () => {
    serve();
    renderApp(`/organizations/${ids.gamma}/settings/general`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    expect(settingsNavigation()).not.toBeInTheDocument();
    for (const navigation of screen.getAllByRole('navigation')) {
      expect(navigation.querySelectorAll('[aria-current]')).toHaveLength(0);
    }
    expect(screen.getByRole('group', { name: 'Контекст работы' })).toBeInTheDocument();
  });
});
