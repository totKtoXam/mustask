import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../../test/mocks/server';
import {
  WORK_ITEM_PURGE_URL,
  deleteRecord,
  recordOf,
  stateId,
  type Dataset,
} from '../../testFixtures';
import {
  chooseFromMenu,
  dialogTitled,
  holdPost,
  moreActions,
  noDialog,
  openAs,
  postsTo,
  refuseWith,
} from './testDeletion';

// mod-005 US-6, design § 6 (the dialog), § 8 (what it meets). Each user action draws the kit's
// fields in jsdom; a test of several steps outlasts the default.

vi.setConfig({ testTimeout: 120_000 });

const BUG = 'MSK-BUG-4';
const EPIC = 'MSK-EPIC-1';
const purgeDialog = () => dialogTitled('Удалить рабочий элемент навсегда?');
const confirm = () =>
  within(screen.getByRole('dialog')).getByRole('button', { name: 'Удалить навсегда' });
const cancel = () => within(screen.getByRole('dialog')).getByRole('button', { name: 'Отмена' });

/** MSK-BUG-4 «Ссылка истекает раньше срока» deleted, a leaf. */
function deletedBug(data: Dataset) {
  deleteRecord(recordOf(data, 4));
}

/** MSK-EPIC-1 deleted with two live children, MSK-FEAT-6 and MSK-FEAT-8. */
function deletedEpic(data: Dataset) {
  recordOf(data, 6).parentId = recordOf(data, 1).id;
  recordOf(data, 8).parentId = recordOf(data, 1).id;
  deleteRecord(recordOf(data, 1));
}

async function openPurge(identifier: string, prepare: (data: Dataset) => void) {
  const opened = await openAs(identifier, { role: 'admin', prepare });
  await chooseFromMenu('Удалить навсегда');
  return { ...opened, dialog: await purgeDialog() };
}

describe('mod-005 US-6 Scenario: The confirmation says that it cannot be undone (design § 6)', () => {
  it('names the identifier and the title; «Отмена» holds the focus; the primary is in the error colour', async () => {
    const { dialog } = await openPurge(BUG, deletedBug);

    expect(
      within(dialog).getByText(
        'Рабочий элемент MSK-BUG-4 «Ссылка истекает раньше срока» будет удалён без возможности восстановления.',
      ),
    ).toBeVisible();
    expect(cancel()).toHaveFocus();
    expect(confirm().className).toMatch(/colorError/);
    // No typed confirmation: the primary works at once (OQ-4 A).
    expect(within(dialog).queryByRole('textbox')).not.toBeInTheDocument();
    expect(confirm()).toBeEnabled();
  });

  it('with live children it goes on «Дочерние элементы (2) останутся без родителя.»', async () => {
    const { dialog } = await openPurge(EPIC, deletedEpic);

    expect(
      within(dialog).getByText(
        'Рабочий элемент MSK-EPIC-1 «Вход и регистрация» будет удалён без возможности восстановления. Дочерние элементы (2) останутся без родителя.',
      ),
    ).toBeVisible();
  });

  it('a long title wraps wherever it must', async () => {
    const { dialog } = await openPurge(BUG, (data) => {
      recordOf(data, 4).title = 'Я'.repeat(200);
      deletedBug(data);
    });

    const text = within(dialog).getByText(/будет удалён без возможности восстановления/);

    expect(getComputedStyle(text).overflowWrap).toBe('anywhere');
  });

  it('«Отмена», Escape and «Закрыть» change nothing, and focus goes back to «Ещё действия»', async () => {
    const { api } = await openPurge(BUG, deletedBug);

    await userEvent.click(cancel());
    await noDialog();
    expect(moreActions()).toHaveFocus();
    await chooseFromMenu('Удалить навсегда');
    await userEvent.keyboard('{Escape}');
    await noDialog();
    await chooseFromMenu('Удалить навсегда');
    await userEvent.click(within(await purgeDialog()).getByRole('button', { name: 'Закрыть' }));
    await noDialog();

    expect(postsTo(api, 'purge')).toEqual([]);
    expect(recordOf(api.data, 4).title).toBe('Ссылка истекает раньше срока');
  });
});

