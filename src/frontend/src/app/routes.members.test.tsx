import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AuthContext } from '../features/auth/AuthContext';
import { ids } from '../features/organizations/testFixtures';
import { serveOrganizations } from '../features/organizations/testApi';
import { ANNA, membersDataset } from '../features/organizations/testMembersData';
import { serveMemberships } from '../features/organizations/testMembership';
import type { TestRole } from '../test/organizationRoles';
import { authValue, LANGUAGES_READ, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

// mod-016 US-1, US-4, design § 2.2, § 3.6, § 3.7: «Участники» in the real
// shell, under the real gate — what a page test cannot see: the served codes
// changing under an open page, the page the user is removed from, and the
// notices of the shell beside the page's own.

const MEMBERS = `/organizations/${ids.alfa}/settings/members`;

function renderMembers({
  role = 'admin' as TestRole,
  path = MEMBERS,
  data = membersDataset(),
} = {}) {
  data.memberships!.find(
    (membership) => membership.organizationId === ids.alfa && membership.userId === ANNA,
  )!.role = role;
  serveOrganizations(data);
  const members = serveMemberships(data);
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={{ ...authValue([LANGUAGES_READ]), subject: ANNA }}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
          <LocationProbe />
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
  return { data, members, queryClient };
}

async function settledTable() {
  const table = await screen.findByRole('table', { name: 'Участники организации' });
  await waitFor(() =>
    expect(screen.queryByRole('status', { name: 'Загрузка данных' })).not.toBeInTheDocument(),
  );
  return table;
}

function settingsLinks(): string[] {
  return within(screen.getByRole('navigation', { name: 'Меню настроек' }))
    .getAllByRole('link')
    .map((link) => link.textContent ?? '');
}

describe('mod-016 US-4 Scenario: «Участники» in the navigation and under the gate', () => {
  it.each(['admin', 'member', 'viewer'] as const)(
    'a %s finds «Участники» between «Команды» and «Процесс» and reads the table',
    async (role) => {
      renderMembers({ role });

      await settledTable();

      expect(settingsLinks().slice(0, 5)).toEqual([
        'Общие',
        'Проекты',
        'Команды',
        'Участники',
        'Процесс',
      ]);
      expect(
        within(screen.getByRole('navigation', { name: 'Меню настроек' })).getByRole('link', {
          name: 'Участники',
          current: 'page',
        }),
      ).toBeVisible();
      expect(screen.getByRole('heading', { level: 1, name: 'Участники' })).toBeVisible();
      expect(screen.getByRole('navigation', { name: 'Путь к странице' })).toHaveTextContent(
        'Настройки/Организация',
      );
      const canWrite = role === 'admin';
      expect(screen.queryByRole('button', { name: 'Добавить участника' }) !== null).toBe(canWrite);
      expect(screen.queryAllByRole('button', { name: /^Действия: / })).toHaveLength(
        canWrite ? 4 : 0,
      );
    },
  );

  it('answers «Организация не найдена» to a user who is not a member, with no table', async () => {
    renderMembers({ path: `/organizations/${ids.gamma}/settings/members` });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });
});

describe('mod-016 design § 3.6 Scenario: an administrator steps down', () => {
  it('keeps the page, drops «Добавить участника» and every «Действия» with no reload, reads «Участник», notices once and moves focus to the main region', async () => {
    const { members } = renderMembers();
    const table = await settledTable();
    await userEvent.click(screen.getByRole('button', { name: 'Действия: Анна Смирнова' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Изменить роль' }));
    const dialog = await screen.findByRole('dialog', { name: 'Изменение роли' });
    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Роль' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Участник' }));
    expect(
      within(dialog).getByText(
        'Вы потеряете права администратора. Вернуть их сможет только другой администратор.',
      ),
    ).toBeVisible();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(
      await screen.findByText('Роль пользователя «Анна Смирнова» изменена.'),
    ).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: 'Добавить участника' })).not.toBeInTheDocument(),
    );
    expect(screen.queryAllByRole('button', { name: /^Действия: / })).toHaveLength(0);
    // The same table, redrawn in place: no reload, no remount.
    expect(table.isConnected).toBe(true);
    expect(within(table).getAllByRole('columnheader')).toHaveLength(5);
    const own = within(table).getAllByRole('row')[1]!;
    expect(within(own).getAllByRole('cell')[2]).toHaveTextContent('Участник');
    await waitFor(() => expect(screen.getByRole('main')).toHaveFocus());
    // The user’s own change speaks through its own notice, not the role-change one.
    expect(screen.queryByText(/Ваша роль в организации/)).not.toBeInTheDocument();
    expect(members.requests.filter((request) => request.method === 'PUT')).toHaveLength(1);
  });
});

