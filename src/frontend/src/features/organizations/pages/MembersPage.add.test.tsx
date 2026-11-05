import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { formatDateTime } from '../../../shared/lib/format';
import { server } from '../../../test/mocks/server';
import { ORGANIZATIONS_URL, PROBLEM_HEADERS, catalogText, ids, problem } from '../testFixtures';
import { ANNA, people } from '../testMembersData';
import {
  chooseRole,
  listedLines,
  membersTable,
  openPage,
  pickUser,
  rowsOf,
  searchUsers,
} from '../testMembersPage';

// mod-016 US-1 (add), design § 3.5: «Добавление участника» — a search by name or
// e-mail with the hint lines of the spec, «Роль» with its helper, and the
// refusals of the server on the user field.

const USER = 'Пользователь';
const NO_SEARCH_YET = 'Введите не менее 2 символов имени или почты';

async function openAddDialog() {
  await membersTable();
  await userEvent.click(screen.getByRole('button', { name: 'Добавить участника' }));
  return screen.findByRole('dialog', { name: 'Добавление участника' });
}

function candidateRequests(api: { requests: { method: string; path: string }[] }) {
  return api.requests.filter((request) => request.path.includes('/member-candidates'));
}

function memberReads(api: { requests: { method: string; path: string }[] }) {
  return api.requests.filter(
    (request) => request.method === 'GET' && /\/members$/.test(request.path),
  );
}

describe('mod-016 design § 3.5: the dialog', () => {
  it('opens on «Пользователь» with focus, «Роль» at «Участник» with its helper, and «Добавить» disabled', async () => {
    openPage();
    const dialog = await openAddDialog();

    await waitFor(() => expect(within(dialog).getByRole('combobox', { name: USER })).toHaveFocus());
    expect(within(dialog).getByRole('combobox', { name: 'Роль' })).toHaveTextContent('Участник');
    expect(
      within(dialog).getByText(
        'Просматривает всё и работает с рабочими элементами: создаёт, меняет, связывает и упорядочивает их.',
      ),
    ).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled();
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toBeEnabled();
  });

  it('says what each role may do under «Роль», and offers the three roles', async () => {
    openPage();
    const dialog = await openAddDialog();

    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Роль' }));
    expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual([
      'Администратор',
      'Участник',
      'Читатель',
    ]);
    await userEvent.click(screen.getByRole('option', { name: 'Читатель' }));

    expect(within(dialog).getByText('Только просматривает, ничего не меняет.')).toBeVisible();
    await chooseRole(dialog, 'Администратор');
    expect(
      within(dialog).getByText(
        'Управляет организацией, участниками, проектами, командами и областями, работает с рабочими элементами.',
      ),
    ).toBeVisible();
  });

  it('limits the search text to 100 characters', async () => {
    openPage();
    const dialog = await openAddDialog();

    expect(within(dialog).getByRole('combobox', { name: USER })).toHaveAttribute(
      'maxlength',
      '100',
    );
  });
});

