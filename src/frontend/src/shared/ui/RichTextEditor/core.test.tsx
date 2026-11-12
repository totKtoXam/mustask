import { useState } from 'react';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Editor } from '@tiptap/core';
import { describe, expect, it } from 'vitest';
import { REPORT_DELAY_MS } from './EditorImpl';
import { RichTextEditor, type RichTextChange, type RichTextEditorHandle } from './RichTextEditor';

interface Harness {
  editor: () => Editor;
  changes: RichTextChange[];
  handle: { current: RichTextEditorHandle | null };
}

/** Mounts the editor under a state holder, like a form does, and waits for the lazy chunk. */
async function mount(initial: string, props: Partial<Parameters<typeof RichTextEditor>[0]> = {}) {
  const changes: RichTextChange[] = [];
  const handle: { current: RichTextEditorHandle | null } = { current: null };
  let editor: Editor | null = null;

  function Holder() {
    const [value, setValue] = useState(initial);
    return (
      <RichTextEditor
        label="Описание"
        value={value}
        ref={handle}
        onChange={(change) => {
          changes.push(change);
          setValue(change.html);
        }}
        onEditorReady={(ready) => {
          editor = ready;
        }}
        {...props}
      />
    );
  }

  const view = render(<Holder />);
  await screen.findByRole('textbox', { name: 'Описание' });
  const harness: Harness = {
    editor: () => {
      if (!editor) throw new Error('The editor is not mounted.');
      return editor;
    },
    changes,
    handle,
  };
  return { ...view, ...harness };
}

describe('RichTextEditor core', () => {
  it('shows the stored value in a textbox named by the label', async () => {
    await mount('<p>Первая</p><p>Вторая</p>');

    const body = screen.getByRole('textbox', { name: 'Описание' });
    expect(body).toHaveTextContent('Первая');
    expect(body).toHaveTextContent('Вторая');
    expect(body).toHaveAttribute('aria-multiline', 'true');
  });

  it('reports a typed change after its delay, as the canonical string with its counts', async () => {
    const user = userEvent.setup();
    const { changes, editor } = await mount('');

    await user.click(screen.getByRole('textbox', { name: 'Описание' }));
    await user.keyboard('Привет');
    expect(editor().getHTML()).toBe('<p>Привет</p>');
    expect(changes).toEqual([]);

    await waitFor(() => expect(changes).toHaveLength(1), { timeout: REPORT_DELAY_MS * 10 });
    expect(changes).toEqual([{ html: '<p>Привет</p>', visibleLength: 6, markupLength: 13 }]);
  });

  it('flush() reports at once, so a save does not wait for the delay', async () => {
    const { changes, editor, handle } = await mount('<p>a</p>');

    await act(async () => {
      editor().commands.focus('end');
      editor().commands.insertContent('b');
    });
    expect(changes).toEqual([]);

    act(() => handle.current?.flush());

    expect(changes).toEqual([{ html: '<p>ab</p>', visibleLength: 2, markupLength: 9 }]);
  });

  it('reports the baseline string itself when the document is edited back to the loaded one', async () => {
    // Stored markup that is not the editor's own serialisation: the baseline is reported as it was given.
    const stored = '<ul><li>одно</li></ul>';
    const { changes, editor, handle } = await mount(stored);

    await act(async () => {
      editor().commands.focus('end');
      editor().commands.insertContent('!');
    });
    act(() => handle.current?.flush());
    expect(changes.at(-1)?.html).toBe('<ul><li><p>одно!</p></li></ul>');

    await act(async () => {
      editor().commands.undo();
    });
    act(() => handle.current?.flush());

    expect(changes.at(-1)?.html).toBe(stored);
  });

  it('disables editing while saving, and every toolbar control', async () => {
    const { rerender } = await mount('<p>a</p>', { disabled: true });
    const body = screen.getByRole('textbox', { name: 'Описание' });

    expect(body).toHaveAttribute('contenteditable', 'false');
    expect(body).toHaveAttribute('aria-disabled', 'true');
    for (const button of screen.getAllByRole('button')) {
      expect(button).toBeDisabled();
    }
    expect(rerender).toBeTypeOf('function');
  });

  it('waits for its echo: a value the document already holds is not a replacement', async () => {
    const { changes, editor, handle } = await mount('<p>a</p>');
    const before = editor();

    await act(async () => {
      editor().commands.focus('end');
      editor().commands.insertContent('b');
    });
    act(() => handle.current?.flush());

    // The holder echoed the reported html back as `value`: the same editor stays.
    await waitFor(() => expect(changes).toHaveLength(1));
    expect(editor()).toBe(before);
    expect(editor().getText()).toBe('ab');
  });
});
