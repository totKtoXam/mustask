import { Extension, type Editor } from '@tiptap/core';
import { GapCursor } from '@tiptap/pm/gapcursor';
import type { ResolvedPos } from '@tiptap/pm/model';
import { Plugin, PluginKey, Selection } from '@tiptap/pm/state';
import { goToNextCell } from '@tiptap/pm/tables';
import type { EditorView } from '@tiptap/pm/view';
import { bindingsOf } from './bindings';

/** The most nested levels of a list (BR-RT-20). */
const MAX_LIST_DEPTH = 3;
const MODIFIER_KEYS = new Set(['Shift', 'Control', 'Alt', 'Meta', 'CapsLock']);

function listDepth($position: ResolvedPos): number {
  let depth = 0;
  for (let level = $position.depth; level > 0; level--) {
    const name = $position.node(level).type.name;
    if (name === 'bulletList' || name === 'orderedList') depth++;
  }
  return depth;
}

function inCell($position: ResolvedPos): boolean {
  for (let level = $position.depth; level > 0; level--) {
    const name = $position.node(level).type.name;
    if (name === 'tableCell' || name === 'tableHeader') return true;
  }
  return false;
}

/**
 * Leaves the top-level block that holds the selection (a code block or a
 * table): the caret goes to the start of the next block, or, when it is the
 * document's last, a **gap cursor** after it. No transaction touches the
 * document, so merely moving through a final block is not a change (US-3,
 * US-6): the paragraph appears only when the member types at the gap cursor.
 */
export function exitBlock(view: EditorView): void {
  const { doc, selection } = view.state;
  const $from = selection.$from;
  if ($from.depth < 1) return;
  const after = $from.after(1);
  const next =
    after < doc.content.size
      ? Selection.near(doc.resolve(after), 1)
      : new GapCursor(doc.resolve(doc.content.size));
  view.dispatch(view.state.tr.setSelection(next).scrollIntoView());
}

/** Line starts, as offsets into `text`, of every line the range `[a, b]` touches. */
function linesOf(text: string, a: number, b: number): number[] {
  const starts: number[] = [];
  let position = text.lastIndexOf('\n', a - 1) + 1;
  for (;;) {
    starts.push(position);
    const next = text.indexOf('\n', position);
    // The next line counts only if it starts before the selection's end.
    if (next < 0 || next + 1 >= Math.max(b, a + 1)) break;
    position = next + 1;
  }
  return starts;
}

/**
 * Tab in a code block (BR-RT-16, F-8): a tab at the caret; with a selection a
 * tab at the start of each selected line. Shift+Tab removes one leading tab,
 * else up to four leading spaces, from every selected line (the caret's own
 * line with no selection).
 */
function indentCode(view: EditorView, outdent: boolean): void {
  const { state } = view;
  const { $from, $to, empty } = state.selection;
  const start = $from.start();
  const text = $from.parent.textContent;

  if (!outdent && empty) {
    view.dispatch(state.tr.insertText('\t').scrollIntoView());
    return;
  }
  if (!$from.sameParent($to)) return;

  const a = $from.parentOffset;
  const b = $to.parentOffset;
  const lines = linesOf(text, a, b);
  const tr = state.tr;

  // From the last line back, so earlier positions stay valid.
  for (const line of [...lines].reverse()) {
    if (!outdent) {
      tr.insertText('\t', start + line);
      continue;
    }
    let remove = 0;
    if (text[line] === '\t') remove = 1;
    else while (remove < 4 && text[line + remove] === ' ') remove++;
    if (remove > 0) tr.delete(start + line, start + line + remove);
  }
  if (tr.docChanged) view.dispatch(tr.scrollIntoView());
}

/**
 * Enter in a code block (BR-RT-16, F-9): a line feed; at the end of the block,
 * with the text already ending in one, the trailing line feeds are removed and
 * the block is left.
 */
