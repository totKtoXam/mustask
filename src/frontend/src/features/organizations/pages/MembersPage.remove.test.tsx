import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { organizationsKeys } from '../api/queryKeys';
import { ids } from '../testFixtures';
import { ANNA, people } from '../testMembersData';
import { chooseRowAction, membersTable, openPage, rowsOf } from '../testMembersPage';
import { contextSource } from '../testRender';

// mod-016 US-1 (remove), design § 3.7: the two confirmations, their refusals,
// the focus after a removal, and what removing oneself does.

async function openRemoval(name: string) {
  await membersTable();
  await chooseRowAction(name, 'Удалить из организации');
  return screen.findByRole('dialog');
}

describe('mod-016 design § 3.7: the confirmation', () => {
  it('names someone else, opens on «Отмена» and has the destructive primary', async () => {
    openPage();
    const dialog = await openRemoval('Иван Петров');

    expect(within(dialog).getByRole('heading', { name: 'Удалить участника?' })).toBeVisible();
    expect(
      within(dialog).getByText(
        'Пользователь Иван Петров потеряет доступ к организации «Альфа» и будет исключён из всех её команд. Назначенные ему рабочие элементы сохранят исполнителя.',
      ),
    ).toBeVisible();
    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus(),
    );
    expect(within(dialog).getByRole('button', { name: 'Удалить' })).toHaveClass(
      'MuiButton-colorError',
    );
  });

  it('names an inactive user without the mark', async () => {
    openPage();
    const dialog = await openRemoval('Олег Иванов');

    expect(
      within(dialog).getByText(
        'Пользователь Олег Иванов потеряет доступ к организации «Альфа» и будет исключён из всех её команд. Назначенные ему рабочие элементы сохранят исполнителя.',
      ),
    ).toBeVisible();
  });

  it('reads an unknown user without «Пользователь» before it', async () => {
    openPage();
    const dialog = await openRemoval('Неизвестный пользователь');

    expect(
      within(dialog).getByText(
        'Неизвестный пользователь потеряет доступ к организации «Альфа» и будет исключён из всех её команд. Назначенные ему рабочие элементы сохранят исполнителя.',
      ),
    ).toBeVisible();
  });

  it('asks the caller about themselves in the other title and the other text', async () => {
    openPage();
    const dialog = await openRemoval('Анна Смирнова');

    expect(
      within(dialog).getByRole('heading', { name: 'Удалить себя из организации?' }),
    ).toBeVisible();
    expect(within(dialog).getByText('Вы потеряете доступ к организации «Альфа».')).toBeVisible();
  });

  it.each([
    [
      '«Отмена»',
      (dialog: HTMLElement) =>
        userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' })),
    ],
    [
      '«Закрыть»',
      (dialog: HTMLElement) =>
        userEvent.click(within(dialog).getByRole('button', { name: 'Закрыть' })),
    ],
    ['Escape', () => userEvent.keyboard('{Escape}')],
  ])('%s changes nothing and sends nothing', async (_how, decline) => {
    const { members } = openPage();
    const dialog = await openRemoval('Иван Петров');

    await decline(dialog);

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(members.requests.filter((request) => request.method === 'DELETE')).toHaveLength(0);
    expect(rowsOf(await membersTable())).toHaveLength(4);
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Действия: Иван Петров' })).toHaveFocus(),
    );
  });
});

describe('mod-016 US-1 Scenario: removing someone else', () => {
  it('sends the removal, drops the row, notices without the mark and reads the team members again', async () => {
    const { members, queryClient } = openPage();
    queryClient.setQueryData(organizationsKeys.teamMembers(ids.alfa, ids.platformTeam), []);
    const dialog = await openRemoval('Олег Иванов');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    expect(
      await screen.findByText('Пользователь «Олег Иванов» удалён из организации.'),
    ).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(members.requests.find((request) => request.method === 'DELETE')?.path).toBe(
      `/api/organizations/${ids.alfa}/members/${people.oleg.id}`,
    );
    const table = await membersTable();
    await waitFor(() =>
      expect(rowsOf(table).map((cells) => cells[0])).toEqual([
        'Анна Смирнова',
        'Иван Петров',
        'Неизвестный пользователь',
      ]),
    );
    expect(
      queryClient.getQueryState(organizationsKeys.teamMembers(ids.alfa, ids.platformTeam))
        ?.isInvalidated,
    ).toBe(true);
  });

  it('moves focus to the next row’s «Действия»', async () => {
    openPage();
    const dialog = await openRemoval('Иван Петров');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Действия: Олег Иванов' })).toHaveFocus(),
    );
  });

  it('moves focus to the previous row’s «Действия» when the last row goes', async () => {
    openPage();
    const dialog = await openRemoval('Неизвестный пользователь');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Действия: Олег Иванов' })).toHaveFocus(),
    );
  });
});