describe('mod-016 US-1 Scenario: the search states of the spec', () => {
  it('shows the hint for under 2 characters and sends no request', async () => {
    const { members } = openPage();
    const dialog = await openAddDialog();

    await searchUsers(dialog, USER, 'М');
    await new Promise((resolve) => setTimeout(resolve, 400));

    expect(listedLines()).toEqual([NO_SEARCH_YET]);
    expect(screen.getByRole('option', { name: NO_SEARCH_YET })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    expect(candidateRequests(members)).toHaveLength(0);
  });

  it('counts the trimmed text: two spaces and a letter are still a short text', async () => {
    const { members } = openPage();
    const dialog = await openAddDialog();

    await searchUsers(dialog, USER, '  М ');
    await new Promise((resolve) => setTimeout(resolve, 400));

    expect(listedLines()).toEqual([NO_SEARCH_YET]);
    expect(candidateRequests(members)).toHaveLength(0);
  });

  it('shows «Поиск…» while the settled text is asked, then the candidates with their e-mail below the name', async () => {
    const { members } = openPage();
    const dialog = await openAddDialog();

    await searchUsers(dialog, USER, 'Ма');
    expect(listedLines()).toEqual(['Поиск…']);

    const maria = await screen.findByRole('option', { name: /Мария Орлова/ });
    expect(maria).toHaveTextContent('maria@example.org');
    expect(listedLines()).toEqual(['Мария Орловаmaria@example.org', 'Марк Орлов']);
    expect(candidateRequests(members).map((request) => request.path)).toEqual([
      `/api/organizations/${ids.alfa}/member-candidates?text=${encodeURIComponent('Ма')}`,
    ]);
  });

  it('asks the trimmed text once typing has settled', async () => {
    const { members } = openPage();
    const dialog = await openAddDialog();

    await searchUsers(dialog, USER, ' Мар ');
    await screen.findByRole('option', { name: /Марк Орлов/ });

    const asked = candidateRequests(members).map((request) =>
      new URL(`http://x${request.path}`).searchParams.get('text'),
    );
    expect(asked).toEqual(['Мар']);
  });

  it('says «Пользователи не найдены» when nobody matches', async () => {
    openPage();
    const dialog = await openAddDialog();

    await searchUsers(dialog, USER, 'Яяя');

    expect(await screen.findByRole('option', { name: 'Пользователи не найдены' })).toBeVisible();
    expect(listedLines()).toEqual(['Пользователи не найдены']);
  });

  it('cuts at the first 20 with «Показаны первые 20. Уточните запрос.» last', async () => {
    const { data } = openPage();
    for (let number = 1; number <= 25; number += 1) {
      const label = String(number).padStart(2, '0');
      data.users!.push({
        id: `user-test-${label}`,
        displayName: `Тест ${label}`,
        email: `test${label}@example.org`,
        state: 'active',
      });
    }
    const dialog = await openAddDialog();

    await searchUsers(dialog, USER, 'Тест');

    await screen.findByRole('option', { name: /Тест 01/ });
    const lines = listedLines();
    expect(lines).toHaveLength(21);
    expect(lines[0]).toContain('Тест 01');
    expect(lines[19]).toContain('Тест 20');
    expect(lines[20]).toBe('Показаны первые 20. Уточните запрос.');
    expect(screen.getByRole('option', { name: lines[20]! })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
  });

  it('never offers an inactive user or one the projection does not hold, and offers a current member', async () => {
    openPage();
    const dialog = await openAddDialog();

    await searchUsers(dialog, USER, 'Бор');
    expect(await screen.findByRole('option', { name: 'Пользователи не найдены' })).toBeVisible();

    const field = within(dialog).getByRole('combobox', { name: USER });
    await userEvent.clear(field);
    await userEvent.type(field, 'Ива');
    // Иван Петров is a member (offered, refused on «Добавить»); Олег Иванов is inactive.
    expect(await screen.findByRole('option', { name: /Иван Петров/ })).toBeVisible();
    expect(screen.queryByRole('option', { name: /Олег Иванов/ })).not.toBeInTheDocument();
  });

  it('says «Не удалось выполнить поиск. Попробуйте ещё раз.» when the search fails, and opening the list again retries', async () => {
    openPage();
    let failures = 1;
    server.use(
      http.get(`${ORGANIZATIONS_URL}/:organizationId/member-candidates`, () => {
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
    const dialog = await openAddDialog();

    const field = await searchUsers(dialog, USER, 'Ма');
    expect(
      await screen.findByRole('option', {
        name: 'Не удалось выполнить поиск. Попробуйте ещё раз.',
      }),
    ).toBeVisible();

    // The first Escape closes the open list, not the dialog; opening it again retries.
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
    expect(screen.getByRole('dialog', { name: 'Добавление участника' })).toBeVisible();
    await userEvent.click(field);
    await userEvent.keyboard('{ArrowDown}');

    expect(await screen.findByRole('option', { name: /Мария Орлова/ })).toBeVisible();
  });

  it('closes the open list with the first Escape and the dialog with the next', async () => {
    openPage();
    const dialog = await openAddDialog();
    await searchUsers(dialog, USER, 'Ма');
    await screen.findByRole('option', { name: /Мария Орлова/ });

    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
    expect(screen.getByRole('dialog', { name: 'Добавление участника' })).toBeVisible();

    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});

describe('mod-016 US-1 Scenario: adding a member', () => {
  it('sends the chosen user and role, closes, reads the list again and notices without a mark', async () => {
    const { members } = openPage({ clock: () => '2026-10-12T08:00:00+00:00' });
    const dialog = await openAddDialog();

    await pickUser(dialog, USER, 'Ма', /Мария Орлова/);
    expect(within(dialog).getByRole('combobox', { name: USER })).toHaveValue('Мария Орлова');
    await chooseRole(dialog, 'Читатель');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(
      await screen.findByText('Пользователь «Мария Орлова» добавлен в организацию.'),
    ).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(members.requests.find((request) => request.method === 'POST')?.body).toEqual({
      userId: people.maria.id,
      role: 'viewer',
    });
    const table = await membersTable();
    await waitFor(() =>
      expect(rowsOf(table).map((cells) => cells[0])).toEqual([
        'Анна Смирнова',
        'Иван Петров',
        'Мария Орлова',
        'Олег Ивановнеактивен',
        'Неизвестный пользователь',
      ]),
    );
    const row = rowsOf(table)[2]!;
    expect(row.slice(2, 5)).toEqual([
      'Читатель',
      `Анна Смирнова, ${formatDateTime('2026-10-12T08:00:00+00:00')}`,
      '—Роль не менялась',
    ]);
  });

  it('gives focus back to «Добавить участника»', async () => {
    openPage();
    const dialog = await openAddDialog();
    await pickUser(dialog, USER, 'Ма', /Мария Орлова/);

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Добавить участника' })).toHaveFocus(),
    );
  });

  it('submits with Enter in a settled field, through the form', async () => {
    const { members } = openPage();
    const dialog = await openAddDialog();
    await pickUser(dialog, USER, 'Ма', /Мария Орлова/);

    await userEvent.keyboard('{Enter}');

    await waitFor(() =>
      expect(members.requests.filter((request) => request.method === 'POST')).toHaveLength(1),
    );
  });

  it('keeps «Добавить» disabled after the chosen user is cleared', async () => {
    openPage();
    const dialog = await openAddDialog();
    await pickUser(dialog, USER, 'Ма', /Мария Орлова/);
    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeEnabled();

    await userEvent.clear(within(dialog).getByRole('combobox', { name: USER }));

    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled();
  });
});

describe('mod-016 design § 3.5, § 9: the refusals of an add', () => {
  it('puts «Пользователь уже состоит в организации» on «Пользователь», keeps the chosen user, reads the list in the background and lets another be picked', async () => {
    const { members } = openPage();
    const dialog = await openAddDialog();
    await pickUser(dialog, USER, 'Ива', /Иван Петров/);
    const readsBefore = memberReads(members).length;

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    const field = within(dialog).getByRole('combobox', { name: USER });
    expect(await within(dialog).findByText('Пользователь уже состоит в организации')).toBeVisible();
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveValue('Иван Петров');
    expect(screen.getByRole('dialog', { name: 'Добавление участника' })).toBeVisible();
    expect(within(dialog).queryByRole('alert')).not.toBeInTheDocument();
    await waitFor(() => expect(memberReads(members).length).toBeGreaterThan(readsBefore));

    await userEvent.clear(field);
    await pickUser(dialog, USER, 'Ма', /Мария Орлова/);
    expect(field).toHaveAttribute('aria-invalid', 'false');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));
    expect(
      await screen.findByText('Пользователь «Мария Орлова» добавлен в организацию.'),
    ).toBeInTheDocument();
  });

  it('puts «Этого пользователя нельзя добавить в организацию» on «Пользователь» when the account was disabled after the search', async () => {
    const { data } = openPage();
    const dialog = await openAddDialog();
    await pickUser(dialog, USER, 'Ма', /Мария Орлова/);
    data.users!.find((user) => user.id === people.maria.id)!.state = 'inactive';

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(
      await within(dialog).findByText('Этого пользователя нельзя добавить в организацию'),
    ).toBeVisible();
    expect(within(dialog).getByRole('combobox', { name: USER })).toHaveAttribute(
      'aria-invalid',
      'true',
    );
    expect(screen.getByRole('dialog', { name: 'Добавление участника' })).toBeVisible();
  });

  it('shows any other refusal as the dialog’s alert, with what was chosen kept', async () => {
    openPage();
    server.use(
      http.post(`${ORGANIZATIONS_URL}/:organizationId/members`, () =>
        HttpResponse.json(problem('organizations.validation_failed'), {
          status: 400,
          headers: PROBLEM_HEADERS,
        }),
      ),
    );
    const dialog = await openAddDialog();
    await pickUser(dialog, USER, 'Ма', /Мария Орлова/);
    await chooseRole(dialog, 'Читатель');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      catalogText('organizations.validation_failed'),
    );
    expect(within(dialog).getByRole('combobox', { name: USER })).toHaveValue('Мария Орлова');
    expect(within(dialog).getByRole('combobox', { name: 'Роль' })).toHaveTextContent('Читатель');
  });
});

describe('mod-016 design § 2.3: «Недостаточно прав» in the dialog', () => {
  it('shows the server’s text in the dialog’s alert with what was typed kept, and locks the primary once the code is gone', async () => {
    const { data, members, update } = openPage();
    const dialog = await openAddDialog();
    await pickUser(dialog, USER, 'Ма', /Мария Орлова/);
    // The caller was made a member behind the open page: the write is refused.
    data.memberships!.find(
      (membership) => membership.userId === ANNA && membership.role === 'admin',
    )!.role = 'member';

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    const alert = await within(dialog).findByRole('alert');
    expect(alert).toHaveTextContent('Недостаточно прав');
    expect(within(dialog).getByRole('combobox', { name: USER })).toHaveValue('Мария Орлова');
    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeEnabled();

    // The codes are read again and show the role changed: the dialog stays, its
    // primary is a reason of state, Enter submits nothing, «Отмена» works.
    update({ role: 'member' });
    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled(),
    );
    expect(within(dialog).getByRole('alert')).toHaveTextContent('Недостаточно прав');
    const posts = () => members.requests.filter((request) => request.method === 'POST').length;
    const sent = posts();
    within(dialog).getByRole('combobox', { name: USER }).focus();
    await userEvent.keyboard('{Enter}');
    expect(posts()).toBe(sent);

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Добавить участника' })).not.toBeInTheDocument();
  });
});
