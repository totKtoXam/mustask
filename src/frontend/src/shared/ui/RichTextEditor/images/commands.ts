import type { Editor } from '@tiptap/core';
import type { ImageRuntime } from './runtime';

/** Removes the image or placeholder at `pos`; the caret goes to the neighbour (design § Keyboard). */
export function removeImageAt(editor: Editor, pos: number, runtime: ImageRuntime): void {
  const node = editor.state.doc.nodeAt(pos);
  if (!node) return;
  editor
    .chain()
    .focus()
    .deleteRange({ from: pos, to: pos + node.nodeSize })
    .run();
  runtime.ui.notice(runtime.copy.removed);
}
