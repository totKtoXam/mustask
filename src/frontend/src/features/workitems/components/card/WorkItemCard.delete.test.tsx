import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../../test/mocks/server';
import {
  DELETED_AT_TEXT,
  WORK_ITEM_DELETE_URL,
  deleteRecord,
  recordOf,
  stateId,
  type Dataset,
} from '../../testFixtures';
import { typeInto } from './testCard';
import {
  chooseFromMenu,
  dialogTitled,
  holdPost,
  menuEntries,
  moreActions,
  noDialog,
  openAs,
  postsTo,
  refuseWith,
} from './testDeletion';

// mod-005 US-1, design § 2 (the menu), § 3 (the dialog), § 4 (the card it leaves), § 8 (what it
// meets), § 9 (roles). Each user action draws the kit's fields in jsdom; a test of several steps
// outlasts the default.

vi.setConfig({ testTimeout: 120_000 });

const STORY = 'MSK-US-2';
// A dialog over the card hides it from the accessibility tree, and the card is still there.
const title = () => screen.getByRole('textbox', { name: 'Заголовок', hidden: true });
const restoreButton = () => screen.getByRole('button', { name: 'Восстановить' });
const deleteDialog = () => dialogTitled('Удалить рабочий элемент?');
const confirmDelete = async () =>
  userEvent.click(within(await deleteDialog()).getByRole('button', { name: 'Удалить' }));

/** MSK-US-2 «Активна» with a live child, MSK-TASK-3: US-1's first scenario. */
function activeWithChild(data: Dataset) {
  recordOf(data, 2).stateId = stateId('US', 'Активна');
  recordOf(data, 3).parentId = recordOf(data, 2).id;
}

describe('mod-005 design § 2: the «⋮» menu of a live card, per role', () => {
  it('a member is offered «Копировать ссылку», a rule and «Удалить»', async () => {
    await openAs(STORY, { role: 'member' });

    const { names, rules } = await menuEntries();

    expect(names).toEqual(['Копировать ссылку', 'Удалить']);
    expect(rules).toBe(1);
  });

  it('the administrator is offered the same: «Удалить навсегда» belongs to a deleted card', async () => {
    await openAs(STORY, { role: 'admin' });

    expect((await menuEntries()).names).toEqual(['Копировать ссылку', 'Удалить']);
  });

  it('a reader is offered «Копировать ссылку» alone, with no rule drawn', async () => {
    await openAs(STORY, { role: 'viewer' });

    const { names, rules } = await menuEntries();

    expect(names).toEqual(['Копировать ссылку']);
    expect(rules).toBe(0);
  });

  it('«Удалить» reads like the other entry: the action is reversible (D-4)', async () => {
    await openAs(STORY, { role: 'member' });
    await userEvent.click(moreActions());

    const copy = await screen.findByRole('menuitem', { name: 'Копировать ссылку' });
    const remove = screen.getByRole('menuitem', { name: 'Удалить' });

    expect(getComputedStyle(remove).color).toBe(getComputedStyle(copy).color);
  });
});

