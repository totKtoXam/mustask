import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { formatDateTime } from '../../../shared/lib/format';
import { server } from '../../../test/mocks/server';
import { ORGANIZATIONS_URL, PROBLEM_HEADERS, ids, problem } from '../testFixtures';
import { ANNA, people } from '../testMembersData';
import { membersTable, openPage, openRowMenu, rowsOf } from '../testMembersPage';

// mod-016 design § 3.1–§ 3.4: «Участники» — a plain table of the organization's
// members, in the server's order, with the marks of design § 3.3, the actions
// only for the holder of `organizations.membership.write`, and the states of
// the page.

const CELLS = (name: string, email: string, role: string, added: string, changed: string) => [
  name,
  email,
  role,
  added,
  changed,
];

describe('mod-016 US-1 Scenario: the members of an organization', () => {
  it('loads with the five columns, then lists the members in the server’s order with who added them and when', async () => {
    openPage();

    const skeleton = screen.getByRole('status', { name: 'Загрузка данных' });
    for (const column of ['Имя', 'Почта', 'Роль', 'Добавлен', 'Роль изменена']) {
      expect(within(skeleton).getByText(column)).toBeInTheDocument();
    }

    const table = await membersTable();
    expect(
      within(table)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual(['Имя', 'Почта', 'Роль', 'Добавлен', 'Роль изменена', '']);
    expect(rowsOf(table).map((cells) => cells.slice(0, 5))).toEqual([
      CELLS(
        'Анна Смирнова',
        'anna@example.org',
        'Администратор',
        `Иван Петров, ${formatDateTime('2026-10-01T10:00:00+00:00')}`,
        '—Роль не менялась',
      ),
      CELLS(
        'Иван Петров',
        'ivan@example.org',
        'Администратор',
        `Анна Смирнова, ${formatDateTime('2026-10-01T10:05:00+00:00')}`,
        '—Роль не менялась',
      ),
      CELLS(
        'Олег Ивановнеактивен',
        'oleg@example.org',
        'Участник',
        `Анна Смирнова, ${formatDateTime('2026-10-02T09:15:00+00:00')}`,
        `Анна Смирнова, ${formatDateTime('2026-10-05T14:30:00+00:00')}`,
      ),
      CELLS(
        'Неизвестный пользователь',
        '',
        'Читатель',
        `Анна Смирнова, ${formatDateTime('2026-10-03T11:00:00+00:00')}`,
        '—Роль не менялась',
      ),
    ]);
  });

  it('shows the dash for a role that never changed to the eye only, and says «Роль не менялась» to the ear', async () => {
    openPage();
    const table = await membersTable();

    const first = within(table).getAllByRole('row')[1]!;
    const cell = within(first).getAllByRole('cell')[4]!;
    expect(cell.querySelector('[aria-hidden="true"]')).toHaveTextContent('—');
    expect(within(cell).getByText('Роль не менялась')).toBeInTheDocument();
  });

  it('marks an inactive member “неактивен” as text after the name, in «Имя» and in the «Добавлен» of rows that name them', async () => {
    const { data } = openPage();
    // Анна was added by Олег, whose account is disabled; the mark stays on
    // the name and the comma follows it (design § 3.3).
    const anna = data.memberships!.find((membership) => membership.userId === ANNA)!;
    anna.addedBy = people.oleg.id;
    const table = await membersTable();

    const rows = rowsOf(table);
    expect(rows[0]![3]).toBe(
      `Олег Ивановнеактивен, ${formatDateTime('2026-10-01T10:00:00+00:00')}`,
    );
    expect(within(table).getAllByText('неактивен')).toHaveLength(2);
    // The button’s name is the person, without the mark.
    expect(screen.getByRole('button', { name: 'Действия: Олег Иванов' })).toBeInTheDocument();
  });

  it('names an unknown user «Неизвестный пользователь» in secondary text, with an empty «Почта»', async () => {
    openPage();
    const table = await membersTable();

    const row = within(table).getAllByRole('row')[4]!;
    const [name, email] = within(row).getAllByRole('cell');
    expect(name).toHaveTextContent('Неизвестный пользователь');
    expect(email).toBeEmptyDOMElement();
    expect(
      screen.getByRole('button', { name: 'Действия: Неизвестный пользователь' }),
    ).toBeVisible();
  });

  it('has no empty state: a table is all there is, however few the members', async () => {
    const { data } = openPage();
    data.memberships = data.memberships!.filter(
      (membership) => membership.organizationId !== ids.alfa || membership.userId === ANNA,
    );

    const table = await membersTable();

    expect(within(table).getAllByRole('row')).toHaveLength(2);
    expect(screen.queryByText('Участников пока нет')).not.toBeInTheDocument();
  });
});

describe('mod-016 US-4: the controls of «Участники» follow the role (design § 6.2)', () => {
  it('shows an administrator «Добавить участника» and a «Действия: <имя>» button on every row', async () => {
    openPage({ role: 'admin' });
    const table = await membersTable();

    expect(screen.getByRole('button', { name: 'Добавить участника' })).toBeVisible();
    expect(
      within(table)
        .getAllByRole('button')
        .map((button) => button.getAttribute('aria-label')),
    ).toEqual([
      'Действия: Анна Смирнова',
      'Действия: Иван Петров',
      'Действия: Олег Иванов',
      'Действия: Неизвестный пользователь',
    ]);
  });

  it.each(['member', 'viewer'] as const)(
    'shows a %s the same table with no «Добавить участника», no actions column and no row buttons',
    async (role) => {
      openPage({ role });
      const table = await membersTable();

      expect(screen.queryByRole('button', { name: 'Добавить участника' })).not.toBeInTheDocument();
      expect(within(table).queryAllByRole('button')).toHaveLength(0);
      expect(within(table).getAllByRole('columnheader')).toHaveLength(5);
      expect(rowsOf(table)).toHaveLength(4);
    },
  );

  it('opens the menu of a row with «Изменить роль» and «Удалить из организации» in the error colour — on every row, the caller’s own included', async () => {
    openPage();
    await membersTable();

    for (const name of [
      'Анна Смирнова',
      'Иван Петров',
      'Олег Иванов',
      'Неизвестный пользователь',
    ]) {
      const menu = await openRowMenu(name);
      const items = within(menu).getAllByRole('menuitem');
      expect(items.map((item) => item.textContent)).toEqual([
        'Изменить роль',
        'Удалить из организации',
      ]);
      expect(getComputedStyle(items[1]!).color).not.toBe(getComputedStyle(items[0]!).color);
      await userEvent.keyboard('{Escape}');
      await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());
    }
  });
});

