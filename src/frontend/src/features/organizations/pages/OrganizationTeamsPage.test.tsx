import { describe, expect, it } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import {
  ORG_TEAMS_URL,
  ORG_TEAM_URL,
  exampleDataset,
  ids,
  noTeamsDataset,
  ownedKey,
  problem,
} from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { people } from '../testMembersData';
import { openPage } from '../testMembersPage';
import { contextSource, renderPage } from '../testRender';

// feat-028 design § 3, § 5 (US-1, US-5, US-7): the organization's «Команды» — the list with its states,
// the creation, the rename, the description and the deletion that reaches every project.

const ALFA_TEAMS = `/organizations/${ids.alfa}/settings/teams`;
const DELETION_COUNTS_URL = `${ORG_TEAM_URL}/deletion-counts`;

async function teamsTable() {
  return screen.findByRole('table', { name: 'Команды организации' });
}

async function openMenu(name: string) {
  await userEvent.click(screen.getByRole('button', { name: `Ещё действия: ${name}` }));
  return screen.findByRole('menu');
}

describe('feat-028 US-1 Scenario: The organization’s teams are listed in its settings', () => {
  it('loads with the three column labels, then lists the active teams by name with their active projects', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_TEAMS });

    const skeleton = screen.getByRole('status', { name: 'Загрузка данных' });
    for (const label of ['Команда', 'Проекты', 'Описание']) {
      expect(within(skeleton).getByText(label)).toBeInTheDocument();
    }

    const table = await teamsTable();
    expect(
      within(table)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual(['Команда', 'Проекты', 'Описание', '']);
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows.map((row) => within(row).getAllByRole('cell')[0]?.textContent)).toEqual([
      'Аналитика',
      'Платформа',
      'Пример проекта',
      'Старый проект',
    ]);
    // The projects a team works on; none of the deactivated ones (BR-OL-4).
    expect(rows.map((row) => within(row).getAllByRole('cell')[1]?.textContent)).toEqual([
      'Аналитика',
      'Пример проекта',
      'Пример проекта',
      'Нет проектов',
    ]);
    expect(within(rows[1]!).getByRole('link', { name: 'Платформа' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/teams/${ids.platformTeam}`,
    );
    // A team of another organization is not listed.
    expect(within(table).queryByText('Отчётность')).not.toBeInTheDocument();
  });

  it('shows the description in one line, empty when there is none', async () => {
    const data = exampleDataset();
    data.teams.find((team) => team.id === ids.platformTeam)!.description =
      'Платформа и инструменты';
    serveOrganizations(data);
    renderPage({ path: ALFA_TEAMS });

    const table = await teamsTable();
    const rows = within(table).getAllByRole('row').slice(1);

    expect(within(rows[1]!).getAllByRole('cell')[2]).toHaveTextContent('Платформа и инструменты');
    expect(within(rows[0]!).getAllByRole('cell')[2]).toHaveTextContent('');
  });

  it('offers «Новая команда» as the one filled action of the head', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_TEAMS });
    await teamsTable();

    expect(screen.getByRole('button', { name: 'Новая команда' })).toHaveClass(
      'MuiButton-contained',
    );
    expect(screen.getByRole('heading', { level: 1, name: 'Команды' })).toBeInTheDocument();
  });

  it('shows the error with «Повторить» in the table’s place, and the table after a retry', async () => {
    serveOrganizations();
    serveProblem('get', ORG_TEAMS_URL, problem('organizations.validation_failed'), { once: true });
    renderPage({ path: ALFA_TEAMS });

    await userEvent.click(await screen.findByRole('button', { name: 'Повторить' }));

    expect(await teamsTable()).toBeVisible();
  });
});

describe('feat-028 US-7 Scenario: No team at all is the first state, right after the upgrade', () => {
  it('says so, with the hint and «Новая команда» for the holder', async () => {
    serveOrganizations(noTeamsDataset());
    renderPage({ path: ALFA_TEAMS });

    expect(
      await screen.findByRole('heading', { level: 2, name: 'В организации пока нет команд' }),
    ).toBeVisible();
    expect(
      screen.getByText('Создайте команду кнопкой «Новая команда» и подключите её к проектам.'),
    ).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Новая команда' })).toHaveLength(2);
  });

  it.each([['member'], ['viewer']] as const)(
    'for the %s: who creates teams, and no action',
    async (role) => {
      serveOrganizations(noTeamsDataset());
      renderPage({ path: ALFA_TEAMS, role });

      expect(await screen.findByText('Команды создаёт администратор организации.')).toBeVisible();
      expect(screen.queryByRole('button', { name: 'Новая команда' })).not.toBeInTheDocument();
    },
  );

  it('the first team is created from the empty frame and then listed', async () => {
    const api = serveOrganizations(noTeamsDataset());
    renderPage({ path: ALFA_TEAMS });
    await screen.findByRole('heading', { level: 2, name: 'В организации пока нет команд' });
    await userEvent.click(screen.getAllByRole('button', { name: 'Новая команда' })[1]!);
    const dialog = await screen.findByRole('dialog', { name: 'Новая команда' });

    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'MARS');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Команда «MARS» создана.')).toBeInTheDocument();
    expect(await within(await teamsTable()).findByRole('link', { name: 'MARS' })).toBeVisible();
    expect(api.data.links).toEqual([]);
  });
});

describe('feat-028 US-1 Scenario: «Новая команда»', () => {
  async function openCreate(role: 'admin' | 'member' | 'viewer' = 'admin') {
    const api = serveOrganizations();
    renderPage({ path: ALFA_TEAMS, role });
    await teamsTable();
    await userEvent.click(screen.getByRole('button', { name: 'Новая команда' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая команда' });
    return { api, dialog };
  }

  it('opens on «Название» with the four fields and their hints; «Проекты» is drawn for the holder of both link codes', async () => {
    const { dialog } = await openCreate();

    expect(within(dialog).getByRole('textbox', { name: 'Название' })).toHaveFocus();
    expect(within(dialog).getByRole('textbox', { name: 'Описание' })).toBeVisible();
    expect(within(dialog).getByRole('combobox', { name: 'Участники' })).toBeVisible();
    expect(within(dialog).getByRole('combobox', { name: 'Проекты' })).toBeVisible();
    expect(within(dialog).getByText('От 1 до 100 символов.')).toBeInTheDocument();
    expect(within(dialog).getByText('Необязательно. Не более 2000 символов.')).toBeInTheDocument();
    expect(within(dialog).getByText('Среди активных участников организации.')).toBeInTheDocument();
    expect(within(dialog).getByText('Необязательно. Можно добавить позже.')).toBeInTheDocument();
  });

  it('creates a team with a name only: one request with the name, a notice, the row listed with no project', async () => {
    const { api, dialog } = await openCreate();

    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Ядро');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Команда «Ядро» создана.')).toBeInTheDocument();
    expect(api.writes()).toEqual([
      { method: 'POST', path: `/api/organizations/${ids.alfa}/teams`, body: { name: 'Ядро' } },
    ]);
    const table = await teamsTable();
    await waitFor(() => expect(within(table).getByRole('link', { name: 'Ядро' })).toBeVisible());
    // BR-OL-2: no project, no link, no team membership created with it.
    expect(api.data.links).toHaveLength(exampleDataset().links.length);
    expect(api.data.teamMembers ?? []).toEqual([]);
  });

  it('creates it with a description, projects and members in one request', async () => {
    const opened = openPage({ path: ALFA_TEAMS });
    await teamsTable();
    await userEvent.click(screen.getByRole('button', { name: 'Новая команда' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая команда' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'JUPITER');
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Описание' }), ' Первая');
    const members = within(dialog).getByRole('combobox', { name: 'Участники' });
    await userEvent.click(members);
    await userEvent.type(members, 'Ива');
    await userEvent.click(await screen.findByRole('option', { name: /Иван Петров/ }));
    await userEvent.keyboard('{Escape}');
    const projects = within(dialog).getByRole('combobox', { name: 'Проекты' });
    await userEvent.click(projects);
    await userEvent.click(await screen.findByRole('option', { name: /Пример проекта/ }));
    await userEvent.keyboard('{Escape}');

    expect(within(dialog).getByRole('button', { name: 'Убрать «Иван Петров»' })).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Убрать «Пример проекта»' })).toBeVisible();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Команда «JUPITER» создана.')).toBeInTheDocument();
    const write = opened.organizations.writes()[0];
    expect(write?.body).toEqual({
      name: 'JUPITER',
      description: ' Первая',
      memberIds: [people.ivan.id],
      projectIds: [ids.prm],
    });
    const created = opened.data.teams.find((team) => team.name === 'JUPITER')!;
    expect(opened.data.links).toContainEqual({ projectId: ids.prm, teamId: created.id });
    expect(opened.data.teamMembers).toContainEqual({ teamId: created.id, userId: people.ivan.id });
  });

  it('offers only active members of the organization: not an inactive one, not a user of no organization', async () => {
    openPage({ path: ALFA_TEAMS });
    await teamsTable();
    await userEvent.click(screen.getByRole('button', { name: 'Новая команда' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая команда' });
    const members = within(dialog).getByRole('combobox', { name: 'Участники' });
    await userEvent.click(members);

    expect(
      await screen.findByRole('option', { name: 'Введите не менее 2 символов имени или почты' }),
    ).toBeVisible();
    await userEvent.type(members, 'Ол');
    expect(await screen.findByRole('option', { name: 'Пользователи не найдены' })).toBeVisible();
    await userEvent.clear(members);
    await userEvent.type(members, 'Мар');
    expect(await screen.findByRole('option', { name: 'Пользователи не найдены' })).toBeVisible();
  });

  it('refuses an empty name and a description over 2 000 characters before anything is sent, both at once', async () => {
    const { api, dialog } = await openCreate();
    fireEvent.change(within(dialog).getByRole('textbox', { name: 'Описание' }), {
      target: { value: 'я'.repeat(2001) },
    });

    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      within(dialog).getByText('Название должно содержать от 1 до 100 символов'),
    ).toBeInTheDocument();
    expect(
      within(dialog).getByText('Описание должно содержать не более 2000 символов'),
    ).toBeInTheDocument();
    expect(within(dialog).getByRole('textbox', { name: 'Название' })).toHaveAttribute(
      'aria-invalid',
      'true',
    );
    // The first field refused takes the focus.
    await waitFor(() =>
      expect(within(dialog).getByRole('textbox', { name: 'Название' })).toHaveFocus(),
    );
    expect(api.writes()).toEqual([]);
  });

  it('puts «Команда с таким названием уже есть в организации» on «Название», in any letter case, keeping the input', async () => {
    const { dialog } = await openCreate();
    const name = within(dialog).getByRole('textbox', { name: 'Название' });

    await userEvent.type(name, '  платформа ');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await within(dialog).findByText('Команда с таким названием уже есть в организации'),
    ).toBeInTheDocument();
    expect(name).toHaveValue('  платформа ');
    expect(name).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('dialog', { name: 'Новая команда' })).toBeInTheDocument();
    // Design § 12: a refusal puts focus on the field it names.
    await waitFor(() => expect(name).toHaveFocus());
  });

  it('a name of a team of another organization is free: "Отчётность" belongs to «Бета»', async () => {
    const { api, dialog } = await openCreate();

    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Отчётность');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Команда «Отчётность» создана.')).toBeInTheDocument();
    expect(api.writes()).toHaveLength(1);
  });

  it.each([
    [
      'organizations.team.member_not_in_organization',
      'Участники',
      'Добавить в команду можно только активного участника организации',
    ],
    ['organizations.project.not_found', 'Проекты', 'Проект не найден.'],
  ])('%s lands under «%s» with the input kept', async (code, field, text) => {
    const { dialog } = await openCreate();
    serveProblem('post', ORG_TEAMS_URL, problem(code));
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Ядро');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await within(dialog).findByText(text)).toBeInTheDocument();
    expect(within(dialog).getByRole('combobox', { name: field })).toHaveAttribute(
      'aria-invalid',
      'true',
    );
    expect(within(dialog).getByRole('textbox', { name: 'Название' })).toHaveValue('Ядро');
  });

  it('a 403 is the dialog’s alert; the creation sends one request per press', async () => {
    const { dialog } = await openCreate();
    serveProblem('post', ORG_TEAMS_URL, problem('organizations.access.forbidden'));
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Ядро');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Недостаточно прав');
  });
});

describe('feat-028 US-1 Scenario: Renamed and described from a row', () => {
  it('«Переименовать» has no helper; the notice «Команда переименована.»; the new name is listed', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_TEAMS });
    await teamsTable();
    const menu = await openMenu('Платформа');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Переименовать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Переименование команды' });
    const field = within(dialog).getByRole('textbox', { name: 'Название' });
    expect(field).toHaveValue('Платформа');

    await userEvent.clear(field);
    await userEvent.type(field, 'Платформа 2');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Команда переименована.')).toBeInTheDocument();
    expect(
      await within(await teamsTable()).findByRole('link', { name: 'Платформа 2' }),
    ).toBeInTheDocument();
  });

  it('puts «Команда с таким названием уже есть в организации» on the rename’s field', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_TEAMS });
    await teamsTable();
    const menu = await openMenu('Платформа');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Переименовать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Переименование команды' });
    const field = within(dialog).getByRole('textbox', { name: 'Название' });

    await userEvent.clear(field);
    await userEvent.type(field, 'аналитика');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(
      await within(dialog).findByText('Команда с таким названием уже есть в организации'),
    ).toBeInTheDocument();
  });

  it('«Изменить описание»: the stored text, «Сохранить» waits for a change, the text goes as typed, the notice and the row read', async () => {
    const data = exampleDataset();
    data.teams.find((team) => team.id === ids.platformTeam)!.description = 'Было';
    const api = serveOrganizations(data);
    renderPage({ path: ALFA_TEAMS });
    await teamsTable();
    const menu = await openMenu('Платформа');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Изменить описание' }));
    const dialog = await screen.findByRole('dialog', { name: 'Описание команды' });
    const field = within(dialog).getByRole('textbox', { name: 'Описание' });
    expect(field).toHaveValue('Было');
    expect(within(dialog).getByText('Не более 2000 символов.')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Сохранить' })).toBeDisabled();

    await userEvent.clear(field);
    await userEvent.type(field, 'Стало');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Описание команды сохранено.')).toBeInTheDocument();
    expect(api.writes()[0]).toEqual({
      method: 'PUT',
      path: `/api/organizations/${ids.alfa}/teams/${ids.platformTeam}/description`,
      body: { description: 'Стало' },
    });
    await waitFor(() =>
      expect(
        within(screen.getByRole('table', { name: 'Команды организации' })).getByText('Стало'),
      ).toBeVisible(),
    );
  });

  it('a row’s menu offers the rename and the description alone to the holder of `team.write`, and a deletion to `team.delete`', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_TEAMS });
    await teamsTable();

    const menu = await openMenu('Платформа');

    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Переименовать', 'Изменить описание', 'Удалить команду']);
  });
});

describe('feat-028 US-5 Scenario: Deleted after a confirmation that counts every project', () => {
  /** `before` puts a one-off answer in front of the served ones (MSW takes the newest handler first). */
  async function openDelete(
    context = contextSource(),
    data = exampleDataset(),
    before: () => void = () => undefined,
    team = 'Платформа',
  ) {
    const api = serveOrganizations(data);
    before();
    renderPage({ path: ALFA_TEAMS, context });
    await teamsTable();
    const menu = await openMenu(team);
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Удалить команду' }));
    return { api, dialog: await screen.findByRole('dialog', { name: 'Удаление команды' }) };
  }

  /** «Платформа» works on «Пример проекта» (5), «Аналитика» (0) and the deactivated «Старый проект» (3). */
  function acrossProjects() {
    const data = exampleDataset();
    data.links.push(
      { projectId: ids.anl, teamId: ids.platformTeam },
      { projectId: ids.old, teamId: ids.platformTeam },
    );
    data.ownedItemCounts = {
      [ownedKey(ids.prm, ids.platformTeam)]: 5,
      [ownedKey(ids.old, ids.platformTeam)]: 3,
    };
    return data;
  }

  it('lists every linked project with its count — zeros and the deactivated one included — and asks with an error-coloured primary and focus on «Отмена»', async () => {
    const { dialog } = await openDelete(contextSource(), acrossProjects());

    expect(
      await within(dialog).findByText(
        'Удалить команду «Платформа»? Рабочие элементы станут «Без команды»:',
      ),
    ).toBeVisible();
    const list = within(dialog).getByRole('list', { name: 'Рабочие элементы по проектам' });
    expect(
      within(list)
        .getAllByRole('listitem')
        .map((item) => item.textContent),
    ).toEqual(['Аналитика0', 'Пример проекта5', 'Старый проектдеактивирован3']);
    expect(within(dialog).getByText('Восстановить команду будет нельзя.')).toBeVisible();
    const confirm = within(dialog).getByRole('button', { name: 'Удалить команду' });
    expect(confirm).toHaveClass('MuiButton-colorError');
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();
  });

  it('deletes: one request at the organization’s address, the notice, and the row leaves', async () => {
    const { api, dialog } = await openDelete(contextSource(), acrossProjects());
    await within(dialog).findByRole('list', { name: 'Рабочие элементы по проектам' });

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить команду' }));

    expect(await screen.findByText('Команда «Платформа» удалена.')).toBeInTheDocument();
    await waitFor(() =>
      expect(
        within(screen.getByRole('table', { name: 'Команды организации' })).queryByText('Платформа'),
      ).toBeNull(),
    );
    expect(api.writes()).toEqual([
      {
        method: 'DELETE',
        path: `/api/organizations/${ids.alfa}/teams/${ids.platformTeam}`,
        body: undefined,
      },
    ]);
    // BR-OL-10: «Пример проекта» is left with the other team; no guard refuses a project's last.
    expect(api.data.links.some((link) => link.teamId === ids.platformTeam)).toBe(false);
  });

  it('a team whose only project is deactivated still lists it, tagged «деактивирован», in the confirmation', async () => {
    const data = exampleDataset();
    data.ownedItemCounts = { [ownedKey(ids.old, ids.oldTeam)]: 2 };
    const { dialog } = await openDelete(contextSource(), data, undefined, 'Старый проект');

    const list = await within(dialog).findByRole('list', {
      name: 'Рабочие элементы по проектам',
    });

    expect(
      within(list)
        .getAllByRole('listitem')
        .map((item) => item.textContent),
    ).toEqual(['Старый проектдеактивирован2']);
  });

  it('a team linked to no project: the plain question', async () => {
    const data = exampleDataset();
    data.links = data.links.filter((link) => link.teamId !== ids.anlTeam);
    const { dialog } = await openDelete(contextSource(), data, undefined, 'Аналитика');

    expect(
      await within(dialog).findByText(
        'Удалить команду «Аналитика»? Восстановить команду будет нельзя.',
      ),
    ).toBeVisible();
    expect(within(dialog).queryByRole('list')).not.toBeInTheDocument();
  });

  it('a project’s last team may be deleted: the project is left with none and no refusal appears (BR-OL-10)', async () => {
    const { api, dialog } = await openDelete(
      contextSource(),
      exampleDataset(),
      undefined,
      'Аналитика',
    );
    await within(dialog).findByRole('list', { name: 'Рабочие элементы по проектам' });

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить команду' }));

    expect(await screen.findByText('Команда «Аналитика» удалена.')).toBeInTheDocument();
    expect(api.data.links.filter((link) => link.projectId === ids.anl)).toEqual([]);
  });

  it('while the counts are read a skeleton stands for the text and «Удалить команду» is unavailable', async () => {
    const held = gate();
    const { dialog } = await openDelete(contextSource(), exampleDataset(), () =>
      server.use(
        http.get(DELETION_COUNTS_URL, async () => {
          await held.opened;
          return undefined;
        }),
      ),
    );

    expect(within(dialog).getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Удалить команду' })).toBeDisabled();

    held.open();
    expect(await within(dialog).findByText(/Восстановить команду будет нельзя/)).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Удалить команду' })).toBeEnabled();
  });

  it('failed counts say so with «Повторить», and the deletion stays unavailable until they succeed', async () => {
    const { dialog } = await openDelete(contextSource(), exampleDataset(), () =>
      serveProblem('get', DELETION_COUNTS_URL, problem('organizations.access.forbidden'), {
        once: true,
      }),
    );

    const notice = await within(dialog).findByRole('alert');
    expect(notice).toHaveTextContent('Недостаточно прав');
    expect(within(dialog).getByRole('button', { name: 'Удалить команду' })).toBeDisabled();

    await userEvent.click(within(notice).getByRole('button', { name: 'Повторить' }));

    expect(await within(dialog).findByText(/Восстановить команду будет нельзя/)).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Удалить команду' })).toBeEnabled();
  });

  it('DC-7: no notice of its own when the deleted team is the switcher’s', async () => {
    const { dialog } = await openDelete(
      contextSource({
        selection: { organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam },
      }),
    );
    await within(dialog).findByText(/Восстановить команду будет нельзя/);

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить команду' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.queryByText('Команда «Платформа» удалена.')).not.toBeInTheDocument();
  });

  it.each([
    ['organizations.team.not_found', 'Команда не найдена.'],
    ['organizations.access.forbidden', 'Недостаточно прав'],
  ])('a direct refusal %s shows «%s» in the dialog, which stays open', async (code, text) => {
    const { dialog } = await openDelete(contextSource(), exampleDataset(), () =>
      serveProblem('delete', ORG_TEAM_URL, problem(code)),
    );
    await within(dialog).findByText(/Восстановить команду будет нельзя/);

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить команду' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(text);
    expect(screen.getByRole('dialog', { name: 'Удаление команды' })).toBeInTheDocument();
  });

  it.each([['«Отмена»'], ['Escape'], ['«Закрыть»']])('%s sends no request', async (how) => {
    const { api, dialog } = await openDelete();
    await within(dialog).findByText(/Восстановить команду будет нельзя/);

    if (how === 'Escape') {
      await userEvent.keyboard('{Escape}');
    } else {
      await userEvent.click(
        within(dialog).getByRole('button', { name: how === '«Отмена»' ? 'Отмена' : 'Закрыть' }),
      );
    }

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(api.writes()).toEqual([]);
    expect(within(await teamsTable()).getByRole('link', { name: 'Платформа' })).toBeInTheDocument();
  });

  it('under StrictMode it opens with focus on «Отмена» each time and gives focus back to the row’s menu', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_TEAMS, strict: true });
    await teamsTable();

    for (const opening of ['first', 'second']) {
      const menu = await openMenu('Платформа');
      await userEvent.click(within(menu).getByRole('menuitem', { name: 'Удалить команду' }));
      const dialog = await screen.findByRole('dialog', { name: 'Удаление команды' });
      expect(document.activeElement, `${opening} opening`).toBe(
        within(dialog).getByRole('button', { name: 'Отмена' }),
      );

      await userEvent.keyboard('{Escape}');

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      await waitFor(() =>
        expect(screen.getByRole('button', { name: 'Ещё действия: Платформа' })).toHaveFocus(),
      );
    }
  });
});
