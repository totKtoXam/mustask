import { Table, TableCell, TableHeader, TableRow } from '@tiptap/extension-table';

/**
 * Tables of the subset (BR-RT-17): rectangular, no spans, no widths, no
 * alignment. The stock cell and header attributes (colspan, rowspan, colwidth,
 * align) and their `style`/`colgroup` output are what the canonical form does
 * not have, so they are replaced: `colspan` and `rowspan` stay registered at 1
 * because `prosemirror-tables` reads them, but are never parsed from the
 * markup (the normaliser has already expanded spans) and never rendered;
 * `colwidth` and `align` are not registered. Merge, split and resize
 * commands are never wired (no keymap, no toolbar control).
 */
const spanAttributes = {
  colspan: { default: 1, parseHTML: () => 1, rendered: false },
  rowspan: { default: 1, parseHTML: () => 1, rendered: false },
};

export const RtTable = Table.configure({
  resizable: false,
  allowTableNodeSelection: false,
}).extend({
  renderHTML() {
    return ['table', ['tbody', 0]];
  },

  // The stock node view builds a colgroup; slice 5 supplies the scroll wrapper.
  addNodeView() {
    return null as never;
  },

  addKeyboardShortcuts() {
    return {
      ...this.parent?.(),
      // The stock Tab adds a row at the last cell; the product's Tab never does
      // (BR-RT-17): next cell, or leave the editor.
      Tab: () => this.editor.commands.goToNextCell(),
      'Shift-Tab': () => this.editor.commands.goToPreviousCell(),
    };
  },
});

export const RtTableRow = TableRow;

export const RtTableCell = TableCell.extend({
  content: 'paragraph+',
  addAttributes: () => spanAttributes,
  parseHTML: () => [{ tag: 'td' }],
  renderHTML: () => ['td', 0],
});

export const RtTableHeader = TableHeader.extend({
  content: 'paragraph+',
  addAttributes: () => spanAttributes,
  parseHTML: () => [{ tag: 'th' }],
  renderHTML: () => ['th', 0],
});
