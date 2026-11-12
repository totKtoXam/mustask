import type { Editor } from '@tiptap/core';

/** What the link prompt opens with (design § Links, BR-RT-18). */
export interface LinkSeed {
  /** The address of the link at the caret or selection, else `''`. */
  address: string;
  /** «Текст ссылки» is shown: nothing is selected and the caret is not in a link. */
  askText: boolean;
}

export function linkSeed(editor: Editor): LinkSeed {
  const inLink = editor.isActive('link');
  const address = inLink ? ((editor.getAttributes('link').href as string | undefined) ?? '') : '';
  return { address, askText: !inLink && editor.state.selection.empty };
}

/**
 * Applies the prompt's answer; `address` has passed `promptAddress`. A
 * selection becomes a link and keeps its text, nothing selected inserts the
 * text (the address when the text is empty) as a linked run, a caret in a link
 * changes that link's address. `setLink` checks the address once more.
 */
export function applyLink(editor: Editor, address: string, text: string): boolean {
  if (editor.isActive('link')) {
    return editor.chain().focus().extendMarkRange('link').setLink({ href: address }).run();
  }
  if (!editor.state.selection.empty) {
    return editor.chain().focus().setLink({ href: address }).run();
  }
  return editor
    .chain()
    .focus()
    .insertContent({
      type: 'text',
      text: text.length > 0 ? text : address,
      marks: [{ type: 'link', attrs: { href: address } }],
    })
    .run();
}
