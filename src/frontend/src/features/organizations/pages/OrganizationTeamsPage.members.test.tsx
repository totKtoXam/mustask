import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { ORGANIZATIONS_URL, PROBLEM_HEADERS, ids, problem } from '../testFixtures';
import { membersDataset, people } from '../testMembersData';
import { listedLines, openPage } from '../testMembersPage';

// feat-028 design § 5.1, api-contract § 2.3a: the creation form's «Участники» asks the server's
// search of the organization's active members — same states, texts and cut-off as the team
// page's candidate search (mod-016 design § 3.5) — and sends the chosen in `memberIds`.

const ALFA_TEAMS = `/organizations/${ids.alfa}/settings/teams`;
const SEARCH_PATH = `/api/organizations/${ids.alfa}/teams/member-candidates`;
const SEARCH_URL = `${ORGANIZATIONS_URL}/:organizationId/teams/member-candidates`;
const NO_SEARCH_YET = 'Введите не менее 2 символов имени или почты';

type Opened = ReturnType<typeof openPage>;

async function openCreate(opened: Opened = openPage({ path: ALFA_TEAMS })) {
  await screen.findByRole('table', { name: 'Команды организации' });
  await userEvent.click(screen.getByRole('button', { name: 'Новая команда' }));
  const dialog = await screen.findByRole('dialog', { name: 'Новая команда' });
  return { ...opened, dialog };
}

/** The searches the creation form asked, as the text each one carried. */
function asked(opened: Opened): (string | null)[] {
  return opened.members.requests
    .filter((request) => request.path.startsWith(SEARCH_PATH))
    .map((request) => new URL(`http://x${request.path}`).searchParams.get('text'));
}

async function typeMembers(dialog: HTMLElement, text: string) {
  const field = within(dialog).getByRole('combobox', { name: 'Участники' });
  await userEvent.click(field);
  await userEvent.type(field, text);
  return field;
}

describe('feat-028 § 2.3a: the creation form’s «Участники» is the server’s search', () => {
  it('asks the organization’s search with the trimmed text once typing has settled, and never reads the member list', async () => {
    const opened = await openCreate();

    await typeMembers(opened.dialog, ' Ива ');
    const option = await screen.findByRole('option', { name: /Иван Петров/ });

    expect(option).toHaveTextContent('ivan@example.org');
    expect(asked(opened)).toEqual(['Ива']);
    expect(
      opened.members.requests.some(
        (request) => request.path === `/api/organizations/${ids.alfa}/members`,
      ),
    ).toBe(false);
  });

  it('under two trimmed characters shows the hint and makes no request', async () => {
    const opened = await openCreate();

    const field = await typeMembers(opened.dialog, '  И ');
    await new Promise((resolve) => setTimeout(resolve, 400));

    expect(listedLines()).toEqual([NO_SEARCH_YET]);
    expect(screen.getByRole('option', { name: NO_SEARCH_YET })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    expect(asked(opened)).toEqual([]);

    await userEvent.clear(field);
    await new Promise((resolve) => setTimeout(resolve, 400));
    expect(asked(opened)).toEqual([]);
  });

  it('says «Поиск…» while the settled text is asked', async () => {
    const opened = await openCreate();

    await typeMembers(opened.dialog, 'Ива');

    expect(listedLines()).toEqual(['Поиск…']);
    await screen.findByRole('option', { name: /Иван Петров/ });
  });

  it('says «Пользователи не найдены» when nobody matches, and offers no inactive member and no user of no organization', async () => {
    const opened = await openCreate();

    const field = await typeMembers(opened.dialog, 'Ол');
    expect(await screen.findByRole('option', { name: 'Пользователи не найдены' })).toBeVisible();
    await userEvent.clear(field);
    await userEvent.type(field, 'Мар');

    expect(await screen.findByRole('option', { name: 'Пользователи не найдены' })).toBeVisible();
    expect(asked(opened)).toEqual(['Ол', 'Мар']);
  });

  it('cuts at the first 20 with «Показаны первые 20. Уточните запрос.» last (moreMatched)', async () => {
    const data = withManyMembers();
    const opened = await openCreate(openPage({ path: ALFA_TEAMS, data }));

    await typeMembers(opened.dialog, 'Тест');

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

  it('says «Не удалось выполнить поиск. Попробуйте ещё раз.» when the search fails, and opening the list again retries', async () => {
    const opened = openPage({ path: ALFA_TEAMS });
    let failures = 1;
    server.use(
      http.get(SEARCH_URL, () => {
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
    const { dialog } = await openCreate(opened);

    const field = await typeMembers(dialog, 'Ива');
    expect(
      await screen.findByRole('option', {
        name: 'Не удалось выполнить поиск. Попробуйте ещё раз.',
      }),
    ).toBeVisible();

    // The first Escape closes the open list, not the dialog; opening it again retries.
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
    expect(screen.getByRole('dialog', { name: 'Новая команда' })).toBeVisible();
    await userEvent.click(field);
    await userEvent.keyboard('{ArrowDown}');

    expect(await screen.findByRole('option', { name: /Иван Петров/ })).toBeVisible();
    expect(failures).toBe(0);
  });

  it('sends every chosen member in memberIds, the ones chosen from different searches together', async () => {
    const opened = await openCreate();
    await userEvent.type(within(opened.dialog).getByRole('textbox', { name: 'Название' }), 'MARS');

    const field = await typeMembers(opened.dialog, 'Ива');
    await userEvent.click(await screen.findByRole('option', { name: /Иван Петров/ }));
    await userEvent.type(field, 'Анн');
    await userEvent.click(await screen.findByRole('option', { name: /Анна Смирнова/ }));
    await userEvent.keyboard('{Escape}');

    expect(
      within(opened.dialog).getByRole('button', { name: 'Убрать «Иван Петров»' }),
    ).toBeVisible();
    expect(
      within(opened.dialog).getByRole('button', { name: 'Убрать «Анна Смирнова»' }),
    ).toBeVisible();
    await userEvent.click(within(opened.dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Команда «MARS» создана.')).toBeInTheDocument();
    expect(opened.organizations.writes()[0]?.body).toEqual({
      name: 'MARS',
      memberIds: [people.ivan.id, people.anna.id],
    });
  });
});

/** «Альфа» with 25 more active members whose names begin with «Тест». */
function withManyMembers() {
  const data = membersDataset();
  for (let number = 1; number <= 25; number += 1) {
    const label = String(number).padStart(2, '0');
    const id = `user-test-${label}`;
    data.users!.push({
      id,
      displayName: `Тест ${label}`,
      email: `test${label}@example.org`,
      state: 'active',
    });
    data.memberships!.push({
      organizationId: ids.alfa,
      userId: id,
      role: 'member',
      addedBy: people.anna.id,
      addedAt: '2026-10-04T09:00:00+00:00',
    });
  }
  return data;
}
