import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import TextField from '@mui/material/TextField';
import { FormDialog } from './FormDialog';

function renderDialog({ busy = false, error }: { busy?: boolean; error?: string } = {}) {
  const onSubmit = vi.fn();
  const onClose = vi.fn();
  render(
    <FormDialog
      open
      title="Новый рабочий элемент"
      submitLabel="Создать"
      busy={busy}
      error={error}
      onSubmit={onSubmit}
      onClose={onClose}
    >
      <TextField label="Заголовок" disabled={busy} />
    </FormDialog>,
  );
  return { onSubmit, onClose };
}

describe('US-9 Scenario: Patterns beyond this task are designed now — the form dialog', () => {
  it('has the dialog anatomy around its fields: title, «Закрыть», «Отмена» left of the filled submit', () => {
    renderDialog();

    const dialog = screen.getByRole('dialog', { name: 'Новый рабочий элемент' });
    expect(within(dialog).getByRole('button', { name: 'Закрыть' })).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Заголовок')).toBeInTheDocument();
    const buttons = within(dialog).getAllByRole('button');
    const cancel = within(dialog).getByRole('button', { name: 'Отмена' });
    const submit = within(dialog).getByRole('button', { name: 'Создать' });
    expect(buttons.indexOf(cancel)).toBeLessThan(buttons.indexOf(submit));
    expect(submit).toHaveAttribute('type', 'submit');
    expect(buttons.filter((button) => button.classList.contains('MuiButton-contained'))).toEqual([
      submit,
    ]);
  });

  it('submits from the footer and from Enter in a field, without the browser’s own submission', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderDialog();

    await user.click(screen.getByRole('button', { name: 'Создать' }));
    await user.type(screen.getByLabelText('Заголовок'), 'Вход по ссылке{Enter}');

    expect(onSubmit).toHaveBeenCalledTimes(2);
  });

  it('shows an error not tied to a field above the first field', () => {
    renderDialog({ error: 'Не удалось создать рабочий элемент.' });

    const alert = screen.getByRole('alert');
    expect(
      alert.compareDocumentPosition(screen.getByLabelText('Заголовок')) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it('while busy disables every control, shows progress and ignores Escape', async () => {
    const user = userEvent.setup();
    const { onClose } = renderDialog({ busy: true });

    expect(screen.getByRole('button', { name: 'Отмена' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Закрыть' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Создать' })).toBeDisabled();
    expect(screen.getByLabelText('Заголовок')).toBeDisabled();
    expect(screen.getByRole('progressbar')).toBeInTheDocument();

    await user.keyboard('{Escape}');
    expect(onClose).not.toHaveBeenCalled();
  });
});
