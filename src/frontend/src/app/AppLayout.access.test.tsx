import { describe, expect, it, vi } from 'vitest';
import { Route } from 'react-router-dom';
import { act, screen, waitFor, within } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { organizationsKeys } from '../features/organizations/api/queryKeys';
import { API } from '../features/organizations/testFixtures';
import type { WorkContextSource } from '../shared/lib/workContext';
import { PageHeader } from '../shared/ui/PageHeader';
import { gate } from '../test/gate';
import { server } from '../test/mocks/server';
import { servedFor } from '../test/organizationRoles';
import {
  authValue,
  LANGUAGES_READ,
  LANGUAGES_WRITE,
  renderShell,
  stubRoutes,
} from '../test/renderShell';

// mod-016 design § 6.1: the shell's navigation reads the served codes. Every
// entry's code is a read code all three roles hold, so the three roles see
// the same entries; what decides an entry is the real context and the read
// codes — unread codes hide the organization's entries and the settings
// navigation with them.

const ORG = 'a0000000-0000-4000-8000-0000000000a1';
const GENERAL = `/organizations/${ORG}/settings/general`;
const LANGUAGES = '/dictionaries/languages';

const routes = (
  <>
    {stubRoutes()}
    <Route
      path="/organizations/:organizationId/settings/general"
      element={<PageHeader title="Общие" />}
    />
  </>
);

function source(overrides: Partial<WorkContextSource> = {}): WorkContextSource {
  return {
    status: 'ready',
    organizations: [
      {
        id: ORG,
        name: 'Альфа',
        projects: [{ id: 'p1', name: 'Проект' }],
        teams: [{ id: 't1', name: 'Команда' }],
      },
    ],
    selection: { organizationId: ORG, projectId: 'p1', teamId: 't1' },
    isReal: true,
    select: vi.fn(),
    ...overrides,
  };
}

const auth = authValue([LANGUAGES_READ, LANGUAGES_WRITE]);

function settingsMenu() {
  return screen.queryByRole('navigation', { name: 'Меню настроек' });
}

function linksOf(navigation: HTMLElement): string[] {
  return within(navigation)
    .getAllByRole('link')
    .map((link) => link.textContent ?? '');
}

function trail() {
  return screen.queryByRole('navigation', { name: 'Путь к странице' });
}

function holdAccess() {
  const open = gate();
  server.use(
    http.get(`${API}/api/organizations/${ORG}/access`, async () => {
      await open.opened;
      return HttpResponse.json(servedFor('admin'));
    }),
  );
  return open;
}

describe('mod-016 US-4 Scenario: the three roles see the same navigation', () => {
  it.each(['admin', 'member', 'viewer'] as const)(
    'a %s sees the organization’s, the project’s and the dictionaries’ settings entries and the work entries',
    async (role) => {
      renderShell({ path: GENERAL, auth, contextSource: source(), routes, access: role });

      const menu = await screen.findByRole('navigation', { name: 'Меню настроек' });
      // «Области» (mod-018) follows «Команды»; «Участники» sits between «Области» and «Процесс» (design § 6.1): its
      // code is a read code every role holds.
      expect(linksOf(menu)).toEqual([
        'Общие',
        'Проекты',
        'Команды',
        'Области',
        'Участники',
        'Процесс',
        'Общие',
        'Команды',
        'Языки',
        'Витрина компонентов',
      ]);
      // The main menu is a rail beside the settings navigation: its links are
      // named by their label, not by text.
      const main = screen.getByRole('navigation', { name: 'Основное меню' });
      for (const name of ['Главная', 'Рабочие элементы', 'Канбан', 'Бэклоги']) {
        expect(within(main).getByRole('link', { name })).toBeInTheDocument();
      }
    },
  );
});