describe('mod-016 design § 3.4: the states of the page', () => {
  it('keeps the header and shows the server’s text with «Повторить» when the read fails, then the table once it succeeds', async () => {
    openPage();
    let failures = 1;
    server.use(
      http.get(`${ORGANIZATIONS_URL}/:organizationId/members`, () => {
        if (failures > 0) {
          failures -= 1;
          return HttpResponse.json(problem('organizations.access.forbidden'), {
            status: 403,
            headers: PROBLEM_HEADERS,
          });
        }
        return undefined;
      }),
    );

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Недостаточно прав');
    expect(screen.getByRole('heading', { level: 1, name: 'Участники' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Добавить участника' })).toBeVisible();

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(await membersTable()).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('answers «Организация не найдена» when the organization’s own read says so (no gate above the page)', async () => {
    openPage({ path: `/organizations/${ids.gamma}/settings/members` });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
  });

  it('has the h1 «Участники», the page’s only level-1 heading', async () => {
    openPage();
    await membersTable();

    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(screen.getByRole('heading', { level: 1, name: 'Участники' })).toBeVisible();
  });
});

describe('mod-016 design § 10: keyboard', () => {
  it('Tab reaches «Добавить участника» and then one «Действия: <имя>» per row in row order; text and headers are not stops', async () => {
    openPage();
    await membersTable();

    const stops: string[] = [];
    for (let step = 0; step < 5; step += 1) {
      await userEvent.tab();
      const focused = document.activeElement;
      stops.push(focused?.getAttribute('aria-label') ?? focused?.textContent ?? '');
    }

    expect(stops).toEqual([
      'Добавить участника',
      'Действия: Анна Смирнова',
      'Действия: Иван Петров',
      'Действия: Олег Иванов',
      'Действия: Неизвестный пользователь',
    ]);
  });

  it('opens a row menu on the first item with Enter, and Escape closes it and returns focus to «Действия»', async () => {
    openPage();
    await membersTable();
    const button = screen.getByRole('button', { name: 'Действия: Иван Петров' });
    button.focus();

    await userEvent.keyboard('{Enter}');
    const menu = await screen.findByRole('menu');
    await waitFor(() =>
      expect(within(menu).getByRole('menuitem', { name: 'Изменить роль' })).toHaveFocus(),
    );
    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());
    await waitFor(() => expect(button).toHaveFocus());
  });
});

describe('mod-016 design § 11: narrow widths', () => {
  it('scrolls the table sideways inside its own paper, with a floor width, names that truncate and dates that do not wrap', async () => {
    openPage();
    const table = await membersTable();

    const paper = table.closest('.MuiTableContainer-root');
    expect(paper).toBeInTheDocument();
    expect(getComputedStyle(paper!).overflowX).toBe('auto');
    expect(getComputedStyle(table).minWidth).toBe('720px');

    const first = within(table).getAllByRole('row')[1]!;
    const name = within(within(first).getAllByRole('cell')[0]!).getByText('Анна Смирнова');
    expect(getComputedStyle(name).textOverflow).toBe('ellipsis');
    expect(getComputedStyle(name).whiteSpace).toBe('nowrap');
    const stamp = within(first).getByText(`, ${formatDateTime('2026-10-01T10:00:00+00:00')}`);
    expect(getComputedStyle(stamp).whiteSpace).toBe('nowrap');
    const email = within(first).getByText('anna@example.org');
    expect(getComputedStyle(email).whiteSpace).toBe('nowrap');
  });
});