function enterInCode(view: EditorView): void {
  const { state } = view;
  const { $from, empty } = state.selection;
  const text = $from.parent.textContent;

  if (empty && $from.parentOffset === text.length && text.endsWith('\n')) {
    let count = 0;
    while (text[text.length - 1 - count] === '\n') count++;
    const end = $from.end();
    view.dispatch(state.tr.delete(end - count, end));
    exitBlock(view);
    return;
  }
  view.dispatch(state.tr.insertText('\n').scrollIntoView());
}

/**
 * The product's own keymap in one handler at the top priority, so it answers
 * before every stock binding and the order inside it is explicit (plan § 7.3).
 *
 * Esc arms exactly the next keystroke (BR-RT-16): an armed Tab or Shift+Tab is
 * not taken by a code block or a table, so the browser moves focus out. Any
 * other key disarms (a lone modifier does not: Esc, then Shift+Tab). An Esc
 * that closes a menu, the emoji popover or the dialog never reaches the
 * editor's DOM, so it cannot arm.
 *
 * Tab is never an indent in the body and never captured in a list item
 * (BR-RT-20); it is the tab character in a code block and the next cell in a
 * table, whose last cell leaves the editor without adding a row.
 */
export const RtKeyboard = Extension.create({
  name: 'rtKeyboard',
  priority: 1000,

  addProseMirrorPlugins() {
    const { editor } = this;
    let armed = false;

    return [
      new Plugin({
        key: new PluginKey('rtKeyboard'),
        props: {
          handleKeyDown: (view, event) => {
            if (MODIFIER_KEYS.has(event.key)) return false;
            if (event.key === 'Escape') {
              armed = true;
              return false;
            }
            const wasArmed = armed;
            armed = false;
            return handleKey(editor, view, event, wasArmed);
          },

          // A link opens on purpose, not by click (BR-RT-18): a plain click places
          // the caret; Ctrl/Cmd+click opens it with `noopener,noreferrer`.
          handleClick: (view, position, event) => {
            if (!(event.ctrlKey || event.metaKey)) return false;
            const type = view.state.schema.marks.link;
            if (!type) return false;
            const mark = view.state.doc
              .resolve(position)
              .marks()
              .find((candidate) => candidate.type === type);
            const href = mark?.attrs.href as string | undefined;
            if (!href) return false;
            window.open(href, '_blank', 'noopener,noreferrer');
            return true;
          },
        },
      }),
    ];
  },
});

function handleKey(
  editor: Editor,
  view: EditorView,
  event: KeyboardEvent,
  wasArmed: boolean,
): boolean {
  if (!editor.isEditable) return false;
  const { state } = view;
  const { $from, empty } = state.selection;
  const mod = event.ctrlKey || event.metaKey;
  const inCode = $from.parent.type.name === 'codeBlock';
  const cell = inCell($from);

  switch (event.key) {
    case 'Tab': {
      if (wasArmed || event.altKey || mod) return false;
      if (inCode) {
        indentCode(view, event.shiftKey);
        return true;
      }
      if (cell) return goToNextCell(event.shiftKey ? -1 : 1)(state, view.dispatch);
      return false;
    }
    case 'Enter': {
      if (mod && !event.shiftKey && !event.altKey && (inCode || cell)) {
        exitBlock(view);
        return true;
      }
      if (inCode && !mod && !event.shiftKey && !event.altKey) {
        enterInCode(view);
        return true;
      }
      return false;
    }
    case 'ArrowDown': {
      if (!inCode || mod || event.shiftKey || event.altKey || !empty) return false;
      if ($from.parentOffset !== $from.parent.content.size) return false;
      exitBlock(view);
      return true;
    }
    case 'k':
    case 'K': {
      if (!mod || event.shiftKey || event.altKey || inCode) return false;
      bindingsOf(editor).openLink();
      return true;
    }
    case ']': {
      if (!mod) return false;
      const depth = listDepth($from);
      if (depth === 0) return false;
      if (depth < MAX_LIST_DEPTH) editor.commands.sinkListItem('listItem');
      return true;
    }
    case '[': {
      if (!mod) return false;
      const depth = listDepth($from);
      if (depth === 0) return false;
      if (depth > 1) editor.commands.liftListItem('listItem');
      return true;
    }
    default:
      return false;
  }
}
