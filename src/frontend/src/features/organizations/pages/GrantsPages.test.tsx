import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { GrantRow } from '../api/grantsApi';
import { ids } from '../testFixtures';
import { people } from '../testMembersData';
import { chooseRole, openPage, pickUser, rowsOf } from '../testMembersPage';

// mod-019 design §§ 2-3: the grants of a department, a project and a team — the states, the dialogs and the rows.

const AREA_PATH = `/organizations/${ids.alfa}/areas/${ids.platformArea}/members`;
const PROJECT_PATH = `/organizations/${ids.alfa}/projects/${ids.prm}/access`;
const TEAM_PATH = `/organizations/${ids.alfa}/teams/${ids.platformTeam}/members`;

function grant(person: { id: string; displayName: string; email: string }, role: GrantRow['role']) {
  return {
    user: { ...person, state: 'active' },
    role,
    origin: 'held',
    department: null,
    givenBy: { ...people.ivan },
    givenAt: '2026-10-20T10:00:00Z',
  } as GrantRow;
}

async function grantsTable() {
  return screen.findByRole('table', { name: 'Доступ', hidden: true });
}

describe('mod-019 grants: a department’s «Участники»', () => {
  it('says nobody holds access and offers «Выдать доступ» to the grant holder', async () => {
    openPage({ path: AREA_PATH });

    expect(
      await screen.findByRole('heading', { name: 'В департаменте пока никому не выдан доступ' }),
    ).toBeVisible();
    expect(screen.getByText('Выдайте роль, чтобы человек работал с объектом.')).toBeVisible();
    expect(screen.getAllByRole('button', { name: 'Выдать доступ' }).length).toBeGreaterThan(0);
  });

  it('shows a reader the same empty list with no command and no hint', async () => {
    openPage({ path: AREA_PATH, role: 'viewer' });

    expect(
      await screen.findByRole('heading', { name: 'В департаменте пока никому не выдан доступ' }),
    ).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Выдать доступ' })).toBeNull();
    expect(screen.queryByText('Выдайте роль, чтобы человек работал с объектом.')).toBeNull();
  });

  it('gives access with the lowest role preselected and lists the grantee', async () => {
    const { organizations } = openPage({ path: AREA_PATH });
    await screen.findByRole('heading', { name: 'В департаменте пока никому не выдан доступ' });

    await userEvent.click(screen.getAllByRole('button', { name: 'Выдать доступ' })[0]!);
    const dialog = await screen.findByRole('dialog', { name: 'Выдача доступа' });
    expect(within(dialog).getByRole('combobox', { name: 'Роль' })).toHaveTextContent('Читатель');
    await pickUser(dialog, 'Участник организации', 'Мар', /Мария Орлова/);
    await chooseRole(dialog, 'Менеджер');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Выдать' }));

    const table = await grantsTable();
    expect(rowsOf(table).map((cells) => cells.slice(0, 2))).toEqual([['Мария Орлова', 'Менеджер']]);
    expect(organizations.writes().map((write) => [write.method, write.body])).toEqual([
      ['POST', { userId: people.maria.id, role: 'manager' }],
    ]);
    expect(await screen.findByText('Пользователю «Мария Орлова» выдан доступ.')).toBeVisible();
  });
});

