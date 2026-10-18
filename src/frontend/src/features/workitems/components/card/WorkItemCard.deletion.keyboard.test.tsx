import { describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { deleteRecord, recordOf, stateId, type Dataset } from '../../testFixtures';
import {
  chooseFromMenu,
  dialogTitled,
  noDialog,
  openAs,
  postsTo,
} from './testDeletion';

// mod-005 QA: the keys of the three dialogs (design § 3, § 5, § 6, § 11). Enter must never confirm a
// deletion, a restore or a purge the user has not reached with a control: the Delete dialog opens
// with focus on its frame, the Purge dialog on «Отмена», and in the Restore dialog Enter on the
// closed list opens the list and submits nothing. All three are modal `dialog`s, never `alertdialog`
// (tech-015 K-14). Each user action draws the kit's fields in jsdom, which are slow.

vi.setConfig({ testTimeout: 120_000 });

const STORY = 'MSK-US-2';

/** MSK-US-2 deleted by Ann out of «Активна». */
function deletedFromActive(data: Dataset) {
  recordOf(data, 2).stateId = stateId('US', 'Активна');
  deleteRecord(recordOf(data, 2));
}

describe('mod-005 QA, design § 3, § 11: «Удалить» opens with focus on the frame', () => {
  it('Enter confirms nothing until a control is focused; the dialog is modal and is no alertdialog', async () => {
    const { api } = await openAs(STORY, { role: 'member' });
    await chooseFromMenu('Удалить');
    const dialog = await dialogTitled('Удалить рабочий элемент?');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Удалить' })).not.toHaveFocus();

    await userEvent.keyboard('{Enter}');

    expect(postsTo(api, 'delete')).toEqual([]);
    expect(screen.getByRole('dialog', { name: 'Удалить рабочий элемент?' })).toBeVisible();
  });
});

describe('mod-005 QA, design § 6, § 11: «Удалить навсегда» opens with focus on «Отмена»', () => {
  it('Enter cancels instead of purging: nothing is sent and the item is still there', async () => {
    const { api, onPurged } = await openAs(STORY, { role: 'admin', prepare: deletedFromActive });
    await chooseFromMenu('Удалить навсегда');
    const dialog = await dialogTitled('Удалить рабочий элемент навсегда?');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();

    await userEvent.keyboard('{Enter}');

    await noDialog();
    expect(postsTo(api, 'purge')).toEqual([]);
    expect(onPurged).not.toHaveBeenCalled();
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Удалена');
  });
});

describe('mod-005 QA, design § 5, § 11: the keys of «Восстановить»', () => {
  it('Enter on the closed list opens it and submits nothing; Escape closes the list alone; Enter on «Восстановить» submits', async () => {
    const { api } = await openAs(STORY, { role: 'member', prepare: deletedFromActive });
    await userEvent.click(screen.getByRole('button', { name: 'Восстановить' }));
    const dialog = await dialogTitled('Восстановить рабочий элемент?');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    const field = screen.getByRole('combobox', { name: 'Статус после восстановления' });

    field.focus();
    await userEvent.keyboard('{Enter}');

    expect(await screen.findByRole('listbox')).toBeVisible();
    expect(postsTo(api, 'restore')).toEqual([]);

    await userEvent.keyboard('{Escape}');

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'Восстановить рабочий элемент?' })).toBeVisible();
    expect(postsTo(api, 'restore')).toEqual([]);

    within(dialog).getByRole('button', { name: 'Восстановить' }).focus();
    await userEvent.keyboard('{Enter}');

    await noDialog();
    expect(postsTo(api, 'restore')).toEqual([
      expect.objectContaining({ body: { stateId: stateId('US', 'Активна') } }),
    ]);
  });
});
