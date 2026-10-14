import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { ORGANIZATIONS_URL, PROBLEM_HEADERS, ids, problem } from '../testFixtures';
import { ANNA, GHOST, people } from '../testMembersData';
import { PLATFORM_TEAM_PATH, openPage, pickUser, rowsOf, searchUsers } from '../testMembersPage';

// mod-016 US-3, design § 4: «Участники команды» on the team page — states,
// the add dialog, the immediate removal and the role views.

const PRM_FIRST_TEAM_PATH = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams/${ids.prmTeam}`;
const TEAM = 'Участник организации';
const MEMBERS_URL = `${ORGANIZATIONS_URL}/:organizationId/projects/:projectId/teams/:teamId/members`;

async function teamTable() {
  return screen.findByRole('table', { name: 'Участники команды', hidden: true });
}

function section() {
  return screen.getByRole('region', { name: 'Участники команды' });
}

async function openAddDialog() {
  await teamTable();
  await userEvent.click(within(section()).getByRole('button', { name: 'Добавить в команду' }));
  return screen.findByRole('dialog', { name: 'Добавление в команду' });
}

function deleteRequests(api: { requests: { method: string; path: string }[] }) {
  return api.requests.filter((request) => request.method === 'DELETE');
}

describe('mod-016 US-3 Scenario: the members of a team', () => {
  it('stands under the facts as the section «Участники команды», its table named by the heading', async () => {
    openPage({ path: PLATFORM_TEAM_PATH });

    const table = await teamTable();

    expect(screen.getByRole('heading', { level: 1, name: 'Команда: Платформа' })).toBeVisible();
    expect(screen.getByRole('heading', { level: 2, name: 'Участники команды' })).toBeVisible();
    expect(section()).toContainElement(table);
    // The facts come first, the members after.
    const facts = screen.getByText('Область по умолчанию');
    expect(facts.compareDocumentPosition(table) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(
      within(table)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual(['Имя', 'Почта', '']);
  });

  it('lists the members in the server’s order with the marks, an unknown user last with an empty «Почта»', async () => {
    const { data } = openPage({ path: PLATFORM_TEAM_PATH });
    data.teamMembers!.push({ teamId: ids.platformTeam, userId: GHOST });

    const table = await teamTable();

    expect(rowsOf(table).map((cells) => cells.slice(0, 2))).toEqual([
      ['Иван Петров', 'ivan@example.org'],
      ['Олег Ивановнеактивен', 'oleg@example.org'],
      ['Неизвестный пользователь', ''],
    ]);
    expect(screen.getByRole('button', { name: 'Действия: Олег Иванов' })).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Действия: Неизвестный пользователь' }),
    ).toBeVisible();
  });

  it('shows the section’s header with a skeleton while the members load, the facts being there already', async () => {
    openPage({ path: PLATFORM_TEAM_PATH });
    const held = gate();
    server.use(
      http.get(MEMBERS_URL, async () => {
        await held.opened;
        return undefined;
      }),
    );

    expect(await screen.findByText('Область по умолчанию')).toBeVisible();
    const skeleton = await screen.findByRole('status', { name: 'Загрузка данных' });
    expect(within(skeleton).getByText('Имя')).toBeVisible();
    expect(within(skeleton).getByText('Почта')).toBeVisible();
    expect(screen.getByRole('heading', { level: 2, name: 'Участники команды' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Добавить в команду' })).toBeVisible();

    held.open();
    expect(await teamTable()).toBeVisible();
  });

  it('keeps the header and the facts and shows the error with «Повторить» when the read fails', async () => {
    openPage({ path: PLATFORM_TEAM_PATH });
    let failures = 1;
    server.use(
      http.get(MEMBERS_URL, () => {
        if (failures > 0) {
          failures -= 1;
          return HttpResponse.json(problem('organizations.access.forbidden'), {
            status: 500,
            headers: PROBLEM_HEADERS,
          });
        }
        return undefined;
      }),
    );

    const alert = await screen.findByRole('alert');

    expect(screen.getByText('Область по умолчанию')).toBeVisible();
    expect(screen.getByRole('heading', { level: 2, name: 'Участники команды' })).toBeVisible();
    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));
    expect(await teamTable()).toBeVisible();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('asks the page to read its team again when the team turns out to be gone, and keeps its banner meanwhile', async () => {
    const { organizations } = openPage({ path: PLATFORM_TEAM_PATH });
    server.use(
      http.get(MEMBERS_URL, () =>
        HttpResponse.json(problem('organizations.team.not_found'), {
          status: 404,
          headers: PROBLEM_HEADERS,
        }),
      ),
    );
    const teamReads = () =>
      organizations.requests.filter(
        (request) =>
          request.method === 'GET' &&
          request.path ===
            `/api/organizations/${ids.alfa}/projects/${ids.prm}/teams/${ids.platformTeam}`,
      ).length;

    // The section’s own read answers «Команда не найдена»; the page, which owns
    // the team, is asked to read it again (and shows the frame if it is gone).
    expect(await screen.findByRole('alert')).toHaveTextContent('Команда не найдена');
    await waitFor(() => expect(teamReads()).toBeGreaterThanOrEqual(2));
  });
});

describe('mod-016 design § 4.3: the empty team', () => {
  it('shows an administrator the frame one level under the section’s heading, with the hint and a second «Добавить в команду»', async () => {
    openPage({ path: PRM_FIRST_TEAM_PATH });

    const frameHeading = await screen.findByRole('heading', {
      level: 3,
      name: 'В команде пока нет участников',
    });

    expect(screen.getByRole('heading', { level: 2, name: 'Участники команды' })).toBeVisible();
    expect(
      screen.getByText('Добавьте участников организации кнопкой «Добавить в команду».'),
    ).toBeVisible();
    expect(screen.getAllByRole('button', { name: 'Добавить в команду' })).toHaveLength(2);
    // The frame is the section’s content, in the look of the `h2` frame.
    expect(frameHeading.closest('.StateFrame-root')).toBeInTheDocument();
    expect(frameHeading.className).toContain('MuiTypography-h2');
    expect(screen.queryByRole('table', { name: 'Участники команды' })).not.toBeInTheDocument();
  });

  it.each(['member', 'viewer'] as const)(
    'shows a %s the same heading with «Состав команды задаёт администратор организации.» and no action',
    async (role) => {
      openPage({ path: PRM_FIRST_TEAM_PATH, role });

      expect(
        await screen.findByRole('heading', { level: 3, name: 'В команде пока нет участников' }),
      ).toBeVisible();

      expect(screen.getByText('Состав команды задаёт администратор организации.')).toBeVisible();
      expect(screen.queryByRole('button', { name: 'Добавить в команду' })).not.toBeInTheDocument();
    },
  );

  it('opens the add dialog from the frame’s action too', async () => {
    openPage({ path: PRM_FIRST_TEAM_PATH });
    await screen.findByRole('heading', { level: 3, name: 'В команде пока нет участников' });

    const frame = document.querySelector<HTMLElement>('.StateFrame-root')!;
    await userEvent.click(within(frame).getByRole('button', { name: 'Добавить в команду' }));

    expect(await screen.findByRole('dialog', { name: 'Добавление в команду' })).toBeVisible();
  });
});

describe('mod-016 design § 6.2: the role views of the team page', () => {
  it('shows an administrator the page’s commands, «Изменить», the section’s command and a «Действия» button per member', async () => {
    openPage({ path: PLATFORM_TEAM_PATH });
    await teamTable();

    for (const name of ['Переименовать', 'Удалить команду', 'Изменить область по умолчанию']) {
      expect(screen.getByRole('button', { name })).toBeVisible();
    }
    expect(within(section()).getByRole('button', { name: 'Добавить в команду' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Действия: Иван Петров' })).toBeVisible();
  });

  it.each(['member', 'viewer'] as const)(
    'shows a %s the facts and the members read-only: no commands, no «Изменить», no «Добавить в команду», no actions column',
    async (role) => {
      openPage({ path: PLATFORM_TEAM_PATH, role });
      const table = await teamTable();

      for (const name of [
        'Переименовать',
        'Удалить команду',
        'Изменить область по умолчанию',
        'Добавить в команду',
      ]) {
        expect(screen.queryByRole('button', { name })).not.toBeInTheDocument();
      }
      expect(within(table).queryAllByRole('button')).toHaveLength(0);
      expect(within(table).getAllByRole('columnheader')).toHaveLength(2);
      expect(screen.getByText('Платформа / API')).toBeVisible();
      expect(rowsOf(table)).toHaveLength(2);
    },
  );
});

describe('mod-016 US-3 Scenario: adding to a team (design § 4.4)', () => {
  it('opens on «Участник организации» with focus, the helper under it and «Добавить» disabled', async () => {
    openPage({ path: PLATFORM_TEAM_PATH });
    const dialog = await openAddDialog();

    await waitFor(() => expect(within(dialog).getByRole('combobox', { name: TEAM })).toHaveFocus());
    expect(within(dialog).getByText('Поиск среди активных участников организации')).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled();
  });

  it('offers the active members of the organization only: not a user of no organization, not a member whose account is disabled', async () => {
    openPage({ path: PLATFORM_TEAM_PATH });
    const dialog = await openAddDialog();

    await searchUsers(dialog, TEAM, 'Мар');
    expect(await screen.findByRole('option', { name: 'Пользователи не найдены' })).toBeVisible();

    const field = within(dialog).getByRole('combobox', { name: TEAM });
    await userEvent.clear(field);
    await userEvent.type(field, 'Оле');
    expect(await screen.findByRole('option', { name: 'Пользователи не найдены' })).toBeVisible();

    await userEvent.clear(field);
    await userEvent.type(field, 'Анн');
    expect(await screen.findByRole('option', { name: /Анна Смирнова/ })).toBeVisible();
    await userEvent.clear(field);
    await userEvent.type(field, 'Ива');
    // Already in the team, and still offered: the refusal comes on «Добавить».
    expect(await screen.findByRole('option', { name: /Иван Петров/ })).toBeVisible();
  });

  it('adds the chosen participant: the dialog closes, the table lists them with the e-mail, the notice names them without a mark and focus returns to the section’s command', async () => {
    const { members, data } = openPage({ path: PLATFORM_TEAM_PATH });
    const dialog = await openAddDialog();

    await pickUser(dialog, TEAM, 'Анн', /Анна Смирнова/);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(
      await screen.findByText('Пользователь «Анна Смирнова» добавлен в команду «Платформа».'),
    ).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const table = await teamTable();
    await waitFor(() =>
      expect(rowsOf(table).map((cells) => cells.slice(0, 2))).toEqual([
        ['Анна Смирнова', 'anna@example.org'],
        ['Иван Петров', 'ivan@example.org'],
        ['Олег Ивановнеактивен', 'oleg@example.org'],
      ]),
    );
    expect(members.requests.find((request) => request.method === 'POST')?.body).toEqual({
      userId: ANNA,
    });
    // Their organization role is untouched.
    expect(
      data.memberships!.find((m) => m.userId === ANNA && m.organizationId === ids.alfa)!.role,
    ).toBe('admin');
    await waitFor(() =>
      expect(within(section()).getByRole('button', { name: 'Добавить в команду' })).toHaveFocus(),
    );
  });

  it('gives focus to the section’s command when the team was empty and the frame’s action — the opener — is gone', async () => {
    openPage({ path: PRM_FIRST_TEAM_PATH });
    await screen.findByRole('heading', { level: 3, name: 'В команде пока нет участников' });
    const frame = document.querySelector<HTMLElement>('.StateFrame-root')!;
    await userEvent.click(within(frame).getByRole('button', { name: 'Добавить в команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление в команду' });

    await pickUser(dialog, TEAM, 'Анн', /Анна Смирнова/);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() =>
      expect(within(section()).getByRole('button', { name: 'Добавить в команду' })).toHaveFocus(),
    );
    expect(screen.queryByRole('heading', { level: 3 })).not.toBeInTheDocument();
  });

  it('puts «Пользователь уже состоит в команде» on the field, keeps the chosen user and reads the table in the background', async () => {
    const { members } = openPage({ path: PLATFORM_TEAM_PATH });
    const dialog = await openAddDialog();
    await pickUser(dialog, TEAM, 'Ива', /Иван Петров/);
    const reads = () =>
      members.requests.filter(
        (request) => request.method === 'GET' && /\/members$/.test(request.path),
      ).length;
    const before = reads();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    const field = within(dialog).getByRole('combobox', { name: TEAM });
    expect(await within(dialog).findByText('Пользователь уже состоит в команде')).toBeVisible();
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveValue('Иван Петров');
    expect(
      within(dialog).queryByText('Поиск среди активных участников организации'),
    ).not.toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'Добавление в команду' })).toBeVisible();
    await waitFor(() => expect(reads()).toBeGreaterThan(before));
  });

  it('puts «Добавить в команду можно только активного участника организации» on the field when the user left the organization after the search', async () => {
    const { data } = openPage({ path: PLATFORM_TEAM_PATH });
    const dialog = await openAddDialog();
    await pickUser(dialog, TEAM, 'Ива', /Иван Петров/);
    data.teamMembers = [];
    data.memberships = data.memberships!.filter(
      (membership) => membership.userId !== people.ivan.id,
    );

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(
      await within(dialog).findByText(
        'Добавить в команду можно только активного участника организации',
      ),
    ).toBeVisible();
    expect(screen.getByRole('dialog', { name: 'Добавление в команду' })).toBeVisible();
  });

  it('shows «Недостаточно прав» in the dialog’s alert with the chosen participant kept, and locks the primary once the code is gone', async () => {
    const { data, update } = openPage({ path: PLATFORM_TEAM_PATH });
    const dialog = await openAddDialog();
    await pickUser(dialog, TEAM, 'Анн', /Анна Смирнова/);
    data.memberships!.find(
      (membership) => membership.userId === ANNA && membership.organizationId === ids.alfa,
    )!.role = 'viewer';

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Недостаточно прав');
    expect(within(dialog).getByRole('combobox', { name: TEAM })).toHaveValue('Анна Смирнова');
    update({ role: 'viewer' });
    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled(),
    );
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});

describe('mod-016 US-3 Scenario: removing from a team (design § 4.4)', () => {
  it('acts at once, with no dialog: the row’s button is busy while the request runs and the other rows stay usable', async () => {
    const { members } = openPage({ path: PLATFORM_TEAM_PATH });
    await teamTable();
    const held = gate();
    server.use(
      http.delete(`${MEMBERS_URL}/:userId`, async () => {
        await held.opened;
        return undefined;
      }),
    );

    await userEvent.click(screen.getByRole('button', { name: 'Действия: Иван Петров' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Исключить из команды' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    const busy = screen.getByRole('button', { name: 'Действия: Иван Петров' });
    await waitFor(() => expect(busy).toBeDisabled());
    expect(within(busy).getByRole('progressbar')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Действия: Олег Иванов' })).toBeEnabled();

    held.open();
    await waitFor(() => expect(deleteRequests(members)).toHaveLength(1));
  });

  it('removes them: the row goes, the notice names them without a mark, their organization role is untouched and focus moves to the next row’s «Действия»', async () => {
    const { members, data } = openPage({ path: PLATFORM_TEAM_PATH });
    await teamTable();

    await userEvent.click(screen.getByRole('button', { name: 'Действия: Иван Петров' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Исключить из команды' }));

    expect(
      await screen.findByText('Пользователь «Иван Петров» исключён из команды «Платформа».'),
    ).toBeInTheDocument();
    const table = await teamTable();
    await waitFor(() =>
      expect(rowsOf(table).map((cells) => cells[0])).toEqual(['Олег Ивановнеактивен']),
    );
    expect(deleteRequests(members)[0]?.path).toBe(
      `/api/organizations/${ids.alfa}/projects/${ids.prm}/teams/${ids.platformTeam}/members/${people.ivan.id}`,
    );
    expect(data.memberships!.find((m) => m.userId === people.ivan.id)!.role).toBe('admin');
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Действия: Олег Иванов' })).toHaveFocus(),
    );
  });

  it('moves focus to the previous row’s «Действия» when the last row goes, and to the section’s command when none is left', async () => {
    const { data } = openPage({ path: PLATFORM_TEAM_PATH });
    data.teamMembers = [
      { teamId: ids.platformTeam, userId: people.ivan.id },
      { teamId: ids.platformTeam, userId: people.maria.id },
    ];
    data.memberships!.push({ organizationId: ids.alfa, userId: people.maria.id, role: 'member' });
    await teamTable();

    await userEvent.click(screen.getByRole('button', { name: 'Действия: Мария Орлова' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Исключить из команды' }));
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Действия: Иван Петров' })).toHaveFocus(),
    );

    await userEvent.click(screen.getByRole('button', { name: 'Действия: Иван Петров' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Исключить из команды' }));

    await screen.findByRole('heading', { level: 3, name: 'В команде пока нет участников' });
    await waitFor(() =>
      expect(
        within(section()).getAllByRole('button', { name: 'Добавить в команду' })[0],
      ).toHaveFocus(),
    );
  });

  it('keeps the row with focus on its button and shows «Участник команды не найден.» in the error notice when they are already gone, then reads the table again', async () => {
    const { data } = openPage({ path: PLATFORM_TEAM_PATH });
    await teamTable();
    data.teamMembers = data.teamMembers!.filter((entry) => entry.userId !== people.ivan.id);

    await userEvent.click(screen.getByRole('button', { name: 'Действия: Иван Петров' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Исключить из команды' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Участник команды не найден.');
    expect(alert).toHaveClass('MuiAlert-colorError');
    const table = await teamTable();
    await waitFor(() =>
      expect(rowsOf(table).map((cells) => cells[0])).toEqual(['Олег Ивановнеактивен']),
    );
  });

  it('shows «Недостаточно прав» in the error notice when the write is refused', async () => {
    const { data } = openPage({ path: PLATFORM_TEAM_PATH });
    await teamTable();
    data.memberships!.find(
      (membership) => membership.userId === ANNA && membership.organizationId === ids.alfa,
    )!.role = 'viewer';

    await userEvent.click(screen.getByRole('button', { name: 'Действия: Иван Петров' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Исключить из команды' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Недостаточно прав');
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Действия: Иван Петров' })).toHaveFocus(),
    );
  });
});
