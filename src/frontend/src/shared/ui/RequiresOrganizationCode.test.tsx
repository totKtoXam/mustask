import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import TextField from '@mui/material/TextField';
import { AccessCheckContext, type AccessCheck } from '../lib/access';
import { ConfirmDialog } from './ConfirmDialog';
import { FormDialog } from './FormDialog';
import { RequiresOrganizationCode } from './RequiresOrganizationCode';

// mod-016 design § 2.3, plan P-9: once the re-read of the served codes shows
// the dialog's code gone, the dialog stays with what was typed and its
// refusal, the primary is disabled, Enter no longer submits, and «Отмена»
// still closes it.

const CODE = 'organizations.project.write';

function holding(codes: readonly string[]): AccessCheck {
  return (code, scope) => scope === 'organization' && codes.includes(code);
}

function renderForm(codes: readonly string[]) {
  const onSubmit = vi.fn();
  const onClose = vi.fn();
  render(
    <AccessCheckContext.Provider value={holding(codes)}>
      <RequiresOrganizationCode code={CODE}>
        <FormDialog
          open
          title="Новый проект"
          submitLabel="Создать"
          error="Недостаточно прав"
          onSubmit={onSubmit}
          onClose={onClose}
        >
          <TextField label="Название" defaultValue="Мой проект" />
        </FormDialog>
      </RequiresOrganizationCode>
    </AccessCheckContext.Provider>,
  );
  return { onSubmit, onClose };
}

describe('FormDialog under RequiresOrganizationCode', () => {
  it('submits as usual while the code is held', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderForm([CODE]);

    await user.click(screen.getByRole('button', { name: 'Создать' }));

    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it('keeps the values and the refusal, disables the primary and ignores Enter once the code is gone', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderForm([]);
    const dialog = screen.getByRole('dialog', { name: 'Новый проект' });

    expect(within(dialog).getByRole('alert')).toHaveTextContent('Недостаточно прав');
    expect(within(dialog).getByLabelText('Название')).toHaveValue('Мой проект');
    expect(within(dialog).getByRole('button', { name: 'Создать' })).toBeDisabled();

    await user.type(within(dialog).getByLabelText('Название'), '{Enter}');

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('still closes through «Отмена» and «Закрыть»', async () => {
    const user = userEvent.setup();
    const { onClose } = renderForm([]);

    await user.click(screen.getByRole('button', { name: 'Отмена' }));
    await user.click(screen.getByRole('button', { name: 'Закрыть' }));

    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it('is not locked outside the wrapper, whatever the check says', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(
      <AccessCheckContext.Provider value={holding([])}>
        <FormDialog
          open
          title="Другой"
          submitLabel="Создать"
          onSubmit={onSubmit}
          onClose={() => {}}
        >
          <TextField label="Название" />
        </FormDialog>
      </AccessCheckContext.Provider>,
    );

    await user.click(screen.getByRole('button', { name: 'Создать' }));

    expect(onSubmit).toHaveBeenCalledTimes(1);
  });
});

describe('ConfirmDialog under RequiresOrganizationCode', () => {
  function renderConfirm(codes: readonly string[]) {
    const onConfirm = vi.fn();
    const onClose = vi.fn();
    render(
      <AccessCheckContext.Provider value={holding(codes)}>
        <RequiresOrganizationCode code={CODE}>
          <ConfirmDialog
            open
            title="Деактивировать проект?"
            confirmLabel="Деактивировать"
            error="Недостаточно прав"
            onConfirm={onConfirm}
            onClose={onClose}
          >
            Проект исчезнет из списков.
          </ConfirmDialog>
        </RequiresOrganizationCode>
      </AccessCheckContext.Provider>,
    );
    return { onConfirm, onClose };
  }

  it('confirms as usual while the code is held', async () => {
    const user = userEvent.setup();
    const { onConfirm } = renderConfirm([CODE]);

    await user.click(screen.getByRole('button', { name: 'Деактивировать' }));

    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('keeps its refusal, disables the primary once the code is gone and still cancels', async () => {
    const user = userEvent.setup();
    const { onConfirm, onClose } = renderConfirm([]);
    const dialog = screen.getByRole('dialog', { name: 'Деактивировать проект?' });

    expect(within(dialog).getByRole('alert')).toHaveTextContent('Недостаточно прав');
    expect(within(dialog).getByRole('button', { name: 'Деактивировать' })).toBeDisabled();

    await user.click(within(dialog).getByRole('button', { name: 'Отмена' }));

    expect(onConfirm).not.toHaveBeenCalled();
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
