import { Extension, type Editor } from '@tiptap/core';
import { Plugin, PluginKey } from '@tiptap/pm/state';

/** What the editor's behaviours ask of the component around it. */
export interface EditorHooks {
  /** Open the link prompt (Ctrl/Cmd+K). */
  openLink: () => void;
}

/** Connects the editor's Ctrl/Cmd+K to the component's link prompt. */
export function bindOpenLink(editor: Editor, openLink: () => void): void {
  // The extension holds the storage object it created: it is filled in place, never replaced.
  (editor.storage as unknown as Record<string, EditorHooks>).rtKeyboard!.openLink = openLink;
}

/**
 * The product's own keymap, at the top priority so it answers before the stock
 * bindings (plan § 7.3). Slice 4: Ctrl/Cmd+K and the link click.
 */
export const RtKeyboard = Extension.create<unknown, EditorHooks>({
  name: 'rtKeyboard',
  priority: 1000,

  // The component around the editor sets these on `editor.storage.rtKeyboard`
  // once the editor exists.
  addStorage() {
    return { openLink: () => {} };
  },

  addKeyboardShortcuts() {
    return {
      'Mod-k': ({ editor }) => {
        if (editor.state.selection.$from.parent.type.spec.code) return false;
        this.storage.openLink();
        return true;
      },
    };
  },

  addProseMirrorPlugins() {
    return [
      new Plugin({
        key: new PluginKey('rtKeyboardClick'),
        props: {
          // A link opens on purpose, not by click (BR-RT-18): a plain click places
          // the caret; Ctrl/Cmd+click opens it with `noopener,noreferrer`.
          handleClick: (view, position, event) => {
            if (!(event.ctrlKey || event.metaKey)) return false;
            const type = view.state.schema.marks.link;
            if (!type) return false;
            const mark = view.state.doc
              .resolve(position)
              .marks()
              .find((m) => m.type === type);
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
