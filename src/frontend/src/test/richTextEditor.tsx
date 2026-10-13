import { useState } from 'react';
import { act, render, screen, within } from '@testing-library/react';
import type { Editor } from '@tiptap/core';
import {
  RichTextEditor,
  type RichTextChange,
  type RichTextEditorHandle,
  type RichTextEditorProps,
} from '../shared/ui/RichTextEditor/RichTextEditor';

/**
 * Helpers of the rich-text editor's component tests (plan-frontend § 12.2): the
 * real editor under a state holder like a form's, the toolbar by names, the
 * live TipTap editor through `onEditorReady` (its types never leave
 * `shared/ui`; tests may touch it), and paste events built by hand because jsdom
 * has no `DataTransfer`.
 */

export interface MountedEditor {
  body: HTMLElement;
  toolbar: HTMLElement;
  editor: () => Editor;
  /** The change reports, in order. */
  changes: RichTextChange[];
  /** The control's button, by its accessible name. */
  button: (name: string) => HTMLElement;
  flush: () => void;
}

export async function mountEditor(
  initial: string,
  props: Partial<RichTextEditorProps> = {},
): Promise<MountedEditor> {
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

  render(<Holder />);
  const body = await screen.findByRole('textbox', { name: 'Описание' });
  const toolbar = screen.getByRole('toolbar', { name: 'Форматирование описания' });
  return {
    body,
    toolbar,
    editor: () => {
      if (!editor) throw new Error('The editor is not mounted.');
      return editor;
    },
    changes,
    button: (name) => within(toolbar).getByRole('button', { name }),
    flush: () => act(() => handle.current?.flush()),
  };
}

/** The position just inside the first text node equal to `text`. */
export function positionOf(editor: Editor, text: string): number {
  let found = -1;
  editor.state.doc.descendants((node, pos) => {
    if (found < 0 && node.isText && node.text === text) found = pos + 1;
  });
  if (found < 0) throw new Error(`No text node ${JSON.stringify(text)} in the document.`);
  return found;
}

export function select(editor: Editor, from: number, to?: number): void {
  act(() => {
    editor.commands.setTextSelection(to === undefined ? from : { from, to });
  });
}

export interface PasteData {
  text?: string;
  html?: string;
  files?: { name: string; type: string }[];
}

/**
 * A `paste` event with a `clipboardData` stub, dispatched on the body. Returns
 * the event, so a test can assert `defaultPrevented`.
 */
export function pasteInto(body: HTMLElement, data: PasteData): Event {
  const types: string[] = [];
  if (data.text !== undefined) types.push('text/plain');
  if (data.html !== undefined) types.push('text/html');
  if (data.files?.length) types.push('Files');
  const clipboardData = {
    types,
    files: data.files ?? [],
    items: [],
    getData: (type: string) =>
      type === 'text/plain' ? (data.text ?? '') : type === 'text/html' ? (data.html ?? '') : '',
  };
  const event = new Event('paste', { bubbles: true, cancelable: true });
  Object.defineProperty(event, 'clipboardData', { value: clipboardData });
  act(() => {
    body.dispatchEvent(event);
  });
  return event;
}

/** A `drop` event carrying files or text, dispatched on the body. */
export function dropOn(body: HTMLElement, data: PasteData): Event {
  const event = new Event('drop', { bubbles: true, cancelable: true });
  const dataTransfer = {
    types: [
      ...(data.text !== undefined ? ['text/plain'] : []),
      ...(data.files?.length ? ['Files'] : []),
    ],
    files: data.files ?? [],
    items: [],
    getData: (type: string) => (type === 'text/plain' ? (data.text ?? '') : ''),
  };
  Object.defineProperty(event, 'dataTransfer', { value: dataTransfer });
  Object.defineProperty(event, 'clientX', { value: 0 });
  Object.defineProperty(event, 'clientY', { value: 0 });
  act(() => {
    body.dispatchEvent(event);
  });
  return event;
}
