import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { formatDateTime } from '../../../shared/lib/format';
import { server } from '../../../test/mocks/server';
import { ORGANIZATIONS_URL, PROBLEM_HEADERS, ids, problem } from '../testFixtures';
import { ANNA, people } from '../testMembersData';
import { chooseRole, chooseRowAction, membersTable, openPage, rowsOf } from '../testMembersPage';

// mod-016 US-1 (change a role), design § 3.6: «Изменение роли» — who, «Роль»
// preselected and focused with its helper, the warning of stepping down, and
// the refusals.

async function openRoleDialog(name: string) {
  await membersTable();
  await chooseRowAction(name, 'Изменить роль');
  return screen.findByRole('dialog', { name: 'Изменение роли' });
}

describe('mod-016 design § 3.6: the dialog', () => {
  it('opens on «Роль» with focus, preselected with the current role and the line «Пользователь: <имя>»; «Сохранить» is disabled until the role differs', async () => {
    openPage();
    const dialog = await openRoleDialog('Иван Петров');

    const role = within(dialog).getByRole('combobox', { name: 'Роль' });
    await waitFor(() => expect(role).toHaveFocus());
    expect(role).toHaveTextContent('Администратор');
    expect(within(dialog).getByText(/^Пользователь:/)).toHaveTextContent(
      'Пользователь: Иван Петров',
    );
    expect(
      within(dialog).getByText(
        'Управляет организацией, участниками, проектами, командами и областями, работает с рабочими элементами.',
      ),
    ).toBeVisible();
    const save = within(dialog).getByRole('button', { name: 'Сохранить' });
    expect(save).toBeDisabled();

    await chooseRole(dialog, 'Читатель');
    expect(save).toBeEnabled();
    expect(within(dialog).getByText('Только просматривает, ничего не меняет.')).toBeVisible();

    await chooseRole(dialog, 'Администратор');
    expect(save).toBeDisabled();
  });

  it('names an inactive user with the mark in the line and an unknown user as such', async () => {
    openPage();
    const oleg = await openRoleDialog('Олег Иванов');
    expect(within(oleg).getByText(/^Пользователь:/)).toHaveTextContent(
      'Пользователь: Олег Ивановнеактивен',
    );
    await userEvent.click(within(oleg).getByRole('button', { name: 'Отмена' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    const ghost = await openRoleDialog('Неизвестный пользователь');
    expect(within(ghost).getByText(/^Пользователь:/)).toHaveTextContent(
      'Пользователь: Неизвестный пользователь',
    );
  });

  it('warns about stepping down only on the caller’s own administrator row, and only for another role', async () => {
    openPage();
    const own = await openRoleDialog('Анна Смирнова');
    const warning =
      'Вы потеряете права администратора. Вернуть их сможет только другой администратор.';

    expect(within(own).queryByText(warning)).not.toBeInTheDocument();
    await chooseRole(own, 'Участник');
    expect(within(own).getByText(warning)).toBeVisible();
    await chooseRole(own, 'Администратор');
    expect(within(own).queryByText(warning)).not.toBeInTheDocument();
    await userEvent.click(within(own).getByRole('button', { name: 'Отмена' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    const other = await openRoleDialog('Иван Петров');
    await chooseRole(other, 'Участник');
    expect(within(other).queryByText(warning)).not.toBeInTheDocument();
  });

  it('closes on Escape, sending nothing', async () => {
    const { members } = openPage();
    await openRoleDialog('Олег Иванов');

    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(members.requests.filter((request) => request.method === 'PUT')).toHaveLength(0);
  });
});

describe('mod-016 US-1 Scenario: changing a role', () => {
  it('sends the role, notices «Роль пользователя «<имя>» изменена.» without the mark, and shows the new role with who changed it and when', async () => {
    const { members } = openPage({ clock: () => '2026-10-12T09:30:00+00:00' });
    const dialog = await openRoleDialog('Олег Иванов');
    await chooseRole(dialog, 'Читатель');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(
      await screen.findByText('Роль пользователя «Олег Иванов» изменена.'),
    ).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const put = members.requests.find((request) => request.method === 'PUT')!;
    expect(put.path).toBe(`/api/organizations/${ids.alfa}/members/${people.oleg.id}/role`);
    expect(put.body).toEqual({ role: 'viewer' });
    const table = await membersTable();
    await waitFor(() => expect(rowsOf(table)[2]![2]).toBe('Читатель'));
    expect(rowsOf(table)[2]![4]).toBe(
      `Анна Смирнова, ${formatDateTime('2026-10-12T09:30:00+00:00')}`,
    );
  });

  it('gives focus back to the row’s «Действия»', async () => {
    openPage();
    const dialog = await openRoleDialog('Олег Иванов');
    await chooseRole(dialog, 'Читатель');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Действия: Олег Иванов' })).toHaveFocus(),
    );
  });
});

describe('mod-016 design § 3.6, § 9: the refusals of a role change', () => {
  it('puts «В организации должен остаться хотя бы один администратор» on «Роль» when the only known active administrator steps down', async () => {
    const { data } = openPage();
    // Иван is no longer an administrator: Анна is the last one.
    data.memberships!.find((membership) => membership.userId === people.ivan.id)!.role = 'member';
    const dialog = await openRoleDialog('Анна Смирнова');
    await chooseRole(dialog, 'Участник');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    const role = within(dialog).getByRole('combobox', { name: 'Роль' });
    expect(
      await within(dialog).findByText('В организации должен остаться хотя бы один администратор'),
    ).toBeVisible();
    expect(role).toHaveTextContent('Участник');
    expect(screen.getByRole('dialog', { name: 'Изменение роли' })).toBeVisible();
    // The refusal is about the field; the dialog’s error alert stays unused
    // (the warning of stepping down is another alert, of the warning kind).
    expect(within(dialog).queryByText('Участник не найден.')).not.toBeInTheDocument();
  });

  it('does not count an administrator the projection does not know', async () => {
    const { data } = openPage();
    // Иван is an administrator nobody knows: he does not count (BR-M1-32).
    data.users = data.users!.filter((user) => user.id !== people.ivan.id);
    const dialog = await openRoleDialog('Анна Смирнова');
    await chooseRole(dialog, 'Читатель');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(
      await within(dialog).findByText('В организации должен остаться хотя бы один администратор'),
    ).toBeVisible();
  });

  it('shows «Участник не найден.» in the dialog’s alert and reads the list again at once when the membership is already gone', async () => {
    const { data } = openPage();
    const dialog = await openRoleDialog('Олег Иванов');
    await chooseRole(dialog, 'Читатель');
    data.memberships = data.memberships!.filter(
      (membership) => membership.userId !== people.oleg.id,
    );

    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Участник не найден.');
    const table = await membersTable();
    await waitFor(() =>
      expect(rowsOf(table).map((cells) => cells[0])).toEqual([
        'Анна Смирнова',
        'Иван Петров',
        'Неизвестный пользователь',
      ]),
    );
    expect(screen.getByRole('dialog', { name: 'Изменение роли' })).toBeVisible();
  });

  it('shows «Недостаточно прав» in the alert with the chosen role kept, then locks «Сохранить» once the code is gone', async () => {
    const { data, update } = openPage();
    const dialog = await openRoleDialog('Олег Иванов');
    await chooseRole(dialog, 'Читатель');
    data.memberships!.find((membership) => membership.userId === ANNA)!.role = 'viewer';

    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Недостаточно прав');
    expect(within(dialog).getByRole('combobox', { name: 'Роль' })).toHaveTextContent('Читатель');

    update({ role: 'viewer' });
    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Сохранить' })).toBeDisabled(),
    );
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('shows a refusal the server words differently by its code, not its text', async () => {
    openPage();
    server.use(
      http.put(`${ORGANIZATIONS_URL}/:organizationId/members/:userId/role`, () =>
        HttpResponse.json(
          { ...problem('organizations.membership.last_admin'), detail: 'Другой текст' },
          { status: 409, headers: PROBLEM_HEADERS },
        ),
      ),
    );
    const dialog = await openRoleDialog('Олег Иванов');
    await chooseRole(dialog, 'Читатель');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await within(dialog).findByText('Другой текст')).toBeVisible();
    expect(within(dialog).getByRole('combobox', { name: 'Роль' })).toHaveAttribute(
      'aria-invalid',
      'true',
    );
  });
});
