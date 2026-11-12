import { describe, expect, it, vi } from 'vitest';
import { act, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { DialogCloseReason } from '../../../../shared/ui';
import { pasteInto } from '../../../../test/richTextEditor';
import type { CardFrameProps } from './cardFrame';
import { createFrameStore } from './frameStore';
import { DialogFrame, PublishFrame } from './KitFrames';
import { findDescription, openCard, stored } from './testCard';

vi.setConfig({ testTimeout: 60_000 });

const TYPE_LINE = 'Пользовательская история MSK-US-2';

/** The card in the dialog's frame with `html` stored as «Описание». */
async function openWithDescription(html: string) {
  const onClose = vi.fn<(reason: DialogCloseReason) => void>();
  const store = createFrameStore();
  const t = await openCard('MSK-US-2', {
    prepare: (api) => {
      const record = stored(api, 2);
      record.description = html;
      record.descriptionFormat = 'html';
    },
    card: {
      mode: 'dialog',
      renderFrame: (frame: CardFrameProps) => (
        <>
          <PublishFrame store={store} frame={frame} />
          <DialogFrame
            store={store}
            identifier={frame.identifier}
            label="Рабочий элемент"
            fallback={null}
            onClose={onClose}
            expanded={false}
            onToggleExpanded={vi.fn()}
          />
        </>
      ),
    },
  });
  return { ...t, onClose };
}

/** As above, with the caret placed in the block that holds `text`. */
async function openAt(html: string, text: string) {
  const opened = await openWithDescription(html);
  const body = await findDescription();
  await userEvent.click(body);
  // jsdom does not place a caret on a click: set the DOM selection and let the editor read it.
  const node = within(body).getByText(text).firstChild as Text;
  act(() => {
    document.getSelection()?.setBaseAndExtent(node, 1, node, 1);
    document.dispatchEvent(new Event('selectionchange'));
  });
  return { ...opened, body };
}

describe('feat-018 CR-1: Esc in the description editor inside the card dialog', () => {
  it('in a paragraph Esc reaches the dialog, which closes while nothing is unsaved', async () => {
    const { onClose } = await openAt('<p>Текст</p>', 'Текст');

    await userEvent.keyboard('{Escape}');

    expect(onClose).toHaveBeenCalledWith('escape');
  });

  it('in a code block the first Esc is the editor’s and the second closes the dialog', async () => {
    const { onClose } = await openAt('<pre><code>код</code></pre>', 'код');

    await userEvent.keyboard('{Escape}');
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByRole('dialog', { name: TYPE_LINE })).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledWith('escape');
  });

  it('in a code block Esc then Tab leaves the block and the dialog stays open', async () => {
    const { onClose, body } = await openAt('<pre><code>код</code></pre>', 'код');

    await userEvent.keyboard('{Escape}{Tab}');

    expect(onClose).not.toHaveBeenCalled();
    expect(body).not.toHaveFocus();
    expect(screen.getByRole('dialog', { name: TYPE_LINE })).toBeInTheDocument();
  });

  it('in a table cell the first Esc is the editor’s too', async () => {
    const { onClose } = await openAt(
      '<table><tbody><tr><td><p>яч</p></td></tr></tbody></table>',
      'яч',
    );

    await userEvent.keyboard('{Escape}');

    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByRole('dialog', { name: TYPE_LINE })).toBeInTheDocument();
  });

  it('typing then Esc within the report delay still asks before closing', async () => {
    const { onClose, body } = await openAt('<p>Текст</p>', 'Текст');
    pasteInto(body, { text: ' новое' });

    // No wait: the editor's 150 ms report has not fired, so the card's state is still clean.
    await userEvent.keyboard('{Escape}');

    expect(
      await screen.findByRole('dialog', { name: 'Закрыть без сохранения?' }),
    ).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });
});
