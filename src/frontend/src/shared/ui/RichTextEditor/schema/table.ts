import { Table, TableCell, TableHeader, TableRow } from '@tiptap/extension-table';
import { Plugin, PluginKey } from '@tiptap/pm/state';
import { Decoration, DecorationSet } from '@tiptap/pm/view';

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

/** The class of the cell that holds the caret (the 2 px frame is the theme's). */
export const ACTIVE_CELL_CLASS = 'rt-cell-active';

const activeCellKey = new PluginKey('rtActiveCell');

export const RtTable = Table.configure({
  resizable: false,
  allowTableNodeSelection: false,
}).extend({
  renderHTML() {
    return ['table', ['tbody', 0]];
  },

  // A scroll wrapper round the table: a table wider than the card scrolls
  // inside itself (ADR-0024 § 5). The wrapper is chrome, never serialised.
  addNodeView() {
    return ({ node: initial }) => {
      const dom = document.createElement('div');
      dom.className = 'rt-table-scroll';
      const table = document.createElement('table');
      const body = document.createElement('tbody');
      table.appendChild(body);
      dom.appendChild(table);
      return {
        dom,
        contentDOM: body,
        update: (updated) => updated.type === initial.type,
      };
    };
  },

  addKeyboardShortcuts() {
    // The stock Tab adds a row at the last cell; the product's Tab never does
    // (BR-RT-17). Tab and Shift+Tab are `behaviour/keyboard.ts`'s alone: a
    // binding here would also take the Tab that Esc armed to leave the table.
    const shortcuts = { ...this.parent?.() };
    delete shortcuts.Tab;
    delete shortcuts['Shift-Tab'];
    return shortcuts;
  },

  addProseMirrorPlugins() {
    return [
      ...(this.parent?.() ?? []),
      // The cell with the caret gets a class, because `:focus-within` cannot name
      // a cell inside one contenteditable (design § Tables).
      new Plugin({
        key: activeCellKey,
        props: {
          decorations: (state) => {
            const { $head } = state.selection;
            for (let depth = $head.depth; depth > 0; depth--) {
              const node = $head.node(depth);
              if (node.type.name === 'tableCell' || node.type.name === 'tableHeader') {
                return DecorationSet.create(state.doc, [
                  Decoration.node($head.before(depth), $head.after(depth), {
                    class: ACTIVE_CELL_CLASS,
                  }),
                ]);
              }
            }
            return DecorationSet.empty;
          },
        },
      }),
    ];
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