describe('mod-005 US-1 Scenario: «Удалить» asks first (design § 3)', () => {
  it('names the identifier and the live child, offers «Отмена» and «Удалить», and Cancel sends nothing', async () => {
    const { api } = await openAs(STORY, { role: 'member', prepare: activeWithChild });

    await chooseFromMenu('Удалить');

    const dialog = await deleteDialog();
    expect(
      within(dialog).getByText(
        'Рабочий элемент MSK-US-2 получит статус «Удалена». Его можно будет восстановить. Дочерние элементы (1) не удаляются: пока он удалён, в бэклогах они показываются без родителя.',
      ),
    ).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toBeEnabled();
    expect(within(dialog).getByRole('button', { name: 'Удалить' })).toBeEnabled();
    // Focus opens on the dialog's frame, so Enter confirms nothing until a control is focused.
    expect(document.activeElement?.tagName).not.toBe('BUTTON');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));
    await noDialog();
    expect(postsTo(api, 'delete')).toEqual([]);
    expect(screen.queryByRole('button', { name: 'Восстановить' })).not.toBeInTheDocument();
  });

  it('a leaf has no sentence about children', async () => {
    await openAs('MSK-BUG-4', { role: 'member' });

    await chooseFromMenu('Удалить');

    expect(
      await screen.findByText(
        'Рабочий элемент MSK-BUG-4 получит статус «Удалена». Его можно будет восстановить.',
      ),
    ).toBeVisible();
  });

  it('Escape and «Закрыть» change nothing, and focus goes back to «Ещё действия»', async () => {
    const { api } = await openAs(STORY, { role: 'member' });

    await chooseFromMenu('Удалить');
    await deleteDialog();
    await userEvent.keyboard('{Escape}');
    await noDialog();
    expect(moreActions()).toHaveFocus();

    await chooseFromMenu('Удалить');
    await userEvent.click(within(await deleteDialog()).getByRole('button', { name: 'Закрыть' }));
    await noDialog();
    expect(postsTo(api, 'delete')).toEqual([]);
    expect(moreActions()).toHaveFocus();
  });

  it('with an unsaved draft the text ends «Несохранённые изменения будут потеряны.»; Cancel keeps the draft', async () => {
    await openAs(STORY, { role: 'member' });
    typeInto(title(), '!');

    await chooseFromMenu('Удалить');

    const dialog = await deleteDialog();
    expect(
      within(dialog).getByText(
        'Рабочий элемент MSK-US-2 получит статус «Удалена». Его можно будет восстановить. Несохранённые изменения будут потеряны.',
      ),
    ).toBeVisible();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));
    await noDialog();
    expect(title()).toHaveValue('Письмо со ссылкой!');
  });
});