describe('mod-016 design § 3.7, § 9: the refusals of a removal', () => {
  it('shows «В организации должен остаться хотя бы один администратор» in the alert when the last administrator is removed', async () => {
    const { data } = openPage();
    data.memberships!.find((membership) => membership.userId === people.ivan.id)!.role = 'member';
    const dialog = await openRemoval('Анна Смирнова');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'В организации должен остаться хотя бы один администратор',
    );
    expect(screen.getByRole('dialog', { name: 'Удалить себя из организации?' })).toBeVisible();
    expect(screen.queryByTestId('location')).toHaveTextContent(
      `/organizations/${ids.alfa}/settings/members`,
    );
  });

  it('shows «Участник не найден.» in the alert and reads the list again at once when the membership is already gone', async () => {
    const { data } = openPage();
    const dialog = await openRemoval('Олег Иванов');
    data.memberships = data.memberships!.filter(
      (membership) => membership.userId !== people.oleg.id,
    );

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Участник не найден.');
    const table = await membersTable();
    await waitFor(() =>
      expect(rowsOf(table).map((cells) => cells[0])).toEqual([
        'Анна Смирнова',
        'Иван Петров',
        'Неизвестный пользователь',
      ]),
    );
  });

  it('shows «Недостаточно прав» in the alert, with the dialog kept, and locks the primary once the code is gone', async () => {
    const { data, update } = openPage();
    const dialog = await openRemoval('Олег Иванов');
    data.memberships!.find((membership) => membership.userId === ANNA)!.role = 'member';

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Недостаточно прав');
    update({ role: 'member' });
    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Удалить' })).toBeDisabled(),
    );
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toBeEnabled();
  });
});

describe('mod-016 US-1 Scenario: removing oneself', () => {
  it('opens the organization list, forgets the organization’s access and notices «Вы больше не состоите…» when nothing replaces it', async () => {
    const { data, queryClient, members } = openPage({
      context: contextSource({
        organizations: [{ id: ids.alfa, name: 'Альфа', projects: [], teams: [] }],
        selection: { organizationId: ids.alfa },
      }),
    });
    // «Альфа» is the only organization the caller is in.
    data.memberships = data.memberships!.filter(
      (membership) => membership.organizationId === ids.alfa,
    );
    data.organizations = data.organizations.filter((organization) => organization.id === ids.alfa);
    queryClient.setQueryData(organizationsKeys.access(ids.alfa), {
      kind: 'member',
      role: 'admin',
      permissions: [],
    });
    const dialog = await openRemoval('Анна Смирнова');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    expect(
      await screen.findByText('Вы больше не состоите в организации «Альфа».'),
    ).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('location').textContent).toBe('/organizations'));
    expect(members.requests.find((request) => request.method === 'DELETE')?.path).toBe(
      `/api/organizations/${ids.alfa}/members/${ANNA}`,
    );
    // The members list was not read again: it would answer «Организация не найдена».
    expect(members.requests.filter((request) => request.method === 'GET')).toHaveLength(1);
    await waitFor(() =>
      expect(queryClient.getQueryState(organizationsKeys.access(ids.alfa))).toBeUndefined(),
    );
  });

  it('leaves the notice to the switcher’s fallback when another organization replaces it', async () => {
    openPage();
    const dialog = await openRemoval('Анна Смирнова');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    await waitFor(() => expect(screen.getByTestId('location').textContent).toBe('/organizations'));
    expect(screen.queryByText(/Вы больше не состоите/)).not.toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