describe('mod-019 grants: rows, filter, revoke', () => {
  function seeded(path: string, key: string) {
    const opened = openPage({ path });
    opened.organizations.grants[key] = [
      grant(people.anna, 'manager'),
      grant(people.ivan, 'member'),
    ];
    return opened;
  }

  it('marks the caller’s row «Вы» and gives it «Отозвать доступ» only', async () => {
    seeded(AREA_PATH, `department:${ids.platformArea}`);
    await grantsTable();

    expect(await screen.findByText('Вы')).toBeVisible();
    await userEvent.click(
      screen.getByRole('button', { name: `Ещё действия: ${people.anna.displayName}` }),
    );
    const menu = await screen.findByRole('menu');
    expect(within(menu).queryByRole('menuitem', { name: 'Изменить роль' })).toBeNull();
    expect(within(menu).getByRole('menuitem', { name: 'Отозвать доступ' })).toBeVisible();
  });

  it('changes a role', async () => {
    const { organizations } = seeded(AREA_PATH, `department:${ids.platformArea}`);
    await grantsTable();

    await userEvent.click(
      screen.getByRole('button', { name: `Ещё действия: ${people.ivan.displayName}` }),
    );
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Изменить роль' }));
    const dialog = await screen.findByRole('dialog', { name: 'Изменение роли' });
    await chooseRole(dialog, 'Читатель');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await waitFor(() =>
      expect(organizations.writes().map((write) => [write.method, write.body])).toEqual([
        ['PUT', { role: 'reader' }],
      ]),
    );
    expect(await screen.findByText('Роль пользователя «Иван Петров» изменена.')).toBeVisible();
  });

  it('revokes after a confirmation naming the role and the object', async () => {
    const { organizations } = seeded(AREA_PATH, `department:${ids.platformArea}`);
    await grantsTable();

    await userEvent.click(
      screen.getByRole('button', { name: `Ещё действия: ${people.ivan.displayName}` }),
    );
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Отозвать доступ' }));
    const dialog = await screen.findByRole('dialog', { name: 'Отозвать доступ?' });
    expect(dialog).toHaveTextContent(
      'Иван Петров потеряет роль «Участник» на объекте «Платформа».',
    );
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отозвать' }));

    await waitFor(() =>
      expect(organizations.writes().map((write) => write.method)).toEqual(['DELETE']),
    );
    expect(await screen.findByText('Доступ пользователя «Иван Петров» отозван.')).toBeVisible();
  });

  it('asks the caller twice-worded when revoking their own access', async () => {
    seeded(AREA_PATH, `department:${ids.platformArea}`);
    await grantsTable();

    await userEvent.click(
      screen.getByRole('button', { name: `Ещё действия: ${people.anna.displayName}` }),
    );
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Отозвать доступ' }));

    expect(await screen.findByRole('dialog', { name: 'Отозвать свой доступ?' })).toBeVisible();
  });

  it('filters by name and says «Ничего не найдено» with a way out', async () => {
    seeded(AREA_PATH, `department:${ids.platformArea}`);
    await grantsTable();

    await userEvent.type(screen.getByRole('textbox', { name: 'Найти участника' }), 'яяя');

    expect(await screen.findByRole('heading', { name: 'Ничего не найдено' })).toBeVisible();
    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));
    expect(rowsOf(await grantsTable())).toHaveLength(2);
  });
});

describe('mod-019 grants: a project’s «Участники и доступ»', () => {
  it('shows the department’s rows muted, without a menu, and names where they come from', async () => {
    const opened = openPage({ path: PROJECT_PATH });
    opened.data.grantInheritance = {
      departmentId: ids.platformArea,
      departmentName: 'Платформа',
      inheritsAccess: true,
    };
    opened.organizations.grants[`project:${ids.prm}`] = [
      grant(people.ivan, 'member'),
      {
        ...grant(people.maria, 'reader'),
        origin: 'department',
        department: { id: ids.platformArea, name: 'Платформа' },
      },
    ];

    const table = await grantsTable();

    expect(rowsOf(table).map((cells) => cells.slice(0, 3))).toEqual([
      ['Иван Петров', 'Участник', 'В проекте'],
      ['Мария Орлова', 'Читатель', 'От департамента «Платформа»'],
    ]);
    expect(
      screen.queryByRole('button', { name: `Ещё действия: ${people.maria.displayName}` }),
    ).toBeNull();
    expect(
      screen.getByRole('button', { name: `Ещё действия: ${people.ivan.displayName}` }),
    ).toBeVisible();
  });

  it('says that the department’s access is switched off', async () => {
    const opened = openPage({ path: PROJECT_PATH });
    opened.data.grantInheritance = {
      departmentId: ids.platformArea,
      departmentName: 'Платформа',
      inheritsAccess: false,
    };

    expect(
      await screen.findByText('Доступ департамента «Платформа» в этом проекте отключён.'),
    ).toBeVisible();
  });
});

describe('mod-019 grants: a team’s two sections', () => {
  it('adds «Роль в команде» to the members and lists «Доступ к команде» below', async () => {
    const opened = openPage({ path: TEAM_PATH });
    opened.organizations.grants[`team:${ids.platformTeam}`] = [grant(people.ivan, 'member')];

    const members = await screen.findByRole('table', { name: 'Участники команды', hidden: true });

    expect(
      within(members)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual(['Имя', 'Почта', 'Роль в команде', '']);
    await waitFor(() =>
      expect(rowsOf(members).map((cells) => cells.slice(0, 3))).toEqual([
        ['Иван Петров', 'ivan@example.org', 'Участник'],
        ['Олег Ивановнеактивен', 'oleg@example.org', '—'],
      ]),
    );
    expect(
      await screen.findByRole('heading', { level: 2, name: 'Доступ к команде' }),
    ).toBeVisible();
  });
});