describe('mod-005 US-1 Scenario: Confirming moves the item into «Удалена» and the card stays, read-only', () => {
  it('sends one delete, tells the user, drops the draft, and turns the card to its deleted look', async () => {
    const { api, notify, onChanged } = await openAs(STORY, {
      role: 'member',
      prepare: activeWithChild,
    });
    typeInto(title(), '!');

    await chooseFromMenu('Удалить');
    await confirmDelete();

    await noDialog();
    expect(postsTo(api, 'delete')).toHaveLength(1);
    expect(notify).toHaveBeenCalledWith('Рабочий элемент MSK-US-2 удалён.');
    expect(onChanged).toHaveBeenCalledTimes(1);
    // The draft is not saved: the stored title is shown, and no Save group or undo is drawn.
    expect(title()).toHaveValue('Письмо со ссылкой');
    expect(screen.queryByRole('button', { name: 'Сохранить' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Отменить все изменения' }),
    ).not.toBeInTheDocument();
    expect(restoreButton()).toBeEnabled();
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Удалена');
    await waitFor(() => expect(moreActions()).toHaveFocus());
    // The deleter is the signed-in user, the time is the deletion's, and the item remembers its state.
    expect(
      await screen.findByText(/^Удалил\(а\): Анна Смирнова, \d{2}\.\d{2}\.\d{4} \d{2}:\d{2}$/),
    ).toBeVisible();
    expect(recordOf(api.data, 2).deletion?.fromStateId).toBe(stateId('US', 'Активна'));
  });

  it('activating «Удалить» twice sends one request, and no refusal of a second one is shown', async () => {
    const { api } = await openAs(STORY, { role: 'member' });
    const release = holdPost(WORK_ITEM_DELETE_URL);

    await chooseFromMenu('Удалить');
    await userEvent.dblClick(within(await deleteDialog()).getByRole('button', { name: 'Удалить' }));
    release();

    await noDialog();
    expect(postsTo(api, 'delete')).toHaveLength(1);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('is busy while the request runs: nothing in the dialog closes it', async () => {
    await openAs(STORY, { role: 'member' });
    const release = holdPost(WORK_ITEM_DELETE_URL);

    await chooseFromMenu('Удалить');
    const dialog = await deleteDialog();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toBeDisabled();
    expect(within(dialog).getByRole('button', { name: 'Закрыть' })).toBeDisabled();
    await userEvent.keyboard('{Escape}');
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    release();
    await noDialog();
  });
});

describe('mod-005 design § 8: «Удалить» refused or failed', () => {
  it('no answer: the dialog stays with the failure above the body, the item is editable and unchanged', async () => {
    const { api, notify } = await openAs(STORY, { role: 'member' });
    server.use(http.post(WORK_ITEM_DELETE_URL, () => HttpResponse.error()));
    await chooseFromMenu('Удалить');
    const dialog = await deleteDialog();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Не удалось соединиться с API. Проверьте, что Mustask.Api запущен.',
    );
    expect(title()).not.toHaveAttribute('readonly');
    expect(within(dialog).getByRole('button', { name: 'Удалить' })).toBeEnabled();
    expect(notify).not.toHaveBeenCalled();
    expect(recordOf(api.data, 2).deletion ?? null).toBeNull();
  });

  it('a server error stays too, and the primary tries again', async () => {
    const { api } = await openAs(STORY, { role: 'member' });
    const failed = vi.fn();
    server.use(
      http.post(
        WORK_ITEM_DELETE_URL,
        () => {
          failed();
          return HttpResponse.json({}, { status: 500 });
        },
        { once: true },
      ),
    );
    await chooseFromMenu('Удалить');
    const dialog = await deleteDialog();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Ошибка API (HTTP 500).');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    await noDialog();
    // The first request was the server error above, the second reached the API and was accepted.
    expect(failed).toHaveBeenCalledTimes(1);
    expect(postsTo(api, 'delete')).toHaveLength(1);
    expect(restoreButton()).toBeInTheDocument();
  });

  it('409 `…deleted` (another user was first): the dialog closes, an error notice says the server’s text, and the card reads the item as deleted', async () => {
    const { api, notify, onChanged } = await openAs(STORY, { role: 'member' });
    await chooseFromMenu('Удалить');
    const dialog = await deleteDialog();
    // Ann deletes it between the card's read and the confirmation.
    deleteRecord(recordOf(api.data, 2));

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    await noDialog();
    expect(notify).toHaveBeenCalledWith(
      'Рабочий элемент удалён и доступен только для чтения.',
      'error',
    );
    expect(await screen.findByRole('button', { name: 'Восстановить' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Сохранить' })).not.toBeInTheDocument();
    // The first deletion's marks, not this request's; and the host reads its views again.
    expect(await screen.findByText(new RegExp(`Анна Смирнова, ${DELETED_AT_TEXT}$`))).toBeVisible();
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('404 `workitems.workitem.not_found` (purged meanwhile): the dialog closes and the card gives way to the item’s frame', async () => {
    const { onGone } = await openAs(STORY, { role: 'member' });
    refuseWith(WORK_ITEM_DELETE_URL, 'workitems.workitem.not_found');
    await chooseFromMenu('Удалить');

    await confirmDelete();

    await noDialog();
    expect(onGone).toHaveBeenCalledWith('workItem');
  });

  it.each([
    ['organizations.project.not_found', 'project'],
    ['organizations.organization.not_found', 'organization'],
  ])('404 `%s`: the frame of that level', async (code, level) => {
    const { onGone } = await openAs(STORY, { role: 'member' });
    refuseWith(WORK_ITEM_DELETE_URL, code);
    await chooseFromMenu('Удалить');

    await confirmDelete();

    await noDialog();
    expect(onGone).toHaveBeenCalledWith(level);
  });

  it('403 `organizations.access.forbidden`: «Недостаточно прав» in the dialog, which stays', async () => {
    const { notify } = await openAs(STORY, { role: 'member' });
    refuseWith(WORK_ITEM_DELETE_URL, 'organizations.access.forbidden');
    await chooseFromMenu('Удалить');
    const dialog = await deleteDialog();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Недостаточно прав');
    expect(notify).not.toHaveBeenCalled();
  });
});
