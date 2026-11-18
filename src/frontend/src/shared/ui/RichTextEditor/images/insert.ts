import type { Editor } from '@tiptap/core';
import type { Node as ProseMirrorNode } from '@tiptap/pm/model';
import { NodeSelection, Plugin, PluginKey, Selection, type Transaction } from '@tiptap/pm/state';

/** An invisible position between blocks, mapped through edits (design § Queue, D-9). Not a node. */
export const slotsKey = new PluginKey<Map<string, number>>('rtImageSlots');

interface SlotMeta {
  add?: [string, number][];
  remove?: string;
}

/** The plugin that keeps the slots: each position is mapped through every transaction, staying left. */
export function slotsPlugin(): Plugin<Map<string, number>> {
  return new Plugin<Map<string, number>>({
    key: slotsKey,
    state: {
      init: () => new Map(),
      apply(tr, slots) {
        const meta = tr.getMeta(slotsKey) as SlotMeta | undefined;
        if (slots.size === 0 && !meta?.add) return slots;
        const next = new Map<string, number>();
        for (const [id, pos] of slots) {
          if (meta?.remove === id) continue;
          next.set(id, tr.mapping.map(pos, -1));
        }
        for (const [id, pos] of meta?.add ?? []) next.set(id, pos);
        return next;
      },
    },
  });
}

export function slotPosition(editor: Editor, id: string): number | null {
  return slotsKey.getState(editor.state)?.get(id) ?? null;
}

export function dropSlot(editor: Editor, id: string): void {
  editor.view.dispatch(
    editor.state.tr
      .setMeta(slotsKey, { remove: id } satisfies SlotMeta)
      .setMeta('addToHistory', false),
  );
}

/** A piece of an insert: a node, or a slot held for an image that is not ready (US-6 queue). */
export type Piece = { node: ProseMirrorNode } | { slot: string };

interface Point {
  from: number;
  to: number;
  before: ProseMirrorNode | null;
  after: ProseMirrorNode | null;
}

const BLOCK_PARENTS = new Set(['doc', 'listItem']);

/**
 * BR-IM-13, made visible (design § Paste, drop and the file chooser): a caret in a paragraph splits it
 * (an empty half is not kept); in a heading, a table cell or a code block the insert lands right
 * after the block; in a list item it is a block of its own in that item.
 */
export function insertionPoint(doc: ProseMirrorNode, pos: number): Point {
  const $pos = doc.resolve(pos);
  if (BLOCK_PARENTS.has($pos.parent.type.name)) {
    return { from: pos, to: pos, before: null, after: null };
  }
  for (let depth = $pos.depth; depth >= 1; depth--) {
    if (!BLOCK_PARENTS.has($pos.node(depth - 1).type.name)) continue;
    const block = $pos.node(depth);
    if (block.type.name === 'paragraph') {
      const offset = pos - $pos.start(depth);
      return {
        from: $pos.before(depth),
        to: $pos.after(depth),
        before: offset > 0 ? block.copy(block.content.cut(0, offset)) : null,
        after: offset < block.content.size ? block.copy(block.content.cut(offset)) : null,
      };
    }
    return { from: $pos.after(depth), to: $pos.after(depth), before: null, after: null };
  }
  return { from: pos, to: pos, before: null, after: null };
}

/**
 * Inserts the pieces consecutively at `pos` (the selection when omitted; a range selection is
 * replaced first). Slots become positions between the nodes. Returns the transaction's end position.
 */
export function insertPieces(editor: Editor, pieces: Piece[], pos?: number): void {
  const { state, view } = editor;
  const tr: Transaction = state.tr;
  let at = pos ?? state.selection.from;
  if (pos === undefined && !state.selection.empty && !(state.selection instanceof NodeSelection)) {
    tr.deleteSelection();
    at = tr.selection.from;
  } else if (pos === undefined && state.selection instanceof NodeSelection) {
    at = state.selection.to;
  }
  at = Math.min(at, tr.doc.content.size);

  const point = insertionPoint(tr.doc, at);
  const nodes: ProseMirrorNode[] = [];
  const slots: [string, number][] = [];
  let offset = point.from + (point.before?.nodeSize ?? 0);
  if (point.before) nodes.push(point.before);
  for (const piece of pieces) {
    if ('slot' in piece) {
      slots.push([piece.slot, offset]);
    } else {
      nodes.push(piece.node);
      offset += piece.node.nodeSize;
    }
  }
  if (point.after) nodes.push(point.after);
  const insertedEnd = offset;

  if (nodes.length > 0) tr.replaceWith(point.from, point.to, nodes);
  if (slots.length > 0) tr.setMeta(slotsKey, { add: slots } satisfies SlotMeta);
  if (nodes.length > 0) {
    // The caret goes after the last inserted block, into the text that follows when there is one.
    const $end = tr.doc.resolve(Math.min(insertedEnd, tr.doc.content.size));
    tr.setSelection(Selection.findFrom($end, 1, true) ?? Selection.near($end, 1));
  }
  view.dispatch(tr.scrollIntoView());
}

/** The position of the node (placeholder or image) a predicate picks, or -1. */
export function findNode(
  doc: ProseMirrorNode,
  predicate: (node: ProseMirrorNode) => boolean,
): number {
  let found = -1;
  doc.descendants((node, pos) => {
    if (found >= 0) return false;
    if (predicate(node)) found = pos;
    return found < 0;
  });
  return found;
}

/** How many image blocks and placeholders the document holds (BR-IM-9's count, design § Count). */
export function countImages(doc: ProseMirrorNode): number {
  let count = 0;
  doc.descendants((node) => {
    if (node.type.name === 'rtImage' || node.type.name === 'rtImagePlaceholder') count++;
    return true;
  });
  return count;
}
