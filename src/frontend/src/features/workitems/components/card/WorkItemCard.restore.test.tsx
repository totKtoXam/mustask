import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../../test/mocks/server';
import {
  PROCESS_URL,
  WORK_ITEM_RESTORE_URL,
  catalogText,
  deleteRecord,
  ids,
  recordOf,
  stateId,
  type Dataset,
} from '../../testFixtures';
import {
  dialogTitled,
  holdGet,
  holdPost,
  noDialog,
  openAs,
  postsTo,
  refuseWith,
} from './testDeletion';

// mod-005 US-4, design § 5 (the dialog), § 8 (what it meets), § 4 (the card it leaves). Each user
// action draws the kit's fields in jsdom; a test of several steps outlasts the default.

vi.setConfig({ testTimeout: 120_000 });

const STORY = 'MSK-US-2';
const HELD_HINT = 'Выбран статус, в котором рабочий элемент был перед удалением.';
const restoreDialog = () => dialogTitled('Восстановить рабочий элемент?');
const field = () => screen.getByRole('combobox', { name: 'Статус после восстановления' });
const primary = () =>
  within(screen.getByRole('dialog')).getByRole('button', { name: 'Восстановить' });
const title = () => screen.getByRole('textbox', { name: 'Заголовок', hidden: true });

/** MSK-US-2 deleted by Ann out of «Активна». */
function deletedFromActive(data: Dataset) {
  recordOf(data, 2).stateId = stateId('US', 'Активна');
  deleteRecord(recordOf(data, 2));
}

/** Opens the restore dialog of the story as a member. */
async function openDialog(prepare: (data: Dataset) => void = deletedFromActive) {
  const opened = await openAs(STORY, { role: 'member', prepare });
  await userEvent.click(screen.getByRole('button', { name: 'Восстановить' }));
  return { ...opened, dialog: await restoreDialog() };
}

/** The options the list offers, in order. */
async function optionsOfField(): Promise<string[]> {
  await userEvent.click(field());
  const options = within(await screen.findByRole('listbox'))
    .getAllByRole('option')
    .map((option) => option.textContent ?? '');
  await userEvent.keyboard('{Escape}');
  return options;
}

describe('mod-005 US-4 Scenario: «Восстановить» proposes the state the item held (design § 5)', () => {
  it('names the item, presets «Активна», offers the states of the type without «Удалена», and explains the preset', async () => {
    const { dialog } = await openDialog();

    expect(
      within(dialog).getByText('Рабочий элемент MSK-US-2 вернётся в списки, на доски и в бэклоги.'),
    ).toBeVisible();
    expect(field()).toHaveTextContent('Активна');
    expect(field()).toHaveAccessibleDescription(HELD_HINT);
    expect(await optionsOfField()).toEqual(['Новая', 'Активна', 'Разрешена', 'Закрыта']);
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toBeEnabled();
    expect(primary()).toBeEnabled();
  });

  it('a Задача is offered «Новая», «Активна» and «Закрыта» only', async () => {
    const { dialog } = await (async () => {
      const opened = await openAs('MSK-TASK-3', {
        role: 'member',
        prepare: (data) => deleteRecord(recordOf(data, 3)),
      });
      await userEvent.click(screen.getByRole('button', { name: 'Восстановить' }));
      return { ...opened, dialog: await restoreDialog() };
    })();

    expect(await optionsOfField()).toEqual(['Новая', 'Активна', 'Закрыта']);
    expect(dialog).toBeVisible();
  });

  it('«Отмена», Escape and «Закрыть» change nothing and send nothing', async () => {
    const { api, dialog } = await openDialog();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));
    await noDialog();
    await userEvent.click(screen.getByRole('button', { name: 'Восстановить' }));
    await userEvent.keyboard('{Escape}');
    await noDialog();
    await userEvent.click(screen.getByRole('button', { name: 'Восстановить' }));
    await userEvent.click(within(await restoreDialog()).getByRole('button', { name: 'Закрыть' }));
    await noDialog();

    expect(postsTo(api, 'restore')).toEqual([]);
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Удалена');
  });
});

