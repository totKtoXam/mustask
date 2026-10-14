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

async function levels(): Promise<string[]> {
  await waitFor(() =>
    expect(within(switcher()).queryByRole('status', { name: 'Загрузка контекста' })).toBeNull(),
  );
  return within(switcher())
    .getAllByRole('button')
    .map((button) => button.getAttribute('aria-label') ?? '');
}

async function expectLevels(expected: string[]) {
  await waitFor(async () => expect(await levels()).toEqual(expected));
}

async function choose(level: RegExp, entry: string) {
  await userEvent.click(within(switcher()).getByRole('button', { name: level }));
  await userEvent.click(await screen.findByRole('menuitemradio', { name: entry }));
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

describe('mod-002 US-8 Scenario: The switcher lists active organizations, projects and teams', () => {
  it('shows the first entries, the levels in the order received, «Все организации» last, nothing deactivated', async () => {
    serve();
    renderApp('/home');

    await expectLevels(['Организация: Альфа', 'Проект: Аналитика', 'Команда: Аналитика']);
    await userEvent.click(within(switcher()).getByRole('button', { name: 'Организация: Альфа' }));
    const menu = await screen.findByRole('menu');
    expect(
      within(menu)
        .getAllByRole('menuitemradio')
        .map((item) => item.textContent),
    ).toEqual(['Альфа', 'Бета']);
    const items = within(menu).getAllByRole('menuitem');
    expect(items.at(-1)).toHaveTextContent('Все организации');
    expect(items.at(-1)).toHaveAttribute('href', '/organizations');
  });
});

describe('mod-002 US-8 Scenario: «Все организации» opens the list', () => {
  it('opens the active tab and leaves the selection as it was', async () => {
    serve();
    renderApp('/home');
    await expectLevels(['Организация: Альфа', 'Проект: Аналитика', 'Команда: Аналитика']);

    await userEvent.click(within(switcher()).getByRole('button', { name: 'Организация: Альфа' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Все организации' }));

    expect(await screen.findByRole('table', { name: 'Активные организации' })).toBeInTheDocument();
    expect(location()).toBe('/organizations');
    await expectLevels(['Организация: Альфа', 'Проект: Аналитика', 'Команда: Аналитика']);
    // The list is reached only through the switcher: no entry is current there.
    expect(settingsNavigation()).not.toBeInTheDocument();
    expect(
      screen.getByRole('navigation', { name: 'Основное меню' }).querySelectorAll('[aria-current]'),
    ).toHaveLength(0);
  });
});

describe('mod-002 US-8 Scenario: «Нет организаций» leads to the list', () => {
  it('links to the list with no project or team level, where «Вы пока не состоите ни в одной организации» shows', async () => {
    serve(emptyDataset());
    renderApp('/home');

    const link = await screen.findByRole('link', { name: 'Нет организаций' });
    expect(within(switcher()).queryByText('Нет проектов')).not.toBeInTheDocument();
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
  it('«Меню настроек» holds «Организация» → «Общие», «Проекты», «Процесс» and «Проект» → «Общие», «Команды», «Области»; «Настройки» opens the organization’s «Общие»', async () => {
    serve();
    renderApp('/home');
    await expectLevels(['Организация: Альфа', 'Проект: Аналитика', 'Команда: Аналитика']);

    const settings = within(screen.getByRole('navigation', { name: 'Основное меню' })).getByRole(
      'link',
      { name: 'Настройки' },
    );
    expect(settings).toHaveAttribute('href', `/organizations/${ids.alfa}/settings/general`);
    await userEvent.click(settings);

    expect(await screen.findByText('Альфа', { selector: 'dd *' })).toBeInTheDocument();
    const navigation = settingsNavigation()!;
    const links = within(navigation)
      .getAllByRole('link')
      .map((link) => link.textContent);
    expect(links).toEqual([
      'Общие',
      'Проекты',
      'Участники',
      'Процесс',
      'Общие',
      'Команды',
      'Области',
      'Языки',
    ]);
    expect(within(navigation).getByText('Организация')).toBeInTheDocument();
    expect(within(navigation).getByText('Проект')).toBeInTheDocument();
    expect(within(navigation).getAllByRole('link', { name: 'Общие' })[0]).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.getByRole('navigation', { name: 'Путь к странице' })).toHaveTextContent(
      'Настройки/Организация',
    );
  });
});

describe('mod-002 US-8 Scenario: A project switch on a project-level page', () => {
  it('on «Области» of «Пример проекта», «Аналитика» opens the «Области» of «Аналитика»', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/projects/${ids.prm}/settings/areas`);
    await expectLevels(['Организация: Альфа', 'Проект: Пример проекта', 'Команда: Пример проекта']);

    await choose(/^Проект:/, 'Аналитика');

    await waitFor(() =>
      expect(location()).toBe(`/organizations/${ids.alfa}/projects/${ids.anl}/settings/areas`),
    );
    await expectLevels(['Организация: Альфа', 'Проект: Аналитика', 'Команда: Аналитика']);
  });

  it('«Бета» opens its first project’s «Общие», and an organization without projects opens «Главная»', async () => {
    const api = serve();
    api.data.organizations.push({
      id: 'a0000000-0000-4000-8000-0000000000c1',
      name: 'Ведро',
      active: true,
    });
    renderApp(`/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`);
    await expectLevels(['Организация: Альфа', 'Проект: Пример проекта', 'Команда: Пример проекта']);

    await choose(/^Организация:/, 'Бета');
    await waitFor(() =>
      expect(location()).toBe(`/organizations/${ids.beta}/projects/${ids.rep}/settings/general`),
    );

    await choose(/^Организация:/, 'Ведро');
    await waitFor(() => expect(location()).toBe('/home'));
  });
});

describe('mod-002 US-8 Scenario: A page addressing one team lands on its entry’s page', () => {
  it('from a team’s page, a project choice opens «Команды» of the new project', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams/${ids.platformTeam}`);
    await screen.findByRole('heading', { level: 1, name: 'Команда: Платформа' });
    await expectLevels(['Организация: Альфа', 'Проект: Пример проекта', 'Команда: Пример проекта']);

    await choose(/^Проект:/, 'Аналитика');

    await waitFor(() =>
      expect(location()).toBe(`/organizations/${ids.alfa}/projects/${ids.anl}/settings/teams`),
    );
  });
});

describe('mod-002 US-8 Scenario: On an organization-level page the lower levels do not move it', () => {
  it('project and team choices keep «Общие»; an organization choice opens the new «Общие»', async () => {
    serve();
    const general = `/organizations/${ids.alfa}/settings/general`;
    renderApp(general);
    await expectLevels(['Организация: Альфа', 'Проект: Аналитика', 'Команда: Аналитика']);

    await choose(/^Проект:/, 'Пример проекта');
    await choose(/^Команда:/, 'Платформа');
    expect(location()).toBe(general);

    await choose(/^Организация:/, 'Бета');
    await waitFor(() => expect(location()).toBe(`/organizations/${ids.beta}/settings/general`));
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
        within(switcher()).getByRole('button', { name: /^Организация:/ }),
      ).toHaveAccessibleName('Организация: Дельта'),
    );
    await waitFor(() => expect(within(switcher()).getByText('Нет проектов')).toBeInTheDocument());
  });

  it('an added team joins the team level; chosen, then deleted, it falls back with the switcher’s notice only', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams`);
    await screen.findByRole('table', { name: 'Команды' });
    await expectLevels(['Организация: Альфа', 'Проект: Пример проекта', 'Команда: Пример проекта']);

    await userEvent.click(screen.getByRole('button', { name: 'Добавить команду' }));
    const add = await screen.findByRole('dialog', { name: 'Новая команда' });
    await userEvent.type(within(add).getByRole('textbox', { name: 'Название' }), 'Ядро');
    await userEvent.click(within(add).getByRole('button', { name: 'Добавить' }));
    await screen.findByText('Команда «Ядро» добавлена.');

    await choose(/^Команда:/, 'Ядро');
    await expectLevels(['Организация: Альфа', 'Проект: Пример проекта', 'Команда: Ядро']);

    await userEvent.click(screen.getByRole('button', { name: 'Действия: Ядро' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Удалить команду' }));
    const remove = await screen.findByRole('dialog', { name: 'Удаление команды' });
    await userEvent.click(within(remove).getByRole('button', { name: 'Удалить команду' }));

    expect(
      await screen.findByText(
        'Ранее выбранный контекст недоступен. Выбрано: Альфа / Пример проекта / Пример проекта.',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByText('Команда «Ядро» удалена.')).not.toBeInTheDocument();
    await expectLevels(['Организация: Альфа', 'Проект: Пример проекта', 'Команда: Пример проекта']);
  });

  it('deactivating «Альфа» from its «Общие»: the deactivated list opens, the switcher on «Бета», the notice the fallback’s only', async () => {
    serve();
    renderApp(`/organizations/${ids.alfa}/settings/general`);
    await expectLevels(['Организация: Альфа', 'Проект: Аналитика', 'Команда: Аналитика']);

    await userEvent.click(await screen.findByRole('button', { name: 'Деактивировать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Деактивировать организацию?' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Деактивировать' }));

    expect(
      await screen.findByText(
        'Ранее выбранный контекст недоступен. Выбрано: Бета / Отчётность / Отчётность.',
      ),
    ).toBeInTheDocument();
    await waitFor(() => expect(location()).toBe('/organizations/deactivated'));
    await expectLevels(['Организация: Бета', 'Проект: Отчётность', 'Команда: Отчётность']);
    expect(screen.queryByText('Организация «Альфа» деактивирована.')).not.toBeInTheDocument();
  });
});

describe('mod-002 design § 12: the navigation around each not-found frame', () => {
  it('«Команда не найдена» keeps «Меню настроек» with «Команды» current and shows no trail', async () => {
    const api = serve();
    api.data.teams.find((team) => team.id === ids.platformTeam)!.deleted = true;
    renderApp(`/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams/${ids.platformTeam}`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда не найдена' }),
    ).toBeInTheDocument();
    await waitFor(() => expect(settingsNavigation()).toBeInTheDocument());
    expect(within(settingsNavigation()!).getByRole('link', { name: 'Команды' })).toHaveAttribute(
      'aria-current',
      'true',
    );
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).not.toBeInTheDocument();
  });

  it('«Организация не найдена» shows no «Меню настроек», no entry current and keeps the switcher', async () => {
    serve();
    renderApp(`/organizations/${ids.gamma}/settings/general`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    await expectLevels(['Организация: Альфа', 'Проект: Аналитика', 'Команда: Аналитика']);
    expect(settingsNavigation()).not.toBeInTheDocument();
    for (const navigation of screen.getAllByRole('navigation')) {
      expect(navigation.querySelectorAll('[aria-current]')).toHaveLength(0);
    }
  });
});
