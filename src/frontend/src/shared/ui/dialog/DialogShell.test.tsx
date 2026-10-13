import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, rulesFor, theme } from '../../../test/kitTesting';
import { setViewportWidth } from '../../../test/viewport';
import { ActionButton } from '../actions/ActionButton';
import { ConfirmDialog } from '../ConfirmDialog';
import {
  DialogShell,
  type DialogCloseReason,
  type DialogGuard,
  type DialogShellProps,
} from './DialogShell';

// tech-015 design § 5.10 «DialogShell»: roles and names, the card size's invisible backdrop,
// the two icons, the close reasons, the busy refusal and the unsaved-changes guard.

type Shell = Partial<DialogShellProps>;

const GUARD: DialogGuard = {
  dirty: true,
  title: 'Закрыть без сохранения?',
  text: 'Изменения в MSK-US-2 не сохранены.',
  discardLabel: 'Закрыть без сохранения',
};

function Card({
  onClose,
  children,
  ...props
}: Shell & { onClose?: (reason: DialogCloseReason) => void }) {
  return (
    <DialogShell
      open
      size="card"
      labelledBy="card-heading"
      onClose={onClose ?? (() => {})}
      header={<h2 id="card-heading">Пользовательская история MSK-US-2</h2>}
      band={<div>Полоса свойств</div>}
      {...props}
    >
      {children ?? <p>Тело карточки</p>}
    </DialogShell>
  );
}

function dialog() {
  return screen.getByRole('dialog');
}

function paper(): HTMLElement {
  return dialog().closest('.MuiDialog-paper') as HTMLElement;
}

