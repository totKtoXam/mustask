import { Extension } from '@tiptap/core';
import { closeHistory } from '@tiptap/pm/history';
import type { Node as ProseMirrorNode } from '@tiptap/pm/model';
import { Plugin, PluginKey, type Transaction } from '@tiptap/pm/state';
import type { EditorView } from '@tiptap/pm/view';
import { findAutoLinks } from '../../../lib/richText';

/**
 * The typed and pasted auto-link (BR-RT-18, plan § 7.4). The link is a
 * **separate dispatch** after the typed or pasted content, with the history
 * closed before it, so the first Ctrl/Cmd+Z removes only the link and the next
 * one the typing. (An `appendTransaction` is not used: appended transactions
 * are merged into the original's history event.)
 *
 * Not applied inside inline code, a code block or an existing link, and never
 * to a loaded legacy value (it is not typed).
 */

interface Range {
  from: number;
  to: number;
  href: string;
}

/** The addresses in `[from, to)` of the document, in text that may carry a link. */
export function autoLinkRanges(doc: ProseMirrorNode, from: number, to: number): Range[] {
  const found: Range[] = [];
  doc.nodesBetween(from, to, (node, position, parent) => {
    if (parent?.type.spec.code) return false;
    if (!node.isText || !node.text) return true;
    if (node.marks.some((mark) => mark.type.name === 'code' || mark.type.name === 'link')) {
      return true;
    }
    const start = Math.max(from, position);
    const end = Math.min(to, position + node.nodeSize);
    const text = node.text.slice(start - position, end - position);
    for (const link of findAutoLinks(text)) {
      found.push({ from: start + link.start, to: start + link.end, href: link.href });
    }
    return true;
  });
  return found;
}

function addLinks(view: EditorView, ranges: readonly Range[]): void {
  const type = view.state.schema.marks.link;
  if (!type || ranges.length === 0) return;
  const tr: Transaction = view.state.tr;
  for (const range of ranges) tr.addMark(range.from, range.to, type.create({ href: range.href }));
  closeHistory(tr);
  view.dispatch(tr);
}

/** The views whose next paste is plain (Ctrl/Cmd+Shift+V): no auto-link, no link over a selection. */
const plainNext = new WeakSet<EditorView>();

export const RtAutoLink = Extension.create({
  name: 'rtAutoLink',
  priority: 1000,

  addProseMirrorPlugins() {
    const { editor } = this;
    let busy = false;

    /** Where the token that ends at `position` starts, in the same textblock. */
    const tokenStart = (doc: ProseMirrorNode, position: number): number => {
      const $position = doc.resolve(position);
      const blockStart = $position.start();
      const before = $position.parent.textBetween(0, position - blockStart, undefined, '\n');
      let index = before.length;
      while (index > 0 && !/\s/u.test(before[index - 1] as string)) index--;
      return blockStart + index;
    };

    return [
      new Plugin({
        key: new PluginKey('rtAutoLink'),
        props: {
          handleTextInput(view, from, to, text) {
            if (text !== ' ' || from !== to || busy) return false;
            const $from = view.state.doc.resolve(from);
            if ($from.parent.type.spec.code) return false;

            const start = tokenStart(view.state.doc, from);
            view.dispatch(view.state.tr.insertText(text, from, to));
            addLinks(view, autoLinkRanges(view.state.doc, start, from));
            return true;
          },

          handleKeyDown(view, event) {
            if (event.key === 'v' || event.key === 'V') {
              if ((event.ctrlKey || event.metaKey) && event.shiftKey) plainNext.add(view);
              return false;
            }
            if (event.key !== 'Enter' || event.shiftKey || event.ctrlKey || event.metaKey) {
              return false;
            }
            const { selection } = view.state;
            if (busy || !selection.empty || selection.$from.parent.type.spec.code) return false;

            const caret = selection.from;
            const start = tokenStart(view.state.doc, caret);
            const ranges = autoLinkRanges(view.state.doc, start, caret);
            if (ranges.length === 0) return false;

            // The stock Enter first (list item split, paragraph split...); the link
            // goes onto text that is before the caret, so its positions are unchanged.
            const before = view.state.doc;
            busy = true;
            try {
              editor.commands.keyboardShortcut('Enter');
            } finally {
              busy = false;
            }
            if (view.state.doc.eq(before)) return false;
            addLinks(view, ranges);
            return true;
          },

          handlePaste(view, event) {
            if (busy) return false;
            const wasPlain = plainNext.delete(view);
            const data = event.clipboardData;
            if (wasPlain || !data) return false;
            if (
              Array.from(data.types ?? []).includes('text/html') ||
              (data.files?.length ?? 0) > 0
            ) {
              return false;
            }
            const text = data.getData('text/plain');
            if (!text) return false;

            const { from, to, empty, $from } = view.state.selection;
            if ($from.parent.type.spec.code) return false;
            const type = view.state.schema.marks.link;
            if (!type) return false;

            // A single address over a selection links the selection and keeps its text.
            if (!empty) {
              const trimmed = text.trim();
              const single = findAutoLinks(trimmed);
              const only = single[0];
              if (single.length === 1 && only && only.start === 0 && only.end === trimmed.length) {
                const hasCode = view.state.doc.rangeHasMark(
                  from,
                  to,
                  view.state.schema.marks.code!,
                );
                if (!hasCode) {
                  addLinks(view, [{ from, to, href: only.href }]);
                  return true;
                }
              }
            }

            if (findAutoLinks(text).length === 0) return false;

            // The content goes in by the editor's own paste (one dispatch), the
            // links in a second one, so the first undo takes the links off only.
            busy = true;
            try {
              view.pasteText(text, event);
            } finally {
              busy = false;
            }
            addLinks(view, autoLinkRanges(view.state.doc, from, view.state.selection.to));
            return true;
          },
        },
      }),
    ];
  },
});
