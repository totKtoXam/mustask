import { describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Button from '@mui/material/Button';
import { ConfirmDialog } from './ConfirmDialog';

function Host({ busy = false, onConfirm = () => {} }: { busy?: boolean; onConfirm?: () => void }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button onClick={() => setOpen(true)}>Сделать языком по умолчанию</Button>
      <ConfirmDialog
        open={open}
        title="Назначить языком по умолчанию"
        confirmLabel="Назначить"
        busy={busy}
        onConfirm={onConfirm}
        onClose={() => setOpen(false)}
      >
        Сделать «Қазақша» (kk) языком по умолчанию?
      </ConfirmDialog>
    </>
  );
}

async function openDialog(user: ReturnType<typeof userEvent.setup>) {
  const opener = screen.getByRole('button', { name: 'Сделать языком по умолчанию' });
  await user.click(opener);
  return { opener, dialog: await screen.findByRole('dialog') };
}

describe('US-9 Scenario: Dialog anatomy', () => {
  it('has a title, «Закрыть» in its header, its content and «Отмена» left of the one filled primary', async () => {
    const user = userEvent.setup();
    render(<Host />);
    const { dialog } = await openDialog(user);

    expect(
      within(dialog).getByRole('heading', { level: 2, name: 'Назначить языком по умолчанию' }),
    ).toBeInTheDocument();
    expect(dialog).toHaveAccessibleName('Назначить языком по умолчанию');
    expect(within(dialog).getByRole('button', { name: 'Закрыть' })).toBeInTheDocument();
    expect(
      within(dialog).getByText('Сделать «Қазақша» (kk) языком по умолчанию?'),
    ).toBeInTheDocument();

    const buttons = within(dialog).getAllByRole('button');
    const cancel = within(dialog).getByRole('button', { name: 'Отмена' });
    const primary = within(dialog).getByRole('button', { name: 'Назначить' });
    expect(buttons.indexOf(cancel)).toBeLessThan(buttons.indexOf(primary));
    expect(buttons.filter((button) => button.classList.contains('MuiButton-contained'))).toEqual([
      primary,
    ]);
    expect(primary).toHaveClass('MuiButton-colorPrimary');
  });

  it('shows a refusal at the top of the body and stays open', () => {
    render(
      <ConfirmDialog
        open
        title="Назначить языком по умолчанию"
        confirmLabel="Назначить"
        error="Только активный язык может стать языком по умолчанию."
        onConfirm={() => {}}
        onClose={() => {}}
      >
        <p>Текст диалога</p>
      </ConfirmDialog>,
    );

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Только активный язык может стать языком по умолчанию.');
    expect(
      alert.compareDocumentPosition(screen.getByText('Текст диалога')) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });
});

describe('US-9 Scenario: Backing out of a dialog', () => {
  it.each([
    ['Escape', async (user: ReturnType<typeof userEvent.setup>) => user.keyboard('{Escape}')],
    [
      '«Закрыть»',
      async (user: ReturnType<typeof userEvent.setup>) =>
        user.click(screen.getByRole('button', { name: 'Закрыть' })),
    ],
    [
      '«Отмена»',
      async (user: ReturnType<typeof userEvent.setup>) =>
        user.click(screen.getByRole('button', { name: 'Отмена' })),
    ],
  ])(
    '%s closes the idle dialog with nothing changed and returns focus to its opener',
    async (_way, backOut) => {
      const user = userEvent.setup();
      const onConfirm = vi.fn();
      render(<Host onConfirm={onConfirm} />);
      const { opener } = await openDialog(user);

      await backOut(user);

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      expect(onConfirm).not.toHaveBeenCalled();
      await waitFor(() => expect(opener).toHaveFocus());
    },
  );
});

describe('US-9 Scenario: A busy dialog cannot be dismissed', () => {
  it('disables «Отмена», «Закрыть» and the primary, shows progress, and ignores Escape', async () => {
    const user = userEvent.setup();
    render(<Host busy />);
    const { dialog } = await openDialog(user);

    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toBeDisabled();
    expect(within(dialog).getByRole('button', { name: 'Закрыть' })).toBeDisabled();
    expect(within(dialog).getByRole('button', { name: 'Назначить' })).toBeDisabled();
    expect(within(dialog).getByRole('progressbar')).toBeInTheDocument();

    await user.keyboard('{Escape}');

    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('ignores a click outside the dialog', async () => {
    const user = userEvent.setup();
    render(<Host busy />);
    await openDialog(user);

    const backdrop = document.querySelector('.MuiBackdrop-root');
    expect(backdrop).not.toBeNull();
    await user.click(backdrop as Element);

    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});

describe('US-9 Scenario: A destructive confirmation looks destructive', () => {
  it('puts the primary action in the error colour and names the action', () => {
    render(
      <ConfirmDialog
        open
        destructive
        title="Удалить рабочий элемент навсегда?"
        confirmLabel="Удалить навсегда"
        onConfirm={() => {}}
        onClose={() => {}}
      >
        Рабочий элемент будет удалён без возможности восстановления.
      </ConfirmDialog>,
    );

    const primary = screen.getByRole('button', { name: 'Удалить навсегда' });
    expect(primary).toHaveClass('MuiButton-contained');
    expect(primary).toHaveClass('MuiButton-colorError');
  });
});