describe('mod-005 US-4 Scenario: Accepting the proposal returns the item as it was', () => {
  it('sends the state it shows, tells the user, makes the card editable again and puts focus on the title', async () => {
    const { api, notify, onChanged } = await openDialog();

    await userEvent.click(primary());

    await noDialog();
    expect(postsTo(api, 'restore')).toEqual([
      expect.objectContaining({ body: { stateId: stateId('US', 'Активна') } }),
    ]);
    expect(notify).toHaveBeenCalledWith('Рабочий элемент MSK-US-2 восстановлен.');
    expect(onChanged).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled());
    expect(title()).not.toHaveAttribute('readonly');
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Активна');
    expect(screen.queryByRole('button', { name: 'Восстановить' })).not.toBeInTheDocument();
    expect(screen.queryByText('Рабочий элемент удалён', { exact: false })).not.toBeInTheDocument();
    expect(screen.queryByText('Удалил(а)', { exact: false })).not.toBeInTheDocument();
    await waitFor(() => expect(title()).toHaveFocus());
  });

  it('any other state of the type may be chosen: it is sent, the hint goes, and no reason is asked', async () => {
    const { api } = await openDialog();

    await userEvent.click(field());
    await userEvent.click(await screen.findByRole('option', { name: 'Новая' }));
    expect(field()).toHaveTextContent('Новая');
    expect(field()).not.toHaveAccessibleDescription(HELD_HINT);
    await userEvent.click(primary());

    await noDialog();
    expect(postsTo(api, 'restore')[0]?.body).toEqual({ stateId: stateId('US', 'Новая') });
    expect(recordOf(api.data, 2).stateId).toBe(stateId('US', 'Новая'));
    expect(recordOf(api.data, 2).deletion).toBeNull();
  });

  it('activating «Восстановить» twice sends one request', async () => {
    const { api } = await openDialog();
    const release = holdPost(WORK_ITEM_RESTORE_URL);

    await userEvent.dblClick(primary());
    release();

    await noDialog();
    expect(postsTo(api, 'restore')).toHaveLength(1);
  });

  it('is busy while the request runs: «Восстановить» of the header shows it, nothing in the dialog closes it', async () => {
    await openDialog();
    const release = holdPost(WORK_ITEM_RESTORE_URL);

    await userEvent.click(primary());

    expect(
      within(screen.getByRole('dialog')).getByRole('button', { name: 'Отмена' }),
    ).toBeDisabled();
    // The header's button is the first of the two; the dialog's is drawn after it.
    expect(
      screen.getAllByRole('button', { name: 'Восстановить', hidden: true })[0],
    ).toHaveAttribute('aria-busy', 'true');
    await userEvent.keyboard('{Escape}');
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    release();
    await noDialog();
  });
});

describe('mod-005 BR-DR-10: an item with no remembered state, a deleted parent', () => {
  it('proposes its type’s first state, «Новая», with no hint about a held one', async () => {
    const { api } = await openDialog((data) => deleteRecord(recordOf(data, 2), { marks: false }));

    expect(field()).toHaveTextContent('Новая');
    expect(field()).not.toHaveAccessibleDescription(HELD_HINT);
    await userEvent.click(primary());

    await noDialog();
    expect(postsTo(api, 'restore')[0]?.body).toEqual({ stateId: stateId('US', 'Новая') });
  });

  it('says that a deleted parent keeps the item unparented in the backlogs', async () => {
    const { dialog } = await openDialog((data) => {
      deletedFromActive(data);
      deleteRecord(recordOf(data, 6));
      recordOf(data, 2).parentId = recordOf(data, 6).id;
    });

    expect(
      within(dialog).getByText(
        'Родитель MSK-FEAT-6 удалён: пока он не восстановлен, в бэклогах рабочий элемент показывается без родителя.',
      ),
    ).toBeVisible();
  });

  it('says nothing of the parent when it is live', async () => {
    const { dialog } = await openDialog((data) => {
      deletedFromActive(data);
      recordOf(data, 2).parentId = recordOf(data, 6).id;
    });

    expect(within(dialog).queryByText('Родитель', { exact: false })).not.toBeInTheDocument();
  });
});

describe('mod-005 design § 5: while the process is read', () => {
  // The process is the card's own read too, asked when the card opens: its answer is held or
  // refused from the start, so the dialog meets it in that state. The handler is put in front of
  // the API's after the API is served.
  it('the field lists the preset alone and «Восстановить» works', async () => {
    let release = () => {};
    const { api } = await openDialog((data) => {
      deletedFromActive(data);
      release = holdGet(PROCESS_URL);
    });

    expect(field()).toHaveTextContent('Активна');
    expect(primary()).toBeEnabled();
    await userEvent.click(primary());
    release();

    await noDialog();
    expect(postsTo(api, 'restore')[0]?.body).toEqual({ stateId: stateId('US', 'Активна') });
  });

  it('with no remembered state the field is a skeleton and «Восстановить» waits for the read', async () => {
    let release = () => {};
    await openDialog((data) => {
      deleteRecord(recordOf(data, 2), { marks: false });
      release = holdGet(PROCESS_URL);
    });

    expect(
      within(screen.getByRole('dialog')).getByRole('status', { name: 'Загрузка данных' }),
    ).toBeVisible();
    expect(
      screen.queryByRole('combobox', { name: 'Статус после восстановления' }),
    ).not.toBeInTheDocument();
    expect(primary()).toBeDisabled();
    release();

    await waitFor(() => expect(primary()).toBeEnabled());
    expect(field()).toHaveTextContent('Новая');
  });

  it('a failed read of the process leaves the preset alone, and the item can still be restored', async () => {
    const { api } = await openDialog((data) => {
      deletedFromActive(data);
      server.use(http.get(PROCESS_URL, () => HttpResponse.json({}, { status: 500 })));
    });

    expect(field()).toHaveTextContent('Активна');
    await userEvent.click(primary());

    await noDialog();
    expect(postsTo(api, 'restore')).toHaveLength(1);
  });

  it('with no remembered state a failed read is an error with «Повторить», and the retry shows the field', async () => {
    let failing = true;
    await openDialog((data) => {
      deleteRecord(recordOf(data, 2), { marks: false });
      server.use(
        http.get(PROCESS_URL, () => (failing ? HttpResponse.json({}, { status: 500 }) : undefined)),
      );
    });

    const dialog = screen.getByRole('dialog');
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Ошибка API (HTTP 500).');
    expect(primary()).toBeDisabled();
    failing = false;
    await userEvent.click(within(dialog).getByRole('button', { name: 'Повторить' }));

    await waitFor(() => expect(field()).toHaveTextContent('Новая'));
    expect(primary()).toBeEnabled();
  });
});