describe('mod-005 US-6 Scenario: Confirming removes the item', () => {
  it('sends one purge, tells the user, hands the card to its host and removes the item and its children’s link', async () => {
    const { api, notify, onPurged, onChanged } = await openPurge(EPIC, deletedEpic);
    const epicId = recordOf(api.data, 1).id;

    await userEvent.click(confirm());

    await noDialog();
    expect(postsTo(api, 'purge')).toHaveLength(1);
    expect(notify).toHaveBeenCalledWith('Рабочий элемент MSK-EPIC-1 удалён навсегда.');
    expect(onPurged).toHaveBeenCalledTimes(1);
    expect(onChanged).toHaveBeenCalledTimes(1);
    expect(api.data.items.some((record) => record.id === epicId)).toBe(false);
    expect(recordOf(api.data, 6).parentId).toBeNull();
    expect(recordOf(api.data, 8).parentId).toBeNull();
  });

  it('activating «Удалить навсегда» twice sends one request', async () => {
    const { api } = await openPurge(BUG, deletedBug);
    const release = holdPost(WORK_ITEM_PURGE_URL);

    await userEvent.dblClick(confirm());
    release();

    await noDialog();
    expect(postsTo(api, 'purge')).toHaveLength(1);
  });

  it('is busy while the request runs: nothing in the dialog closes it', async () => {
    await openPurge(BUG, deletedBug);
    const release = holdPost(WORK_ITEM_PURGE_URL);

    await userEvent.click(confirm());

    expect(cancel()).toBeDisabled();
    expect(
      within(screen.getByRole('dialog')).getByRole('button', { name: 'Закрыть' }),
    ).toBeDisabled();
    await userEvent.keyboard('{Escape}');
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    release();
    await noDialog();
  });
});

describe('mod-005 design § 8: «Удалить навсегда» refused or failed', () => {
  it('no answer: the dialog stays with the failure above the body, the item still deleted', async () => {
    const { api, onPurged } = await openPurge(BUG, deletedBug);
    server.use(http.post(WORK_ITEM_PURGE_URL, () => HttpResponse.error()));

    await userEvent.click(confirm());

    expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent(
      'Не удалось соединиться с API. Проверьте, что Mustask.Api запущен.',
    );
    expect(confirm()).toBeEnabled();
    expect(onPurged).not.toHaveBeenCalled();
    expect(recordOf(api.data, 4).deletion).not.toBeNull();
  });

  it('a server error stays too, and the primary tries again', async () => {
    const { api, onPurged } = await openPurge(BUG, deletedBug);
    const failed = vi.fn();
    server.use(
      http.post(
        WORK_ITEM_PURGE_URL,
        () => {
          failed();
          return HttpResponse.json({}, { status: 500 });
        },
        { once: true },
      ),
    );

    await userEvent.click(confirm());
    expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent(
      'Ошибка API (HTTP 500).',
    );
    await userEvent.click(confirm());

    await noDialog();
    expect(failed).toHaveBeenCalledTimes(1);
    expect(postsTo(api, 'purge')).toHaveLength(1);
    expect(onPurged).toHaveBeenCalledTimes(1);
  });

  it('409 `…not_deleted` (another user restored it first): the dialog closes, an error notice says so and the card reads the item as live', async () => {
    const { api, notify, onPurged } = await openPurge(BUG, deletedBug);
    const record = recordOf(api.data, 4);
    record.stateId = stateId('BUG', 'Активна');
    record.deletion = null;

    await userEvent.click(confirm());

    await noDialog();
    expect(notify).toHaveBeenCalledWith('Рабочий элемент не удалён.', 'error');
    expect(onPurged).not.toHaveBeenCalled();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled());
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Активна');
  });

  it('404 `workitems.workitem.not_found` (purged meanwhile): the dialog closes and the card gives way to the item’s frame', async () => {
    const { onGone, onPurged } = await openPurge(BUG, deletedBug);
    refuseWith(WORK_ITEM_PURGE_URL, 'workitems.workitem.not_found');

    await userEvent.click(confirm());

    await noDialog();
    expect(onGone).toHaveBeenCalledWith('workItem');
    expect(onPurged).not.toHaveBeenCalled();
  });

  it('403 `organizations.access.forbidden`: «Недостаточно прав» in the dialog, which stays', async () => {
    await openPurge(BUG, deletedBug);
    refuseWith(WORK_ITEM_PURGE_URL, 'organizations.access.forbidden');

    await userEvent.click(confirm());

    expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent(
      'Недостаточно прав',
    );
  });

  it('a lost code disables the primary and leaves «Отмена»', async () => {
    const { serve, api } = await openPurge(BUG, deletedBug);

    await serve('member');

    await waitFor(() => expect(confirm()).toBeDisabled());
    expect(cancel()).toBeEnabled();
    expect(postsTo(api, 'purge')).toEqual([]);
  });
});
