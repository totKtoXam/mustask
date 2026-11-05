import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ConfirmDialog } from './ConfirmDialog';

// tech-015 design § 5.10 «ConfirmDialog»: `cancelLabel` (default «Отмена») so the card's
// unsaved-changes question reads «Продолжить редактирование». Nothing else changes —
// including the role, which stays `dialog` (K-14).

function renderConfirm(props: Partial<Parameters<typeof ConfirmDialog>[0]> = {}) {
  return render(
    <ConfirmDialog
      open
      title="Закрыть без сохранения?"
      confirmLabel="Закрыть без сохранения"
      onConfirm={() => {}}
      onClose={() => {}}
      {...props}
    >
      Изменения в MSK-US-2 не сохранены.
    </ConfirmDialog>,
  );
}

describe('tech-015 Scenario: ConfirmDialog.cancelLabel (design § 5.10)', () => {
  it('reads «Отмена» by default, as before', () => {
    renderConfirm();
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toBeInTheDocument();
  });

  it('reads the host’s label on the cancel button instead', () => {
    renderConfirm({ cancelLabel: 'Продолжить редактирование' });
    const dialog = screen.getByRole('dialog');
    expect(
      within(dialog).getByRole('button', { name: 'Продолжить редактирование' }),
    ).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: 'Отмена' })).toBeNull();
  });

  it('is still the cancel: it closes the dialog and confirms nothing', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    const onConfirm = vi.fn();
    renderConfirm({ cancelLabel: 'Продолжить редактирование', onClose, onConfirm });
    await user.click(screen.getByRole('button', { name: 'Продолжить редактирование' }));
    expect(onClose).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('holds the initial focus when `cancelAutoFocus` is set, whatever it reads', async () => {
    renderConfirm({
      cancelLabel: 'Продолжить редактирование',
      cancelAutoFocus: true,
      destructive: true,
    });
    await vi.waitFor(() =>
      expect(screen.getByRole('button', { name: 'Продолжить редактирование' })).toHaveFocus(),
    );
  });

  it('is disabled with the rest of the dialog while busy', () => {
    renderConfirm({ cancelLabel: 'Продолжить редактирование', busy: true });
    expect(screen.getByRole('button', { name: 'Продолжить редактирование' })).toBeDisabled();
  });

  it('stays a `dialog`, never an `alertdialog` (K-14)', () => {
    renderConfirm({ cancelLabel: 'Продолжить редактирование' });
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).toBeNull();
  });
});
