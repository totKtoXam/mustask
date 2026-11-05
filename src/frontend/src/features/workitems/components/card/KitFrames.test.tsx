import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { DialogCloseReason } from '../../../../shared/ui';
import type { CardFrameProps } from './cardFrame';
import { createFrameStore } from './frameStore';
import { DialogFrame, PublishFrame } from './KitFrames';
import { fill, held, openCard, typeInto } from './testCard';

vi.setConfig({ testTimeout: 60_000 });

const TYPE_LINE = 'Пользовательская история MSK-US-2';

/** The card in the dialog's frame, as the dialog host hosts it. */
async function openDialog() {
  const onClose = vi.fn<(reason: DialogCloseReason) => void>();
  const onToggleExpanded = vi.fn<() => void>();
  const store = createFrameStore();
  const t = await openCard('MSK-US-2', {
    card: {
      mode: 'dialog',
      renderFrame: (frame: CardFrameProps) => (
        <>
          <PublishFrame store={store} frame={frame} />
          <DialogFrame
            store={store}
            identifier={frame.identifier}
            label={`Рабочий элемент ${frame.identifier}`}
            fallback={null}
            onClose={onClose}
            expanded={false}
            onToggleExpanded={onToggleExpanded}
          />
        </>
      ),
    },
  });
  return { ...t, onClose, onToggleExpanded };
}

const title = held(() => screen.getByRole('textbox', { name: 'Заголовок' }));

describe('feat-029 US-1: the dialog’s shell before a card has published a frame (design § 6)', () => {
  it('is named by its label, holds the fallback, and keeps its two icons', () => {
    render(
      <DialogFrame
        store={createFrameStore()}
        identifier="MSK-US-2"
        label="Рабочий элемент MSK-US-2"
        fallback={<p>Пока ничего</p>}
        onClose={vi.fn()}
        expanded={false}
        onToggleExpanded={vi.fn()}
      />,
    );

    const dialog = screen.getByRole('dialog', { name: 'Рабочий элемент MSK-US-2' });
    expect(within(dialog).getByText('Пока ничего')).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Развернуть на весь экран' })).toBeEnabled();
    expect(within(dialog).getByRole('button', { name: 'Закрыть' })).toBeEnabled();
    expect(within(dialog).queryByRole('heading')).not.toBeInTheDocument();
  });
});

describe('feat-029 US-1, US-2: the card in the dialog’s frame', () => {
  it('is a modal dialog named by the type line, which is its h2', async () => {
    await openDialog();

    const dialog = screen.getByRole('dialog', { name: TYPE_LINE });
    expect(within(dialog).getByRole('heading', { level: 2, name: TYPE_LINE })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Развернуть на весь экран' })).toBeEnabled();
    expect(within(dialog).getByRole('button', { name: 'Закрыть' })).toBeEnabled();
    expect(
      within(dialog).getByRole('tablist', { name: 'Разделы рабочего элемента' }),
    ).toBeVisible();
    expect(within(dialog).getByRole('textbox', { name: 'Описание' })).toBeVisible();
  });

  it('closes at once while nothing is unsaved', async () => {
    const { onClose } = await openDialog();

    await userEvent.click(screen.getByRole('button', { name: 'Закрыть' }));

    expect(onClose).toHaveBeenCalledWith('close');
    expect(
      screen.queryByRole('dialog', { name: 'Закрыть без сохранения?' }),
    ).not.toBeInTheDocument();
  });

  it('asks «Закрыть без сохранения?» first when edits stand, for «Закрыть» and for Esc', async () => {
    const { onClose } = await openDialog();
    typeInto(title(), '!');

    await userEvent.click(screen.getByRole('button', { name: 'Закрыть' }));
    const question = await screen.findByRole('dialog', { name: 'Закрыть без сохранения?' });
    expect(question).toHaveTextContent(
      'Несохранённые изменения рабочего элемента MSK-US-2 будут потеряны.',
    );
    await waitFor(() =>
      expect(
        within(question).getByRole('button', { name: 'Продолжить редактирование' }),
      ).toHaveFocus(),
    );
    expect(onClose).not.toHaveBeenCalled();

    await userEvent.click(
      within(question).getByRole('button', { name: 'Продолжить редактирование' }),
    );
    await waitFor(() =>
      expect(
        screen.queryByRole('dialog', { name: 'Закрыть без сохранения?' }),
      ).not.toBeInTheDocument(),
    );
    expect(title()).toHaveValue('Письмо со ссылкой!');

    await userEvent.keyboard('{Escape}');
    await userEvent.click(await screen.findByRole('button', { name: 'Закрыть без сохранения' }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('expands to the whole window without a question, whatever was touched', async () => {
    const { onToggleExpanded } = await openDialog();
    fill(title(), 'Письмо для входа');

    await userEvent.click(screen.getByRole('button', { name: 'Развернуть на весь экран' }));

    expect(onToggleExpanded).toHaveBeenCalledTimes(1);
    expect(
      screen.queryByRole('dialog', { name: 'Закрыть без сохранения?' }),
    ).not.toBeInTheDocument();
  });

  it('leads the type line to the item’s own page, in a new tab', async () => {
    await openDialog();

    const link = within(screen.getByRole('dialog', { name: TYPE_LINE })).getByRole('link', {
      name: TYPE_LINE,
    });
    expect(link).toHaveAttribute('href', expect.stringMatching(/\/workitems\/MSK-US-2$/));
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', expect.stringContaining('noopener'));
  });
});