describe('mod-016 design § 6.1 Scenario: unread codes hide the organization’s entries', () => {
  it('shows no «Меню настроек», no marker and no trail on an organization’s settings page until the read answers', async () => {
    const open = holdAccess();
    renderShell({ path: GENERAL, auth, contextSource: source(), routes, access: 'unread' });

    expect(await screen.findByRole('heading', { level: 1, name: 'Общие' })).toBeInTheDocument();
    expect(settingsMenu()).not.toBeInTheDocument();
    expect(trail()).not.toBeInTheDocument();
    const main = screen.getByRole('navigation', { name: 'Основное меню' });
    expect(main.querySelectorAll('[aria-current]')).toHaveLength(0);
    // No organization-scoped work entry either.
    expect(within(main).queryByRole('link', { name: 'Рабочие элементы' })).not.toBeInTheDocument();
    expect(within(main).queryByRole('link', { name: 'Бэклоги' })).not.toBeInTheDocument();

    open.open();

    const menu = await screen.findByRole('navigation', { name: 'Меню настроек' });
    expect(within(menu).getByRole('link', { name: 'Общие', current: 'page' })).toBeInTheDocument();
    expect(await screen.findByRole('navigation', { name: 'Путь к странице' })).toHaveTextContent(
      'Настройки/Организация',
    );
    expect(within(main).getByRole('link', { name: 'Рабочие элементы' })).toBeInTheDocument();
  });

  it('shows «Справочники» alone on «Языки» from the first frame, and «Организация» and «Проект» join it when the codes arrive', async () => {
    const open = holdAccess();
    renderShell({ path: LANGUAGES, auth, contextSource: source(), access: 'unread' });

    const menu = await screen.findByRole('navigation', { name: 'Меню настроек' });
    expect(linksOf(menu)).toEqual(['Языки', 'Витрина компонентов']);
    expect(within(menu).getByText('Справочники')).toBeInTheDocument();
    expect(within(menu).queryByText('Организация')).not.toBeInTheDocument();
    expect(within(menu).queryByText('Проект')).not.toBeInTheDocument();

    open.open();

    await waitFor(() => expect(linksOf(menu)).toHaveLength(10));
    expect(within(menu).getByText('Организация')).toBeInTheDocument();
    expect(within(menu).getByText('Проект')).toBeInTheDocument();
  });

  it('«Настройки» in the rail opens «Языки» meanwhile, as for a user with no organization', async () => {
    holdAccess();
    renderShell({ path: '/home', auth, contextSource: source(), access: 'unread' });

    const settings = await screen.findByRole('link', { name: /Настройки/ });
    expect(settings).toHaveAttribute('href', LANGUAGES);
  });

  it('shows the codes of an organization read earlier in this tab from the first frame', async () => {
    const { queryClient } = renderShell({
      path: GENERAL,
      auth,
      contextSource: source(),
      routes,
      access: 'viewer',
    });
    expect(await screen.findByRole('navigation', { name: 'Меню настроек' })).toBeInTheDocument();
    expect(queryClient.getQueryData(organizationsKeys.access(ORG))).toMatchObject({
      kind: 'member',
      role: 'viewer',
    });
  });
});

describe('mod-016 US-4 Scenario: a user in no organization', () => {
  it('still gets «Настройки» → «Языки» and nothing else in the settings navigation', async () => {
    const none = source({ organizations: [], selection: {} });
    renderShell({ path: LANGUAGES, auth, contextSource: none, access: 'unread' });

    const menu = await screen.findByRole('navigation', { name: 'Меню настроек' });
    expect(linksOf(menu)).toEqual(['Языки', 'Витрина компонентов']);
    expect(await screen.findByRole('link', { name: /Настройки/ })).toHaveAttribute(
      'href',
      LANGUAGES,
    );
  });
});

describe('mod-016 design § 2.2 Scenario: the navigation follows the served answer without a reload', () => {
  it('drops the organization’s entries when the tab learns the user is no longer a member', async () => {
    const { queryClient } = renderShell({
      path: LANGUAGES,
      auth,
      contextSource: source(),
      access: 'admin',
    });
    const menu = await screen.findByRole('navigation', { name: 'Меню настроек' });
    await waitFor(() => expect(linksOf(menu)).toHaveLength(10));

    act(() => {
      queryClient.setQueryData(organizationsKeys.access(ORG), { kind: 'not-member' });
    });

    await waitFor(() => expect(linksOf(menu)).toEqual(['Языки', 'Витрина компонентов']));
  });
});
