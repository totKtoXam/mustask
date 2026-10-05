import { describe, expect, it, vi } from 'vitest';
import { StrictMode, useState } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import { ConfirmDialog } from './ConfirmDialog';
import { FormDialog } from './FormDialog';
import { PageChromeContext, type PageChrome } from './pageChrome';
import { PageHeader } from './PageHeader';
import { NotFound } from './StateFrame';

// mod-002 plan-frontend P-7 (a) and P-3 (a): optional props on the shared
// patterns. Each test shows the new behaviour with the prop and the old one
// without it.

describe('mod-002 design § 4.3: FormDialog’s primary is disabled while there is nothing to save', () => {
  it('disables «Сохранить» and ignores Enter with `submitDisabled`', async () => {
    const onSubmit = vi.fn();
    render(
      <FormDialog
        open
        title="Переименование"
        submitLabel="Сохранить"
        submitDisabled
        onSubmit={onSubmit}
        onClose={() => {}}
      >
        <TextField label="Название" defaultValue="Альфа" autoFocus />
      </FormDialog>,
    );

    expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled();
    await userEvent.type(screen.getByRole('textbox', { name: 'Название' }), '{Enter}');
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('keeps the primary enabled without it, as before', async () => {
    const onSubmit = vi.fn();
    render(
      <FormDialog
        open
        title="Создание"
        submitLabel="Создать"
        onSubmit={onSubmit}
        onClose={() => {}}
      >
        <TextField label="Название" autoFocus />
      </FormDialog>,
    );

    expect(screen.getByRole('button', { name: 'Создать' })).toBeEnabled();
    await userEvent.click(screen.getByRole('button', { name: 'Создать' }));
    expect(onSubmit).toHaveBeenCalledOnce();
  });
});

describe('mod-002 design § 4.3: a confirmation opens with focus on «Отмена»', () => {
  it('focuses «Отмена» with `cancelAutoFocus`, so Enter never confirms by accident', async () => {
    const onConfirm = vi.fn();
    const onClose = vi.fn();
    render(
      <ConfirmDialog
        open
        title="Деактивировать?"
        confirmLabel="Деактивировать"
        cancelAutoFocus
        onConfirm={onConfirm}
        onClose={onClose}
      >
        Текст
      </ConfirmDialog>,
    );

    expect(screen.getByRole('button', { name: 'Отмена' })).toHaveFocus();
    await userEvent.keyboard('{Enter}');
    expect(onConfirm).not.toHaveBeenCalled();
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('leaves «Отмена» unfocused without it, as before', () => {
    render(
      <ConfirmDialog
        open
        title="Назначить?"
        confirmLabel="Назначить"
        onConfirm={() => {}}
        onClose={() => {}}
      >
        Текст
      </ConfirmDialog>,
    );

    expect(screen.getByRole('button', { name: 'Отмена' })).not.toHaveFocus();
  });
});

// qa.md D-2: the panel renders under <StrictMode> in development (src/main.tsx),
// where React mounts a new subtree's effects twice. React's one-off `autoFocus`
// put the focus on «Отмена»; MUI's focus trap gave it back to the opener on the
// first cleanup and moved it to the dialog's paper on the re-run.
describe('mod-002 design § 4.3, qa.md D-2: «Отмена» keeps the focus under StrictMode', () => {
  function Host({ cancelAutoFocus }: { cancelAutoFocus?: boolean }) {
    const [open, setOpen] = useState(false);
    return (
      <>
        <Button onClick={() => setOpen(true)}>Открыть</Button>
        <ConfirmDialog
          open={open}
          title="Деактивировать организацию?"
          confirmLabel="Деактивировать"
          cancelAutoFocus={cancelAutoFocus}
          onConfirm={() => {}}
          onClose={() => setOpen(false)}
        >
          Текст
        </ConfirmDialog>
      </>
    );
  }

  async function openDialog(user: ReturnType<typeof userEvent.setup>) {
    await user.click(screen.getByRole('button', { name: 'Открыть' }));
    return screen.findByRole('dialog', { name: 'Деактивировать организацию?' });
  }

  it('focuses «Отмена» at the first opening and again when the dialog is reopened', async () => {
    const user = userEvent.setup();
    render(
      <StrictMode>
        <Host cancelAutoFocus />
      </StrictMode>,
    );
    const opener = screen.getByRole('button', { name: 'Открыть' });

    for (const opening of ['first', 'second']) {
      const dialog = await openDialog(user);
      expect(document.activeElement, `${opening} opening`).toBe(
        within(dialog).getByRole('button', { name: 'Отмена' }),
      );

      await user.keyboard('{Enter}');

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      await waitFor(() => expect(opener).toHaveFocus());
    }
  });

  it('leaves the initial focus to MUI without the prop, as before', async () => {
    const user = userEvent.setup();
    render(
      <StrictMode>
        <Host />
      </StrictMode>,
    );

    const dialog = await openDialog(user);

    expect(within(dialog).getByRole('button', { name: 'Отмена' })).not.toHaveFocus();
    expect(dialog).toContainElement(document.activeElement as HTMLElement);
  });
});

describe('mod-002 design § 5.2, § 7.2: the views are named on their own', () => {
  const views = [
    { label: 'Активные', to: '/organizations' },
    { label: 'Деактивированные', to: '/organizations/deactivated' },
  ];

  it('names the tabs by `viewsLabel`', () => {
    render(
      <MemoryRouter initialEntries={['/organizations']}>
        <PageHeader title="Организации" views={views} viewsLabel="Организации по статусу" />
      </MemoryRouter>,
    );

    expect(screen.getByRole('tablist', { name: 'Организации по статусу' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Активные' })).toHaveAttribute('aria-selected', 'true');
  });

  it('names them by the title without it, as before', () => {
    render(
      <MemoryRouter initialEntries={['/organizations']}>
        <PageHeader title="Организации" views={views} />
      </MemoryRouter>,
    );

    expect(screen.getByRole('tablist', { name: 'Организации' })).toBeInTheDocument();
  });
});

describe('mod-002 design § 12: «Команда не найдена» keeps the settings navigation', () => {
  function chrome(): PageChrome & { registerNotFound: ReturnType<typeof vi.fn> } {
    return {
      registerHeader: () => () => {},
      registerNotFound: vi.fn(() => () => {}),
      trail: [],
      trailPlacement: 'topBar',
    };
  }

  it('registers nothing with the shell under `keepNavigation`', () => {
    const value = chrome();
    render(
      <MemoryRouter>
        <PageChromeContext.Provider value={value}>
          <NotFound level="team" keepNavigation />
        </PageChromeContext.Provider>
      </MemoryRouter>,
    );

    expect(
      screen.getByRole('heading', { level: 1, name: 'Команда не найдена' }),
    ).toBeInTheDocument();
    expect(value.registerNotFound).not.toHaveBeenCalled();
  });

  it('registers not-found without it, as before', () => {
    const value = chrome();
    render(
      <MemoryRouter>
        <PageChromeContext.Provider value={value}>
          <NotFound level="team" />
        </PageChromeContext.Provider>
      </MemoryRouter>,
    );

    expect(value.registerNotFound).toHaveBeenCalledOnce();
  });
});
