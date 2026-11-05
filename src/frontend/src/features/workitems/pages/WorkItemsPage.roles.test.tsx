import { describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { servedFor, type TestRole } from '../../../test/organizationRoles';
import { organizationsKeys } from '../../organizations/api/queryKeys';
import { serveProblem, serveWorkItems } from '../testApi';
import { API, WORK_ITEMS_URL, emptyDataset, ids, problem } from '../testFixtures';
import { MSK_LIST, OPS_LIST, projectContext, renderWorkItems } from '../testRender';

// mod-016 design § 6.2, § 6.3 (plan-frontend § 3.3): «Новый рабочий элемент» needs
// `workitems.workitem.write`; the empty list keeps its heading and tells a reader who
// creates items, with no action.

const ACCESS_URL = `${API}/api/organizations/:organizationId/access`;
const HOLDER_HINT = 'Создайте первый рабочий элемент кнопкой «Новый рабочий элемент» выше.';
const READER_HINT =
  'Рабочие элементы создают участники организации с ролью «Участник» или «Администратор».';
const ROLE_CHANGED = 'Ваша роль в организации «Пример организации» изменилась: «Читатель».';

describe.each([
  ['admin', true],
  ['member', true],
  ['viewer', false],
] as const)(
  'mod-016 design § 6.2 Scenario: «Рабочие элементы» for the %s',
  (role: TestRole, writes) => {
    it(
      writes
        ? 'offers «Новый рабочий элемент» in the header'
        : 'reads the list and offers no «Новый рабочий элемент»',
      async () => {
        serveWorkItems();
        const shell = renderWorkItems(MSK_LIST, { role });

        expect(await screen.findByRole('table', { name: 'Рабочие элементы' })).toBeVisible();
        // The process is read after the list: the primary waits for it.
        await waitFor(() => expect(shell.queryClient.isFetching()).toBe(0));
        expect(screen.queryAllByRole('button', { name: 'Новый рабочий элемент' })).toHaveLength(
          writes ? 1 : 0,
        );
        expect(screen.getByRole('link', { name: 'Падение при входе' })).toBeVisible();
      },
    );

    it(
      writes
        ? 'an empty list keeps its hint and has the action twice, in the header and in the frame'
        : 'an empty list says who creates items, with no action',
      async () => {
        serveWorkItems(emptyDataset());
        const shell = renderWorkItems(OPS_LIST, {
          role,
          context: projectContext({ projectId: ids.ops }),
        });

        expect(
          await screen.findByRole('heading', { level: 2, name: 'Рабочих элементов пока нет' }),
        ).toBeVisible();
        await waitFor(() => expect(shell.queryClient.isFetching()).toBe(0));
        expect(screen.getByText(writes ? HOLDER_HINT : READER_HINT)).toBeVisible();
        expect(screen.queryByText(writes ? READER_HINT : HOLDER_HINT)).not.toBeInTheDocument();
        expect(screen.queryAllByRole('button', { name: 'Новый рабочий элемент' })).toHaveLength(
          writes ? 2 : 0,
        );
      },
    );
  },
);

describe('mod-016 design § 2.2, § 6.2 Scenario: the type picker goes with the code that opened it', () => {
  // The codes are read again while the menu stands (a refocus, a refused call):
  // its control is gone with `workitems.workitem.write`, and a type chosen from
  // the menu that stayed would open a create dialog that is already locked.
  it('closes the open menu when the codes turn to the reader’s, sends focus to the main region and opens no dialog', async () => {
    serveWorkItems();
    const shell = renderWorkItems(MSK_LIST, { role: 'member' });
    await screen.findByRole('table', { name: 'Рабочие элементы' });
    await userEvent.click(await screen.findByRole('button', { name: 'Новый рабочий элемент' }));
    expect(await screen.findByRole('menu', { name: 'Новый рабочий элемент' })).toBeVisible();

    const reader = servedFor('viewer');
    act(() => {
      shell.queryClient.setQueryData(organizationsKeys.access(ids.example), {
        kind: 'member',
        role: reader.role,
        permissions: reader.permissions,
      });
    });

    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).not.toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await waitFor(() => expect(document.getElementById('main-content')).toHaveFocus());

    // The code coming back does not bring the menu back with it.
    const member = servedFor('member');
    act(() => {
      shell.queryClient.setQueryData(organizationsKeys.access(ids.example), {
        kind: 'member',
        role: member.role,
        permissions: member.permissions,
      });
    });
    expect(await screen.findByRole('button', { name: 'Новый рабочий элемент' })).toBeVisible();
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });
});

describe('mod-016 design § 2.3 Scenario: a refused creation turns the member into a reader', () => {
  async function openCreateDialog() {
    serveWorkItems();
    const shell = renderWorkItems(MSK_LIST, { role: 'member' });
    await screen.findByRole('table', { name: 'Рабочие элементы' });
    await userEvent.click(await screen.findByRole('button', { name: 'Новый рабочий элемент' }));
    const menu = await screen.findByRole('menu', { name: 'Новый рабочий элемент' });
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Баг' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый рабочий элемент' });
    return { shell, dialog };
  }

  it('shows «Недостаточно прав» in the dialog with the title kept; once the codes are read again the primary is disabled, «Отмена» works, the role-change notice speaks and the page’s button is gone', async () => {
    const { dialog } = await openCreateDialog();
    serveProblem('post', WORK_ITEMS_URL, problem('organizations.access.forbidden'), {
      once: true,
    });
    await userEvent.type(
      within(dialog).getByRole('textbox', { name: 'Заголовок' }),
      'Новая ошибка',
    );
    // The codes the 403 makes the shell read again are the reader’s.
    server.use(http.get(ACCESS_URL, () => HttpResponse.json(servedFor('viewer'))));

    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await within(dialog).findByText('Недостаточно прав')).toBeVisible();
    expect(within(dialog).getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Новая ошибка');
    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Создать' })).toBeDisabled(),
    );
    expect(await screen.findByText(ROLE_CHANGED)).toBeInTheDocument();

    // Enter no longer submits; «Отмена» still works.
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Заголовок' }), '{Enter}');
    expect(within(dialog).getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Новая ошибка');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).not.toBeInTheDocument();
  });

  it('with the role unchanged the dialog keeps its alert, its title and a working primary, and no role-change notice is raised', async () => {
    const { dialog } = await openCreateDialog();
    serveProblem('post', WORK_ITEMS_URL, problem('organizations.access.forbidden'), {
      once: true,
    });
    await userEvent.type(
      within(dialog).getByRole('textbox', { name: 'Заголовок' }),
      'Новая ошибка',
    );

    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await within(dialog).findByText('Недостаточно прав')).toBeVisible();
    await waitFor(() =>
      expect(screen.queryByText(/Ваша роль в организации/)).not.toBeInTheDocument(),
    );
    expect(within(dialog).getByRole('button', { name: 'Создать' })).toBeEnabled();
    expect(within(dialog).getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Новая ошибка');
  });
});
