import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { servedFor } from '../../../test/organizationRoles';
import { serveWorkItems, type WorkItemsTestApi } from '../testApi';
import {
  API,
  CANDIDATES_URL,
  PROBLEM_HEADERS,
  exampleDataset,
  ids,
  problem,
  type Dataset,
} from '../testFixtures';
import { MSK_LIST, mskForm, renderWorkItems } from '../testRender';

// mod-016 US-5, design § 7 (plan-frontend § 3.6): the assignee picker keeps its
// behaviour under the membership rule. The candidates it reads are members only (the
// server); an assignee who left the organization stays as the field's value, by name
// and with no mark (the marks are feat-017's); «Не назначен» clears; a user who left
// between the search and the save is refused at the field and the draft is kept; a
// reader has no picker.

const ACCESS_URL = `${API}/api/organizations/:organizationId/access`;

async function openForm(data: Dataset = exampleDataset(), role: 'admin' | 'member' = 'admin') {
  const api = serveWorkItems(data);
  const shell = renderWorkItems(mskForm('MSK-BUG-4'), { role });
  await screen.findByRole('textbox', { name: 'Заголовок' });
  return { api, shell };
}

const patches = (api: WorkItemsTestApi) =>
  api.writes().filter((request) => request.method === 'PATCH');
const save = () => screen.getByRole('button', { name: 'Сохранить' });
const assignee = () => screen.getByRole('combobox', { name: 'Исполнитель' });
const title = () => screen.getByRole('textbox', { name: 'Заголовок' });

async function search(text: string) {
  await userEvent.click(assignee());
  await userEvent.clear(assignee());
  await userEvent.paste(text);
}

/** MSK-BUG-4's assignee, «Анна Смирнова», left the organization. */
function exMemberDataset(): Dataset {
  const data = exampleDataset();
  const anna = data.users.find((user) => user.id === ids.anna);
  if (anna) anna.member = false;
  return data;
}

describe('mod-016 US-5 Scenario: an assignee who left the organization stays', () => {
  it('reads their name as the field’s value with no mark, and saving another field sends no assignee', async () => {
    const { api } = await openForm(exMemberDataset());

    expect(assignee()).toHaveValue('Анна Смирнова');
    expect(screen.queryByText(/неактивен|не в организации/)).not.toBeInTheDocument();
    await userEvent.type(title(), '!');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({ title: 'Падение при входе!' });
    expect(assignee()).toHaveValue('Анна Смирнова');
    expect(api.record(ids.msk, 4).assigneeId).toBe(ids.anna);
  });

  it('is not offered by the search any more; «Не назначен» clears the field and is saved', async () => {
    const { api } = await openForm(exMemberDataset());

    await search('Смирнова');
    expect(await screen.findByRole('option', { name: 'Никого не найдено' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Анна Смирнова/ })).not.toBeInTheDocument();
    await userEvent.clear(assignee());
    await userEvent.click(await screen.findByRole('option', { name: 'Не назначен' }));
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({ assignee: { userId: null } });
    // The card shows an empty assignee as its placeholder text, not as a value.
    expect(assignee()).toHaveValue('');
    expect(screen.getByText('Не назначен')).toBeInTheDocument();
  });

  it('is offered again once they are members again', async () => {
    const data = exMemberDataset();
    await openForm(data);
    await search('Смирнова');
    expect(await screen.findByRole('option', { name: 'Никого не найдено' })).toBeInTheDocument();

    const anna = data.users.find((user) => user.id === ids.anna);
    if (anna) anna.member = true;
    // Another text: the first answer is kept for the text it was asked for.
    await search('smirnova@');

    expect(await screen.findByRole('option', { name: /Анна Смирнова/ })).toBeInTheDocument();
  });
});

// Retired by name: "mod-016 design § 7 Scenario: elsewhere an ex-member assignee reads like any
// other > the list’s «Исполнитель» column shows their name and no mark" — superseded by feat-003
// OQ-2 A and BR-WS-7 (the human's answer at gate-1, 2026-10-18): the list's column reads the
// marks, through mod-005's one helper, the same words as the filter's options. The card's field
// keeps its reading (the test above) until feat-017.
describe('feat-003 OQ-2 A, BR-WS-7 Scenario: in the list an ex-member assignee reads with the mark', () => {
  it('the list’s «Исполнитель» column shows their name and «(не в организации)»', async () => {
    serveWorkItems(exMemberDataset());
    renderWorkItems(MSK_LIST);

    const table = await screen.findByRole('table', { name: 'Рабочие элементы' });

    const row = within(table)
      .getAllByRole('row')
      .find((candidate) => candidate.textContent?.includes('MSK-BUG-4'));
    expect(row).toHaveTextContent('АСАнна Смирнова (не в организации)');
    expect(row).not.toHaveTextContent('неактивен');
  });
});

describe('mod-016 US-5 Scenario: a user who left between the search and the save', () => {
  it('puts «Этого пользователя нельзя назначить исполнителем.» under «Исполнитель», keeps the draft and assigns nobody', async () => {
    const { api } = await openForm();
    await search('Кузнецова');
    await userEvent.click(await screen.findByRole('option', { name: /Анна Кузнецова/ }));
    await userEvent.type(title(), ' (срочно)');
    // She leaves the organization before the save lands.
    const user = api.data.users.find((candidate) => candidate.id === ids.annaK);
    if (user) user.member = false;

    await userEvent.click(save());

    expect(
      await screen.findByText('Этого пользователя нельзя назначить исполнителем.'),
    ).toBeInTheDocument();
    expect(assignee()).toHaveAttribute('aria-invalid', 'true');
    expect(assignee()).toHaveValue('Анна Кузнецова');
    expect(title()).toHaveValue('Падение при входе (срочно)');
    expect(api.record(ids.msk, 4).assigneeId).toBe(ids.anna);
    expect(save()).toBeEnabled();
  });
});

describe('mod-016 US-5 Scenario: a member whose role has just turned to reader', () => {
  it('meets the search’s failed line, then the card turns read-only and shows the assignee as its value', async () => {
    const { shell } = await openForm(exampleDataset(), 'member');
    // The search is refused for the right; the codes are then read again — held until
    // the failed line has been seen.
    const held = gate();
    server.use(
      http.get(CANDIDATES_URL, () =>
        HttpResponse.json(problem('organizations.access.forbidden'), {
          status: 403,
          headers: PROBLEM_HEADERS,
        }),
      ),
      http.get(ACCESS_URL, async () => {
        await held.opened;
        return HttpResponse.json(servedFor('viewer'));
      }),
    );

    await search('Кузнецова');

    expect(
      await screen.findByRole('option', {
        name: 'Не удалось выполнить поиск. Попробуйте ещё раз.',
      }),
    ).toHaveAttribute('aria-disabled', 'true');
    held.open();

    await waitFor(() => expect(title()).toHaveAttribute('readonly'));
    expect(screen.queryByRole('button', { name: 'Сохранить' })).not.toBeInTheDocument();
    // No picker is left to search with: the field is read-only and reads the assignee,
    // not the text the failed search was typed with.
    await waitFor(() => expect(assignee()).toHaveAttribute('aria-readonly', 'true'));
    expect(assignee()).toHaveValue('Анна Смирнова');
    await waitFor(() => expect(shell.queryClient.isFetching()).toBe(0));
  });
});