describe('mod-005 design § 8: «Восстановить» refused or failed', () => {
  it('a state the server refuses stands under the field, and the dialog stays', async () => {
    const { notify } = await openDialog();
    refuseWith(WORK_ITEM_RESTORE_URL, 'workitems.workitem.state_not_allowed');

    await userEvent.click(primary());

    expect(
      await within(screen.getByRole('dialog')).findByText(
        catalogText('workitems.workitem.state_not_allowed'),
      ),
    ).toBeVisible();
    expect(field()).toBeInvalid();
    expect(notify).not.toHaveBeenCalled();
    // Choosing another state takes the error away.
    await userEvent.click(field());
    await userEvent.click(await screen.findByRole('option', { name: 'Закрыта' }));
    expect(field()).not.toBeInvalid();
  });

  it('no answer: the dialog stays with the failure above the body, the item still deleted', async () => {
    const { api } = await openDialog();
    server.use(http.post(WORK_ITEM_RESTORE_URL, () => HttpResponse.error()));

    await userEvent.click(primary());

    expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent(
      'Не удалось соединиться с API. Проверьте, что Mustask.Api запущен.',
    );
    expect(primary()).toBeEnabled();
    expect(recordOf(api.data, 2).deletion).not.toBeNull();
  });

  it('409 `…not_deleted` (another user restored it first): the dialog closes, an error notice says so and the card reads the item as live', async () => {
    const { api, notify } = await openDialog();
    // Ann restores it between the card's read and the confirmation.
    const record = recordOf(api.data, 2);
    record.stateId = stateId('US', 'Разрешена');
    record.deletion = null;

    await userEvent.click(primary());

    await noDialog();
    expect(notify).toHaveBeenCalledWith('Рабочий элемент не удалён.', 'error');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled());
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Разрешена');
    expect(recordOf(api.data, 2).stateId).toBe(stateId('US', 'Разрешена'));
  });

  it('404 `workitems.workitem.not_found` (purged meanwhile): the dialog closes and the card gives way to the item’s frame', async () => {
    const { onGone } = await openDialog();
    refuseWith(WORK_ITEM_RESTORE_URL, 'workitems.workitem.not_found');

    await userEvent.click(primary());

    await noDialog();
    expect(onGone).toHaveBeenCalledWith('workItem');
  });

  it('403 `organizations.access.forbidden`: «Недостаточно прав» in the dialog, which stays', async () => {
    await openDialog();
    refuseWith(WORK_ITEM_RESTORE_URL, 'organizations.access.forbidden');

    await userEvent.click(primary());

    expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent(
      'Недостаточно прав',
    );
  });

  it('a lost code disables the primary and leaves «Отмена»', async () => {
    const { serve, api } = await openDialog();

    await serve('viewer');

    await waitFor(() => expect(primary()).toBeDisabled());
    expect(
      within(screen.getByRole('dialog')).getByRole('button', { name: 'Отмена' }),
    ).toBeEnabled();
    expect(postsTo(api, 'restore')).toEqual([]);
    await userEvent.click(
      within(screen.getByRole('dialog')).getByRole('button', { name: 'Отмена' }),
    );
    await noDialog();
  });

  it('lets an administrator who is the deleter restore as well (the same code as a member)', async () => {
    const opened = await openAs(STORY, { role: 'admin', prepare: deletedFromActive });

    await userEvent.click(screen.getByRole('button', { name: 'Восстановить' }));
    await restoreDialog();
    await userEvent.click(primary());

    await noDialog();
    expect(postsTo(opened.api, 'restore')).toHaveLength(1);
    void ids;
  });
});
