import { Extension, type Editor } from '@tiptap/core';

/** What a notice says: the image that was not pasted, the code that was copied. */
export type NoticeKind = 'image' | 'copied' | 'copyFailed';

/** What the editor's behaviours ask of the component around it. */
export interface EditorBindings {
  /** Open the link prompt (Ctrl/Cmd+K, the toolbar). */
  openLink: () => void;
  /** Say a notice in the live region. */
  notice: (kind: NoticeKind) => void;
  /** Report a change that is still waiting for its delay, now (Tab leaving the body, blur). */
  flush: () => void;
}

/**
 * The seam between the editor's own behaviours (keymap, paste, node views) and
 * the React component around it, which owns the dialog and the live region.
 * The callbacks live in the extension's storage, filled in place by
 * `bindEditor` once the editor exists; until then they do nothing.
 */
export const RtBindings = Extension.create<unknown, EditorBindings>({
  name: 'rtBindings',

  addStorage() {
    return { openLink: () => {}, notice: () => {}, flush: () => {} };
  },
});

export function bindingsOf(editor: Editor): EditorBindings {
  return (editor.storage as unknown as Record<string, EditorBindings>).rtBindings as EditorBindings;
}

export function bindEditor(editor: Editor, bindings: Partial<EditorBindings>): void {
  Object.assign(bindingsOf(editor), bindings);
}
