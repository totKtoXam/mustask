import { Extension } from '@tiptap/core';
import {
  Fragment,
  Slice,
  type Node as ProseMirrorNode,
  type ResolvedPos,
  type Schema,
} from '@tiptap/pm/model';
import { Plugin, PluginKey } from '@tiptap/pm/state';
import { normaliseBody, reducePastedHtml, tabularRows } from '../../../lib/richText';
import { bindingsOf } from './bindings';

/**
 * The paste and drop pipeline (BR-RT-5, plan-frontend § 7.5, § 7.6):
 *
 * - html: `reducePastedHtml` (style read into marks, IDE code recognised, an
 *   image noticed) then `normaliseBody` — the server's own rules, F-1 — before
 *   the schema parses it. So merged cells, widths, foreign colours and sizes,
 *   lists in cells and images never reach the parser.
 * - plain text: one paragraph per line, blank lines kept (F-7); tab-separated
 *   lines of one shape from a spreadsheet are a table (no header row), not with
 *   Ctrl+Shift+V and not inside a table, a list item or a code block.
 * - inside a cell or a list item the pasted tables and code blocks become
 *   paragraphs, so `prosemirror-tables`' own paste (which overwrites cell
 *   ranges) has nothing of its kind to handle.
 * - a file or an image on its own is not inserted: the member is told in the
 *   live region, and the browser never navigates to it.
 */

function ancestors($position: ResolvedPos): Set<string> {
  const names = new Set<string>();
  for (let depth = $position.depth; depth > 0; depth--) names.add($position.node(depth).type.name);
  return names;
}

function paragraphOf(schema: Schema, text: string): ProseMirrorNode {
  return schema.nodes.paragraph!.create(null, text.length > 0 ? schema.text(text) : null);
}

function tableOf(schema: Schema, rows: readonly (readonly string[])[]): ProseMirrorNode {
  const { table, tableRow, tableCell } = schema.nodes;
  return table!.create(
    null,
    rows.map((cells) =>
      tableRow!.create(
        null,
        cells.map((cell) => tableCell!.create(null, paragraphOf(schema, cell))),
      ),
    ),
  );
}

/** The slice's blocks as paragraphs: text blocks keep their text, code blocks one paragraph per line. */
function flatten(schema: Schema, fragment: Fragment, keepLists: boolean): ProseMirrorNode[] {
  const result: ProseMirrorNode[] = [];
  fragment.forEach((node) => {
    const name = node.type.name;
    if (node.isInline) {
      result.push(node);
    } else if (name === 'paragraph') {
      result.push(node);
    } else if (name === 'heading') {
      result.push(schema.nodes.paragraph!.create(null, node.content));
    } else if (name === 'codeBlock') {
      for (const line of node.textContent.split('\n')) result.push(paragraphOf(schema, line));
    } else if (keepLists && (name === 'bulletList' || name === 'orderedList')) {
      result.push(node);
    } else {
      result.push(...flatten(schema, node.content, keepLists));
    }
  });
  return result;
}

export const RtPaste = Extension.create({
  name: 'rtPaste',
  priority: 1000,

  addProseMirrorPlugins() {
    const { editor } = this;

    return [
      new Plugin({
        key: new PluginKey('rtPaste'),
        props: {
          transformPastedHTML: (html) => {
            const reduced = reducePastedHtml(html);
            if (!reduced) return '';
            if (reduced.hadImage) bindingsOf(editor).notice('image');
            return normaliseBody(reduced.body).html;
          },

          clipboardTextParser: (text, $context, plain, view) => {
            const { schema } = view.state;
            const inside = ancestors($context);
            const rows = plain ? null : tabularRows(text);
            if (
              rows &&
              !inside.has('table') &&
              !inside.has('bulletList') &&
              !inside.has('orderedList')
            ) {
              return new Slice(Fragment.from(tableOf(schema, rows)), 0, 0);
            }
            const lines = text.split(/\r\n|\r|\n/);
            return new Slice(Fragment.from(lines.map((line) => paragraphOf(schema, line))), 1, 1);
          },

          transformPasted: (slice, view) => {
            const inside = ancestors(view.state.selection.$from);
            const inCell = inside.has('tableCell') || inside.has('tableHeader');
            const inItem = inside.has('listItem');
            if (!inCell && !inItem) return slice;

            const blocks = flatten(view.state.schema, slice.content, !inCell);
            if (blocks.length === 0) return slice;
            return new Slice(
              Fragment.from(blocks),
              Math.min(slice.openStart, 1),
              Math.min(slice.openEnd, 1),
            );
          },

          handlePaste: (_view, event) => {
            const data = event.clipboardData;
            if (!data) return false;
            const hasFiles = (data.files?.length ?? 0) > 0;
            const types = Array.from(data.types ?? []);
            const hasText = types.includes('text/html') || types.includes('text/plain');
            if (hasFiles && !hasText) {
              event.preventDefault();
              bindingsOf(editor).notice('image');
              return true;
            }
            return false;
          },

          handleDOMEvents: {
            // A dropped file: the default would navigate the tab to it. Text and
            // html drops go through the editor's own drop, and the same pipeline.
            drop: (_view, event) => {
              const files = (event as DragEvent).dataTransfer?.files;
              if (!files || files.length === 0) return false;
              event.preventDefault();
              bindingsOf(editor).notice('image');
              return true;
            },
          },
        },
      }),
    ];
  },
});
