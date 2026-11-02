import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { organizationsKeys } from '../features/organizations/api/queryKeys';
import { API } from '../features/organizations/testFixtures';
import type { WorkContextSource } from '../shared/lib/workContext';
import { server } from '../test/mocks/server';
import { ADMIN_CODES, servedWith } from '../test/organizationRoles';
import { authValue, LANGUAGES_READ, LANGUAGES_WRITE, renderShell } from '../test/renderShell';
import { KEPT_CHOICE_KEY } from './navigation/keptChoice';

const reader = [LANGUAGES_READ];
const administrator = [LANGUAGES_READ, LANGUAGES_WRITE];

function mainNavigation() {
  return screen.getByRole('navigation', { name: 'Основное меню' });
}

describe('US-3 Scenario: The main navigation outside settings (§ Navigation model)', () => {
  it('lists «Главная», «Настройки» at its bottom above the collapse toggle, and no settings navigation', () => {
    renderShell({ auth: authValue(reader) });

    const navigation = mainNavigation();
    expect(within(navigation).getByRole('link', { name: 'Главная' })).toBeInTheDocument();
    const settings = within(navigation).getByRole('link', { name: 'Настройки' });
    const toggle = screen.getByRole('button', { name: 'Свернуть меню' });
    expect(
      settings.compareDocumentPosition(toggle) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Языки' })).not.toBeInTheDocument();
  });
});

describe('US-3 Scenario: «Настройки» opens the first settings page the user may open (BR-19)', () => {
  it('opens «Языки» with «Меню настроек» beside a rail whose «Настройки» is current and which has no toggle', async () => {
    renderShell({ auth: authValue(reader) });

    await userEvent.click(within(mainNavigation()).getByRole('link', { name: 'Настройки' }));

    expect(screen.getByTestId('location')).toHaveTextContent('/dictionaries/languages');
    const settingsNavigation = screen.getByRole('navigation', { name: 'Меню настроек' });
    expect(within(settingsNavigation).getByText('Настройки')).toBeInTheDocument();
    expect(within(settingsNavigation).getByText('Справочники')).toBeInTheDocument();
    expect(within(settingsNavigation).getByRole('link', { name: 'Языки' })).toBeInTheDocument();

    const rail = mainNavigation();
    // A rail of icons: the entries are named, but no label is drawn beside them.
    expect(within(rail).queryByText('Главная')).not.toBeInTheDocument();
    expect(within(rail).getByRole('link', { name: 'Главная' })).toBeInTheDocument();
    expect(within(rail).getByRole('link', { name: 'Настройки' })).toHaveAttribute(
      'aria-current',
      'true',
    );
    expect(screen.queryByRole('button', { name: /меню$/ })).not.toBeInTheDocument();
  });
});

describe('US-3 Scenario: A settings address opened directly shows the same navigation', () => {
  it('shows «Меню настроек» with «Языки» current, and the rail with «Настройки» current', () => {
    renderShell({ path: '/dictionaries/languages', auth: authValue(reader) });

    const settingsNavigation = screen.getByRole('navigation', { name: 'Меню настроек' });
    expect(within(settingsNavigation).getByRole('link', { name: 'Языки' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(within(mainNavigation()).getByRole('link', { name: 'Настройки' })).toHaveAttribute(
      'aria-current',
      'true',
    );
  });
});

describe('US-3 Scenario: Leaving settings', () => {
  it('opens «Главная» from the rail, drops «Меню настроек» and expands the navigation again', async () => {
    renderShell({ path: '/dictionaries/languages', auth: authValue(reader) });

    await userEvent.click(within(mainNavigation()).getByRole('link', { name: 'Главная' }));

    expect(screen.getByTestId('location')).toHaveTextContent('/home');
    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).not.toBeInTheDocument();
    // 1440 px with no kept choice: expanded, labels drawn, the toggle back.
    expect(within(mainNavigation()).getByText('Главная')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Свернуть меню' })).toBeInTheDocument();
  });
});

describe('US-3 Scenario: A user with no settings page sees no settings entry (BR-7)', () => {
  it('lists «Главная» without «Настройки», and the registry by address answers «Доступ ограничен» in the shell', async () => {
    renderShell({ auth: authValue([]) });

    expect(within(mainNavigation()).getByRole('link', { name: 'Главная' })).toBeInTheDocument();
    expect(
      within(mainNavigation()).queryByRole('link', { name: 'Настройки' }),
    ).not.toBeInTheDocument();
  });

  it('answers «Доступ ограничен» inside the shell, with no settings navigation', () => {
    renderShell({ path: '/dictionaries/languages', auth: authValue([]) });

    expect(screen.getByRole('banner')).toBeInTheDocument();
    expect(
      within(screen.getByRole('main')).getByRole('heading', { level: 1, name: 'Доступ ограничен' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).not.toBeInTheDocument();
  });
});

describe('US-3 Scenario: Pages that do not exist yet never appear (BR-7) — on screen', () => {
  it('shows no hub, section or entry of a later line, and nothing disabled', () => {
    renderShell({ path: '/dictionaries/languages', auth: authValue(administrator) });

    for (const label of [
      'Рабочие элементы',
      'Канбан',
      'Бэклоги',
      'Спринты',
      'Запросы',
      'Организация',
      'Проект',
      'Процесс',
    ]) {
      expect(screen.queryByText(label)).not.toBeInTheDocument();
      expect(screen.queryByRole('link', { name: label })).not.toBeInTheDocument();
    }
    for (const navigation of screen.getAllByRole('navigation')) {
      expect(navigation.querySelector('[aria-disabled="true"], .Mui-disabled')).toBeNull();
    }
  });
});

describe('US-3 Scenario: The menu of the chosen object waits for a real context (BR-8, D-18)', () => {
  const organizations = [{ id: 'org-1', name: 'Организация 1', projects: [], teams: [] }];
  const realSource = (selection: WorkContextSource['selection']): WorkContextSource => ({
    status: 'ready',
    organizations,
    selection,
    isReal: true,
  });

  it('is hidden while no real context is chosen', () => {
    renderShell({ auth: authValue(administrator) });

    expect(screen.queryByRole('link', { name: 'Обзор' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Проекты' })).not.toBeInTheDocument();
  });

  it('shows the organization menu, from the table, once the source reports a real organization', async () => {
    const shell = renderShell({ auth: authValue([]) });
    expect(screen.queryByRole('link', { name: 'Обзор' })).not.toBeInTheDocument();

    shell.update({ contextSource: realSource({ organizationId: 'org-1' }) });

    expect(await within(mainNavigation()).findByRole('link', { name: 'Обзор' })).toHaveAttribute(
      'href',
      '/organizations/org-1/overview',
    );
    for (const [label, segment] of [
      ['Области', 'areas'],
      ['Проекты', 'projects'],
      ['Команды', 'teams'],
      ['Участники', 'members'],
    ]) {
      expect(within(mainNavigation()).getByRole('link', { name: label })).toHaveAttribute(
        'href',
        `/organizations/org-1/${segment}`,
      );
    }
  });

  it('shows the team menu when a team is chosen, and not the organization one', async () => {
    renderShell({
      auth: authValue([]),
      contextSource: realSource({ organizationId: 'org-1', teamId: 'team-1' }),
    });

    expect(
      await within(mainNavigation()).findByRole('link', { name: 'Канбан' }),
    ).toBeInTheDocument();
    expect(within(mainNavigation()).getByRole('link', { name: 'Бэклог' })).toBeInTheDocument();
    expect(
      within(mainNavigation()).queryByRole('link', { name: 'Области' }),
    ).not.toBeInTheDocument();
  });

  it('stays hidden when the served bundle lacks the entry’s code', async () => {
    server.use(
      http.get(`${API}/api/organizations/org-1/access`, () =>
        HttpResponse.json(
          servedWith(
            'admin',
            ADMIN_CODES.filter((code) => code !== 'organizations.area.read'),
          ),
        ),
      ),
    );
    const shell = renderShell({
      auth: authValue([]),
      contextSource: realSource({ organizationId: 'org-1' }),
      access: 'unread',
    });

    await waitFor(() =>
      expect(shell.queryClient.getQueryData(organizationsKeys.access('org-1'))).toMatchObject({
        kind: 'member',
      }),
    );
    expect(
      within(mainNavigation()).queryByRole('link', { name: 'Области' }),
    ).not.toBeInTheDocument();
  });
});

describe('US-3 Scenario: The current page is marked', () => {
  it('marks «Языки» `page` on the registry, `true` with the selected marker on the form; only «Настройки» in the rail', () => {
    const { unmount } = renderShell({
      path: '/dictionaries/languages',
      auth: authValue(administrator),
    });
    expect(
      within(screen.getByRole('navigation', { name: 'Меню настроек' })).getByRole('link', {
        name: 'Языки',
      }),
    ).toHaveAttribute('aria-current', 'page');
    unmount();

    renderShell({ path: '/dictionaries/languages/new', auth: authValue(administrator) });

    const settingsNavigation = screen.getByRole('navigation', { name: 'Меню настроек' });
    const languages = within(settingsNavigation).getByRole('link', { name: 'Языки' });
    expect(languages).toHaveAttribute('aria-current', 'true');
    expect(languages).toHaveClass('Mui-selected');
    expect(settingsNavigation.querySelectorAll('[aria-current]')).toHaveLength(1);

    const current = mainNavigation().querySelectorAll('[aria-current]');
    expect(current).toHaveLength(1);
    expect(current[0]).toHaveAccessibleName('Настройки');
    expect(current[0]).toHaveAttribute('aria-current', 'true');
  });

  // qa.md D-1: the router serves an address with a trailing slash as the page
  // itself, so it reads the same as without it (BR-6: ancestors only).
  it('reads `/home/` as «Главная»: no trail, «Главная» marked `page`', () => {
    renderShell({ path: '/home/', auth: authValue(administrator) });

    expect(screen.getByRole('heading', { level: 1, name: 'Главная' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).not.toBeInTheDocument();
    expect(within(mainNavigation()).getByRole('link', { name: 'Главная' })).toHaveAttribute(
      'aria-current',
      'page',
    );
  });

  it('reads `/dictionaries/languages/` as «Языки»: the trail has no link, «Языки» marked `page`', () => {
    renderShell({ path: '/dictionaries/languages/', auth: authValue(administrator) });

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

describe('US-3 Scenario: The current page is marked — an address no page serves (BR-13, BR-19)', () => {
  // qa.md D-2: «Страница не найдена» comes before the settings-navigation
  // decision (the BPMN), so an unknown address beneath «Языки» is not a
  // settings page, and no entry is current on it.
  it('beneath «Языки»: «Страница не найдена» with the navigation as kept and no «Меню настроек»', () => {
    localStorage.setItem(KEPT_CHOICE_KEY, 'expanded');
    renderShell({ path: '/dictionaries/languages/ru/extra', auth: authValue(administrator) });

    expect(
      screen.getByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).not.toBeInTheDocument();
    // Expanded as kept, with its toggle: not forced to the settings rail.
    expect(within(mainNavigation()).getByText('Главная')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Свернуть меню' })).toBeInTheDocument();
    expect(mainNavigation().querySelectorAll('[aria-current]')).toHaveLength(0);
  });

  it('beneath «Главная»: no entry is marked current', () => {
    renderShell({ path: '/home/extra', auth: authValue(administrator) });

    expect(
      screen.getByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
    expect(mainNavigation().querySelectorAll('[aria-current]')).toHaveLength(0);
  });

  it('gives the settings navigation back once a page serves the address again', async () => {
    renderShell({ path: '/dictionaries/languages/ru/extra', auth: authValue(reader) });
    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).not.toBeInTheDocument();

    await userEvent.click(within(mainNavigation()).getByRole('link', { name: 'Настройки' }));

    expect(screen.getByTestId('location')).toHaveTextContent('/dictionaries/languages');
    expect(
      within(screen.getByRole('navigation', { name: 'Меню настроек' })).getByRole('link', {
        name: 'Языки',
      }),
    ).toHaveAttribute('aria-current', 'page');
  });
});

describe('US-3 Scenario: Visibility follows the permission check, not a copy of it (BR-8, BR-19)', () => {
  it('drops «Настройки» and «Меню настроек» on a renewed token without the code, and the page answers «Доступ ограничен»', () => {
    const shell = renderShell({ path: '/dictionaries/languages', auth: authValue(reader) });
    expect(screen.getByRole('navigation', { name: 'Меню настроек' })).toBeInTheDocument();

    shell.update({ auth: authValue([]) });

    expect(screen.queryByRole('link', { name: 'Настройки' })).not.toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Доступ ограничен' })).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/dictionaries/languages');
  });
});