describe('mod-016 design § 3.7 Scenario: a member removes themselves', () => {
  it('opens the organization list without the organization, lets the switcher fall back with its notice and moves focus to the main region', async () => {
    renderMembers();
    await settledTable();
    await userEvent.click(screen.getByRole('button', { name: 'Действия: Анна Смирнова' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Удалить из организации' }));
    const dialog = await screen.findByRole('dialog', { name: 'Удалить себя из организации?' });

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    await waitFor(() => expect(screen.getByTestId('location').textContent).toBe('/organizations'));
    expect(await screen.findByRole('heading', { level: 1, name: 'Организации' })).toBeVisible();
    await waitFor(() => expect(screen.getByRole('main')).toHaveFocus());
    // «Бета» is the other organization: the switcher falls back to it and its
    // notice is the only one.
    expect(
      await screen.findByText(/Ранее выбранный контекст недоступен\. Выбрано: Бета/),
    ).toBeInTheDocument();
    expect(screen.queryByText(/Вы больше не состоите/)).not.toBeInTheDocument();
    expect(await screen.findByRole('link', { name: 'Бета' })).toBeVisible();
    expect(screen.queryByRole('link', { name: 'Альфа' })).not.toBeInTheDocument();
  });

  it('says «Вы больше не состоите в организации «Альфа».» when the organization was the user’s only one', async () => {
    const data = membersDataset();
    data.memberships = data.memberships!.filter(
      (membership) => membership.organizationId === ids.alfa,
    );
    renderMembers({ data });
    await settledTable();
    await userEvent.click(screen.getByRole('button', { name: 'Действия: Анна Смирнова' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Удалить из организации' }));
    const dialog = await screen.findByRole('dialog', { name: 'Удалить себя из организации?' });

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    expect(
      await screen.findByText('Вы больше не состоите в организации «Альфа».'),
    ).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('location').textContent).toBe('/organizations'));
    expect(
      await screen.findByRole('heading', {
        level: 2,
        name: 'Вы пока не состоите ни в одной организации',
      }),
    ).toBeVisible();
  });
});

describe('mod-016 design § 2.2, § 2.3 Scenario: the codes change behind the open page', () => {
  it('turns the page into «Организация не найдена» at once when a dialog’s write finds the user removed: no alert, the dialog gone, focus in the main region', async () => {
    const { data } = renderMembers();
    await settledTable();
    await userEvent.click(screen.getByRole('button', { name: 'Добавить участника' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление участника' });
    const field = within(dialog).getByRole('combobox', { name: 'Пользователь' });
    await userEvent.click(field);
    await userEvent.type(field, 'Мария');
    await userEvent.click(await screen.findByRole('option', { name: /Мария Орлова/ }));
    // Another administrator removed the caller from the organization.
    data.memberships = data.memberships!.filter(
      (membership) => !(membership.organizationId === ids.alfa && membership.userId === ANNA),
    );

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeVisible();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    // No refusal of its own: the dialog went with the page. The only alert is
    // the switcher’s notice that the selection fell back.
    expect(document.querySelector('.MuiAlert-colorError')).toBeNull();
    expect(screen.queryByText('Организация не найдена.')).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('main')).toHaveFocus());
  });

  it('shows «Недостаточно прав» in the dialog, then the role-change notice, with the primary locked and the page’s controls gone', async () => {
    const { data } = renderMembers();
    await settledTable();
    await userEvent.click(screen.getByRole('button', { name: 'Добавить участника' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление участника' });
    const field = within(dialog).getByRole('combobox', { name: 'Пользователь' });
    await userEvent.click(field);
    await userEvent.type(field, 'Мария');
    await userEvent.click(await screen.findByRole('option', { name: /Мария Орлова/ }));
    // The caller was made a reader behind the open page.
    data.memberships!.find(
      (membership) => membership.organizationId === ids.alfa && membership.userId === ANNA,
    )!.role = 'viewer';

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Недостаточно прав');
    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled(),
    );
    expect(
      await screen.findByText('Ваша роль в организации «Альфа» изменилась: «Читатель».'),
    ).toBeInTheDocument();
    expect(within(dialog).getByRole('combobox', { name: 'Пользователь' })).toHaveValue(
      'Мария Орлова',
    );
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Добавить участника' })).not.toBeInTheDocument();
  });
});
