import type { Editor } from '@tiptap/core';

/**
 * «Очистить форматирование» (US-5): every mark but the link is removed over the
 * selection, a heading becomes a paragraph, and lists and tables stay as they
 * are. With a caret and no selection the marks that would be typed are cleared
 * and the heading under the caret becomes a paragraph. One undo step.
 */
export function clearFormatting(editor: Editor): boolean {
  return editor
    .chain()
    .command(({ tr, state }) => {
      const { from, to, empty, $from } = state.selection;
      const paragraph = state.schema.nodes.paragraph;
      if (!paragraph) return false;

      const clearable = Object.values(state.schema.marks).filter((mark) => mark.name !== 'link');

      if (empty) {
        const active = state.storedMarks ?? $from.marks();
        tr.setStoredMarks(active.filter((mark) => mark.type.name === 'link'));
      } else {
        for (const mark of clearable) tr.removeMark(from, to, mark);
      }

      state.doc.nodesBetween(from, to, (node, pos) => {
        if (node.type.name === 'heading') tr.setNodeMarkup(pos, paragraph);
      });
      return true;
    })
    .run();
}