function backdrop(): HTMLElement {
  return document.querySelector('.MuiBackdrop-root') as HTMLElement;
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('tech-015 Scenario: DialogShell (design § 5.10)', () => {
  describe('Scenario: role and name', () => {
    it('is a modal dialog named by the heading it points at', () => {
      render(<Card />);
      expect(dialog()).toHaveAccessibleName('Пользовательская история MSK-US-2');
      expect(dialog()).toHaveAttribute('aria-labelledby', 'card-heading');
      expect(dialog()).toHaveAttribute('aria-modal', 'true');
    });

    it('is named by its title for a confirm or form size, an h2 of the dialog', () => {
      render(
        <DialogShell open size="form" title="Новый язык" onClose={() => {}}>
          Поля формы
        </DialogShell>,
      );
      expect(dialog()).toHaveAccessibleName('Новый язык');
      expect(screen.getByRole('heading', { level: 2, name: 'Новый язык' })).toBeInTheDocument();
    });

    it('is named by `label` when there is no heading to point at — a card still loading', () => {
      render(
        <DialogShell open size="card" label="Рабочий элемент MSK-US-2" onClose={() => {}}>
          <div role="status">Загрузка данных</div>
        </DialogShell>,
      );
      expect(dialog()).toHaveAccessibleName('Рабочий элемент MSK-US-2');
      expect(dialog()).toHaveAttribute('aria-label', 'Рабочий элемент MSK-US-2');
      expect(dialog()).not.toHaveAttribute('aria-labelledby');
    });

    it('keeps role dialog for every size, ConfirmDialog included (K-14)', () => {
      render(
        <ConfirmDialog
          open
          title="Удалить?"
          confirmLabel="Удалить"
          onConfirm={() => {}}
          onClose={() => {}}
        >
          Текст
        </ConfirmDialog>,
      );
      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.queryByRole('alertdialog')).toBeNull();
    });

    it('warns in development when it has no name, or more than one', () => {
      const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
      const { unmount } = render(
        <DialogShell open size="card" onClose={() => {}}>
          Нет имени
        </DialogShell>,
      );
      expect(warn).toHaveBeenCalledWith(expect.stringContaining('exactly one of'));
      unmount();
      warn.mockClear();
      render(
        <DialogShell open size="card" title="Один" label="Два" onClose={() => {}}>
          Два имени
        </DialogShell>,
      );
      expect(warn).toHaveBeenCalledWith(expect.stringContaining('exactly one of'));
    });

    it('does not warn when it has exactly one name', () => {
      const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
      render(<Card />);
      expect(warn).not.toHaveBeenCalled();
    });
  });

  describe('Scenario: the card size’s Paper', () => {
    it('is 90 % × 90 % of the window with no margin, no height cap and a column layout', () => {
      render(<Card />);
      const style = getComputedStyle(paper());
      expect(style.width).toBe(`${theme.layout.card.dialogShare * 100}%`);
      expect(style.height).toBe(`${theme.layout.card.dialogShare * 100}%`);
      expect(parseFloat(style.margin)).toBe(0);
      expect(style.maxHeight).toBe('none');
      expect(style.maxWidth).toBe('none');
      expect(style.display).toBe('flex');
      expect(style.flexDirection).toBe('column');
      expect(style.borderRadius).toBe(`${theme.shape.borderRadius}px`);
    });

    it('keeps MUI’s elevation-24 shadow — no shadow of the kit’s own', () => {
      render(<Card />);
      expect(paper()).toHaveClass('MuiPaper-elevation24');
    });

    it('declares the kit-card container for the header and the band to read', () => {
      render(<Card />);
      expect(getComputedStyle(paper()).containerType).toBe('inline-size');
      const declared = rulesFor(paper(), '').find((rule) => rule.declarations['container-name']);
      expect(declared?.declarations['container-name']).toBe('kit-card');
    });

    it.each(['confirm', 'form'] as const)(
      'does not declare it on the %s size, whose width follows its content',
      (size) => {
        render(
          <DialogShell open size={size} title="Заголовок" onClose={() => {}}>
            Текст
          </DialogShell>,
        );
        expect(getComputedStyle(paper()).containerType).toBe('');
        expect(rulesFor(paper(), '').some((rule) => rule.declarations['container-name'])).toBe(
          false,
        );
      },
    );

    it('has a confirm size at MUI’s xs and a form size at sm', () => {
      const { unmount } = render(
        <DialogShell open size="confirm" title="Вопрос" onClose={() => {}}>
          Текст
        </DialogShell>,
      );
      expect(paper()).toHaveClass('MuiDialog-paperWidthXs');
      unmount();
      render(
        <DialogShell open size="form" title="Форма" onClose={() => {}}>
          Текст
        </DialogShell>,
      );
      expect(paper()).toHaveClass('MuiDialog-paperWidthSm');
    });
  });

  describe('Scenario: the card draws no dimming, and the outside click is still caught', () => {
    it('has an invisible backdrop — transparent, but still an element', () => {
      render(<Card />);
      expect(backdrop()).toBeInTheDocument();
      expect(backdrop()).toHaveClass('MuiBackdrop-invisible');
      const style = getComputedStyle(backdrop());
      expect(['transparent', 'rgba(0, 0, 0, 0)']).toContain(style.backgroundColor);
    });

    it('is not hideBackdrop: a click outside the card calls onClose(backdrop)', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<Card onClose={onClose} />);
      await user.click(backdrop());
      expect(onClose).toHaveBeenCalledExactlyOnceWith('backdrop');
    });

    it.each(['confirm', 'form'] as const)('keeps the default dimming on the %s size', (size) => {
      render(
        <DialogShell open size={size} title="Заголовок" onClose={() => {}}>
          Текст
        </DialogShell>,
      );
      expect(backdrop()).not.toHaveClass('MuiBackdrop-invisible');
      expect(getComputedStyle(backdrop()).backgroundColor).toBe('rgba(0, 0, 0, 0.5)');
    });

    it('dims under a ConfirmDialog opened over the card — a question dims, the card included', () => {
      render(
        <>
          <Card />
          <ConfirmDialog
            open
            title="Закрыть без сохранения?"
            confirmLabel="Закрыть без сохранения"
            onConfirm={() => {}}
            onClose={() => {}}
          >
            Текст вопроса
          </ConfirmDialog>
        </>,
      );
      const backdrops = Array.from(document.querySelectorAll('.MuiBackdrop-root'));
      expect(backdrops).toHaveLength(2);
      expect(backdrops[0]).toHaveClass('MuiBackdrop-invisible');
      expect(backdrops[1]).not.toHaveClass('MuiBackdrop-invisible');
      expect(getComputedStyle(backdrops[1] as Element).backgroundColor).toBe('rgba(0, 0, 0, 0.5)');
    });
  });

  describe('Scenario: the two icons', () => {
    it('draws «Развернуть на страницу» and «Закрыть» first in the DOM, with their tooltips', async () => {
      const user = userEvent.setup();
      render(<Card onExpand={() => {}} />);
      const buttons = within(paper()).getAllByRole('button');
      expect(buttons.map((button) => button.getAttribute('aria-label'))).toEqual([
        'Развернуть на страницу',
        'Закрыть',
      ]);
      await user.hover(screen.getByRole('button', { name: 'Закрыть' }));
      expect(await screen.findByRole('tooltip')).toHaveTextContent('Закрыть');
    });

    it('draws no «Развернуть на страницу» without onExpand', () => {
      render(<Card />);
      expect(screen.queryByRole('button', { name: 'Развернуть на страницу' })).toBeNull();
      expect(screen.getByRole('button', { name: 'Закрыть' })).toBeInTheDocument();
    });

    it('puts them dialogIconGap apart, the ✕ dialogIconInset from the right edge and headerPadTop from the top', () => {
      render(<Card onExpand={() => {}} />);
      const icons = paper().querySelector('[data-dialog-icons]') as HTMLElement;
      const style = getComputedStyle(icons);
      expect(style.position).toBe('absolute');
      expect(px(style.top)).toBe(theme.layout.card.headerPadTop);
      expect(px(style.right)).toBe(theme.layout.card.dialogIconInset);
      expect(px(style.gap)).toBe(theme.layout.card.dialogIconGap);
      // The reference's measures: the ✕'s centre 21 from the edge, the ⤢'s 55 (pitch 34).
      const { box } = theme.layout.iconAction.bar;
      expect(theme.layout.card.dialogIconInset + box / 2).toBe(21);
      expect(box + theme.layout.card.dialogIconGap).toBe(34);
      expect(theme.layout.card.dialogIconInset + box / 2 + 34).toBe(55);
    });

    it('makes each a 32 px bar action', () => {
      render(<Card onExpand={() => {}} />);
      for (const button of within(paper()).getAllByRole('button')) {
        expect(px(getComputedStyle(button).width)).toBe(theme.layout.iconAction.bar.box);
      }
    });

    it('calls onExpand and never guards it', async () => {
      const user = userEvent.setup();
      const onExpand = vi.fn();
      const onClose = vi.fn();
      render(<Card onExpand={onExpand} onClose={onClose} guard={GUARD} />);
      await user.click(screen.getByRole('button', { name: 'Развернуть на страницу' }));
      expect(onExpand).toHaveBeenCalledOnce();
      expect(onClose).not.toHaveBeenCalled();
      expect(screen.getAllByRole('dialog')).toHaveLength(1);
    });
  });

  describe('Scenario: the areas', () => {
    it('stacks the header, the band, the alert and the panel in that order', () => {
      render(<Card alert={<div role="status">Конфликт версий</div>} />);
      const areas = Array.from(paper().querySelectorAll('[data-dialog-area]')).map(
        (area) => (area as HTMLElement).dataset.dialogArea,
      );
      expect(areas).toEqual(['header', 'band', 'alert', 'panel']);
    });

    it('lets the panel scroll and keep its scrollbar gutter, while the header and band stay', () => {
      render(<Card />);
      const panel = paper().querySelector('[data-dialog-area="panel"]') as HTMLElement;
      const style = getComputedStyle(panel);
      expect(style.overflowY).toBe('auto');
      expect(style.scrollbarGutter).toBe('stable');
      expect(parseFloat(style.minHeight)).toBe(0);
      expect(panel).toHaveTextContent('Тело карточки');
      expect(
        getComputedStyle(paper().querySelector('[data-dialog-area="header"]') as Element).overflowY,
      ).not.toBe('auto');
    });

    it('tells the header how far the icons reach, so the type line stops short of them', () => {
      render(<Card onExpand={() => {}} />);
      const header = paper().querySelector('[data-dialog-area="header"]') as HTMLElement;
      const { dialogIconInset, dialogIconGap, rightInset } = theme.layout.card;
      const { box } = theme.layout.iconAction.bar;
      const reach = dialogIconInset + box + dialogIconGap + box - rightInset;
      const declared = rulesFor(header, '').find((rule) => rule.declarations['--kit-card-icons']);
      expect(declared?.declarations['--kit-card-icons']).toBe(`${reach}px`);
    });

    it('draws the lead and the footer of a form size, the footer as the dialog’s actions', () => {
      render(
        <DialogShell
          open
          size="form"
          title="Новый язык"
          lead="Заполните поля."
          footer={<ActionButton label="Сохранить" tone="primary" />}
          onClose={() => {}}
        >
          Поля
        </DialogShell>,
      );
      expect(screen.getByText('Заполните поля.')).toBeInTheDocument();
      expect(
        screen.getByRole('button', { name: 'Сохранить' }).closest('.MuiDialogActions-root'),
      ).not.toBeNull();
    });
  });

  describe('Scenario: focus', () => {
    it('opens with focus on the dialog’s own frame, in no field, and traps Tab inside', async () => {
      const user = userEvent.setup();
      render(
        <>
          <button type="button">Вне окна</button>
          <Card header={<h2 id="card-heading">Карточка</h2>}>
            <input aria-label="Поле" />
          </Card>
        </>,
      );
      await waitFor(() => expect(dialog().contains(document.activeElement)).toBe(true));
      expect(document.activeElement).not.toBe(screen.getByLabelText('Поле'));
      expect(document.activeElement?.tagName).not.toBe('INPUT');
      for (let step = 0; step < 6; step += 1) {
        await user.tab();
        expect(dialog().contains(document.activeElement)).toBe(true);
      }
    });

    it('returns focus to the opener when it closes', async () => {
      const user = userEvent.setup();
      function Host() {
        const [open, setOpen] = useState(false);
        return (
          <>
            <button type="button" onClick={() => setOpen(true)}>
              Открыть окно карточки
            </button>
            <Card open={open} onClose={() => setOpen(false)} />
          </>
        );
      }
      render(<Host />);
      const opener = screen.getByRole('button', { name: 'Открыть окно карточки' });
      await user.click(opener);
      await screen.findByRole('dialog');
      await user.keyboard('{Escape}');
      await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
      await waitFor(() => expect(opener).toHaveFocus());
    });

    it('calls onExited after the dialog has closed, for a host that restores focus itself', async () => {
      const user = userEvent.setup();
      const onExited = vi.fn();
      function Host() {
        const [open, setOpen] = useState(true);
        return <Card open={open} onClose={() => setOpen(false)} onExited={onExited} />;
      }
      render(<Host />);
      expect(onExited).not.toHaveBeenCalled();
      await user.click(screen.getByRole('button', { name: 'Закрыть' }));
      await waitFor(() => expect(onExited).toHaveBeenCalledOnce());
    });
  });

  describe('Scenario: closing, and why', () => {
    it.each([
      [
        'Escape',
        'escape',
        async (user: ReturnType<typeof userEvent.setup>) => user.keyboard('{Escape}'),
      ],
      [
        '«Закрыть»',
        'close',
        async (user: ReturnType<typeof userEvent.setup>) =>
          user.click(screen.getByRole('button', { name: 'Закрыть' })),
      ],
      [
        'a click outside',
        'backdrop',
        async (user: ReturnType<typeof userEvent.setup>) => user.click(backdrop()),
      ],
    ] as const)('%s calls onClose with the reason %s', async (_way, reason, dismiss) => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<Card onClose={onClose} />);
      await dismiss(user);
      expect(onClose).toHaveBeenCalledExactlyOnceWith(reason);
    });

    it('lets the plain title of a form size close it through its «Закрыть»', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(
        <DialogShell open size="form" title="Новый язык" onClose={onClose}>
          Поля
        </DialogShell>,
      );
      await user.click(screen.getByRole('button', { name: 'Закрыть' }));
      expect(onClose).toHaveBeenCalledExactlyOnceWith('close');
    });
  });

  describe('Scenario: busy refuses every way out', () => {
    it.each([
      ['Escape', async (user: ReturnType<typeof userEvent.setup>) => user.keyboard('{Escape}')],
      [
        'the outside click',
        async (user: ReturnType<typeof userEvent.setup>) => user.click(backdrop()),
      ],
      [
        '«Закрыть»',
        async (user: ReturnType<typeof userEvent.setup>) =>
          user.click(screen.getByRole('button', { name: 'Закрыть' })),
      ],
    ])('%s does nothing', async (_way, dismiss) => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<Card onClose={onClose} busy onExpand={() => {}} />);
      await dismiss(user);
      expect(onClose).not.toHaveBeenCalled();
      expect(dialog()).toBeInTheDocument();
    });

    it('makes the two icons inert but keeps them in the Tab order', async () => {
      const user = userEvent.setup();
      const onExpand = vi.fn();
      render(<Card busy onExpand={onExpand} />);
      const expand = screen.getByRole('button', { name: 'Развернуть на страницу' });
      const close = screen.getByRole('button', { name: 'Закрыть' });
      expect(expand).toHaveAttribute('aria-disabled', 'true');
      expect(close).toHaveAttribute('aria-disabled', 'true');
      expect(expand).not.toBeDisabled();
      await user.click(expand);
      expect(onExpand).not.toHaveBeenCalled();
    });
  });

  describe('Scenario: the unsaved-changes guard', () => {
    function GuardHost({
      guard = GUARD,
      onClose,
    }: {
      guard?: DialogGuard;
      onClose: (r: DialogCloseReason) => void;
    }) {
      const [open, setOpen] = useState(true);
      return (
        <Card
          open={open}
          guard={guard}
          onClose={(reason) => {
            onClose(reason);
            setOpen(false);
          }}
        />
      );
    }

    it('does not intervene while nothing is dirty', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<GuardHost guard={{ ...GUARD, dirty: false }} onClose={onClose} />);
      await user.click(screen.getByRole('button', { name: 'Закрыть' }));
      expect(onClose).toHaveBeenCalledExactlyOnceWith('close');
    });

    it.each([
      [
        '«Закрыть»',
        async (user: ReturnType<typeof userEvent.setup>) =>
          user.click(screen.getByRole('button', { name: 'Закрыть' })),
      ],
      ['Escape', async (user: ReturnType<typeof userEvent.setup>) => user.keyboard('{Escape}')],
      [
        'the outside click',
        async (user: ReturnType<typeof userEvent.setup>) => user.click(backdrop()),
      ],
    ])(
      'asks first when %s dismisses a dirty dialog, and calls onClose only on a discard',
      async (_way, dismiss) => {
        const user = userEvent.setup();
        const onClose = vi.fn();
        render(<GuardHost onClose={onClose} />);
        await dismiss(user);
        const question = await screen.findByRole('dialog', { name: 'Закрыть без сохранения?' });
        expect(onClose).not.toHaveBeenCalled();
        expect(
          within(question).getByText('Изменения в MSK-US-2 не сохранены.'),
        ).toBeInTheDocument();
        await user.click(within(question).getByRole('button', { name: 'Закрыть без сохранения' }));
        expect(onClose).toHaveBeenCalledOnce();
      },
    );

    it('passes the reason of the original dismissal on to onClose', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<GuardHost onClose={onClose} />);
      await user.keyboard('{Escape}');
      const question = await screen.findByRole('dialog', { name: 'Закрыть без сохранения?' });
      await user.click(within(question).getByRole('button', { name: 'Закрыть без сохранения' }));
      expect(onClose).toHaveBeenCalledExactlyOnceWith('escape');
    });

    it('reads «Продолжить редактирование» on the cancel by default, and keeps the card open on it', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<GuardHost onClose={onClose} />);
      await user.click(screen.getByRole('button', { name: 'Закрыть' }));
      const question = await screen.findByRole('dialog', { name: 'Закрыть без сохранения?' });
      await user.click(within(question).getByRole('button', { name: 'Продолжить редактирование' }));
      await waitFor(() =>
        expect(screen.queryByRole('dialog', { name: 'Закрыть без сохранения?' })).toBeNull(),
      );
      expect(onClose).not.toHaveBeenCalled();
      expect(
        screen.getByRole('dialog', { name: 'Пользовательская история MSK-US-2' }),
      ).toBeInTheDocument();
    });

    it('takes a cancel label of the host’s', async () => {
      const user = userEvent.setup();
      render(<GuardHost guard={{ ...GUARD, cancelLabel: 'Вернуться' }} onClose={() => {}} />);
      await user.click(screen.getByRole('button', { name: 'Закрыть' }));
      const question = await screen.findByRole('dialog', { name: 'Закрыть без сохранения?' });
      expect(within(question).getByRole('button', { name: 'Вернуться' })).toBeInTheDocument();
      expect(
        within(question).queryByRole('button', { name: 'Продолжить редактирование' }),
      ).toBeNull();
    });

    it('holds the initial focus on the cancel, so Enter never discards', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<GuardHost onClose={onClose} />);
      await user.click(screen.getByRole('button', { name: 'Закрыть' }));
      const question = await screen.findByRole('dialog', { name: 'Закрыть без сохранения?' });
      await waitFor(() =>
        expect(
          within(question).getByRole('button', { name: 'Продолжить редактирование' }),
        ).toHaveFocus(),
      );
      await user.keyboard('{Enter}');
      expect(onClose).not.toHaveBeenCalled();
      await waitFor(() =>
        expect(screen.queryByRole('dialog', { name: 'Закрыть без сохранения?' })).toBeNull(),
      );
    });

    it('closes only the question on the first Escape, and the card on a second', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<GuardHost guard={{ ...GUARD, dirty: true }} onClose={onClose} />);
      await user.keyboard('{Escape}');
      await screen.findByRole('dialog', { name: 'Закрыть без сохранения?' });
      await user.keyboard('{Escape}');
      await waitFor(() =>
        expect(screen.queryByRole('dialog', { name: 'Закрыть без сохранения?' })).toBeNull(),
      );
      expect(onClose).not.toHaveBeenCalled();
      expect(
        screen.getByRole('dialog', { name: 'Пользовательская история MSK-US-2' }),
      ).toBeInTheDocument();
    });

    it('asks nothing while busy, and refuses the dismissal', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<Card onClose={onClose} guard={GUARD} busy />);
      await user.keyboard('{Escape}');
      expect(screen.getAllByRole('dialog')).toHaveLength(1);
      expect(onClose).not.toHaveBeenCalled();
    });

    it('is a destructive confirmation in the error colour, over the card', async () => {
      const user = userEvent.setup();
      render(<GuardHost onClose={() => {}} />);
      await user.click(screen.getByRole('button', { name: 'Закрыть' }));
      const question = await screen.findByRole('dialog', { name: 'Закрыть без сохранения?' });
      const discard = within(question).getByRole('button', { name: 'Закрыть без сохранения' });
      expect(discard).toHaveClass('MuiButton-colorError');
    });
  });

  describe('Scenario: full screen below md (1024 px)', () => {
    it('is the card at 90 % at 1440 px and full screen, square and without margin, at 800 px', () => {
      setViewportWidth(1440);
      const { unmount } = render(<Card />);
      expect(paper()).not.toHaveClass('MuiDialog-paperFullScreen');
      expect(getComputedStyle(paper()).width).toBe('90%');
      unmount();

      setViewportWidth(800);
      render(<Card />);
      expect(paper()).toHaveClass('MuiDialog-paperFullScreen');
      const style = getComputedStyle(paper());
      expect(style.width).toBe('100%');
      expect(style.height).toBe('100%');
      expect(parseFloat(style.borderRadius)).toBe(0);
    });

    it('follows a resize while it is open', () => {
      setViewportWidth(1440);
      render(<Card />);
      expect(paper()).not.toHaveClass('MuiDialog-paperFullScreen');
      act(() => setViewportWidth(700));
      expect(paper()).toHaveClass('MuiDialog-paperFullScreen');
    });

    it('is never full screen when told so', () => {
      setViewportWidth(700);
      render(<Card fullScreenBelow={false} />);
      expect(paper()).not.toHaveClass('MuiDialog-paperFullScreen');
    });

    it('is not full screen at the confirm and form sizes by default', () => {
      setViewportWidth(700);
      render(
        <DialogShell open size="form" title="Форма" onClose={() => {}}>
          Текст
        </DialogShell>,
      );
      expect(paper()).not.toHaveClass('MuiDialog-paperFullScreen');
    });
  });

  describe('Scenario: stacking', () => {
    it('opens a ConfirmDialog above the card, which is still the page’s modal underneath', () => {
      render(
        <>
          <Card />
          <ConfirmDialog
            open
            title="Вопрос"
            confirmLabel="Да"
            onConfirm={() => {}}
            onClose={() => {}}
          >
            Текст
          </ConfirmDialog>
        </>,
      );
      const dialogs = screen.getAllByRole('dialog', { hidden: true });
      expect(dialogs).toHaveLength(2);
      // The question is the only one a reader reaches; the card is inert beneath it.
      expect(screen.getAllByRole('dialog')).toHaveLength(1);
      expect(screen.getByRole('dialog')).toHaveAccessibleName('Вопрос');
    });
  });
});
