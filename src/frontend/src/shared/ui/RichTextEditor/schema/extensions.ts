import type { AnyExtension } from '@tiptap/core';
import { Bold } from '@tiptap/extension-bold';
import { Code } from '@tiptap/extension-code';
import { Document } from '@tiptap/extension-document';
import { HardBreak } from '@tiptap/extension-hard-break';
import { Heading } from '@tiptap/extension-heading';
import { Italic } from '@tiptap/extension-italic';
import { Link } from '@tiptap/extension-link';
import { BulletList, ListItem, ListKeymap, OrderedList } from '@tiptap/extension-list';
import { Paragraph } from '@tiptap/extension-paragraph';
import { Strike } from '@tiptap/extension-strike';
import { Text } from '@tiptap/extension-text';
import { Underline } from '@tiptap/extension-underline';
import { Dropcursor, Gapcursor, Placeholder, UndoRedo } from '@tiptap/extensions';
import { readLinkAddress } from '../../../lib/richText';
import { RtAutoLink } from '../behaviour/autoLink';
import { RtBindings } from '../behaviour/bindings';
import { RtCodeHighlight } from '../behaviour/codeHighlight';
import { RtKeyboard } from '../behaviour/keyboard';
import { RtPaste } from '../behaviour/paste';
import { RtCodeBlock, type CodeChrome } from './codeBlock';
import { RtColor, RtHighlight, RtSize } from './rtMarks';
import { RtTable, RtTableCell, RtTableHeader, RtTableRow } from './table';

/** The `rel` the stored link carries (api-contract § 3); the server writes it too. */
const LINK_REL = 'noopener noreferrer';

/**
 * Heading levels 3 and 4 only (BR-RT-4); a heading takes every mark but the
 * size (BR-RT-15), as the server's `Marks.Canonical(inHeading)`.
 */
const RtHeading = Heading.configure({ levels: [3, 4] }).extend({
  marks: 'link bold italic underline strike rtHighlight rtColor code',
});

/** A list has no `start` and no `type`: neither is parsed, neither is rendered (US-7). */
const RtOrderedList = OrderedList.extend({
  addAttributes() {
    return {};
  },
  renderHTML() {
    return ['ol', 0];
  },
});

/**
 * An item holds paragraphs and nested lists only. The stock Tab and Shift-Tab
 * are removed: Tab never indents (BR-RT-20); indent and outdent are `Mod-]`,
 * `Mod-[` (the product's keymap, `behaviour/keyboard.ts`, with the depth
 * guards) and the toolbar's two buttons.
 */
const RtListItem = ListItem.extend({
  content: 'paragraph (paragraph | bulletList | orderedList)*',
  addKeyboardShortcuts() {
    return {
      Enter: () => this.editor.commands.splitListItem(this.name),
    };
  },
});

/** Backspace and Delete in lists stay; the stock keymap's Tab is removed (BR-RT-20). */
const RtListKeymap = ListKeymap.extend({
  addKeyboardShortcuts() {
    const shortcuts = { ...this.parent?.() };
    delete shortcuts.Tab;
    return shortcuts;
  },
});

/**
 * A link carries an address that passed the server's rule and a fixed `rel`;
 * it is never opened by a click, never auto-detected by the stock plugins (the
 * product's own auto-link is `behaviour/autoLink.ts`, slice 4), and has no
 * `target`, `class` or `title` (BR-RT-18).
 */
const RtLink = Link.configure({
  openOnClick: false,
  autolink: false,
  linkOnPaste: false,
  isAllowedUri: (address) => readLinkAddress(address) !== null,
}).extend({
  addAttributes() {
    return {
      href: {
        default: null,
        parseHTML: (element: HTMLElement) => readLinkAddress(element.getAttribute('href')),
      },
    };
  },
  renderHTML({ HTMLAttributes }) {
    return ['a', { href: HTMLAttributes.href as string, rel: LINK_REL }, 0];
  },
  addPasteRules() {
    return [];
  },
});

/**
 * Mark input rules are off (`**`, `*`, `_`, `~~`): the design's D-4 keeps only
 * the block rules and the backticks (F-12). Paste rules are off too: a paste is
 * the product's own pipeline (slice 5).
 */
const noRules = { addInputRules: () => [], addPasteRules: () => [] };

const RtBold = Bold.extend(noRules);
const RtItalic = Italic.extend(noRules);
const RtStrike = Strike.extend(noRules);

/**
 * Inline code keeps only a link (ADR-0037 decision 7): it excludes every mark
 * but link, and link does not exclude code. ProseMirror's own check then makes
 * "code wins" in both directions.
 */
const RtCode = Code.extend({
  excludes: 'bold italic underline strike rtHighlight rtColor rtSize',
  addPasteRules: () => [],
});

/**
 * The editor's explicit extension list (F-11), not `StarterKit`: the starter
 * set also brings link, underline, a trailing node and list keymaps with
 * their own Tab bindings, which BR-RT-20 and ADR-0037 decision 7 switch off.
 * Not registered, so no shortcut or rule can create them: blockquote,
 * horizontal rule, task list, text align, trailing node, image
 * (`extensions.test.ts` pins the list).
 *
 * Mark priority is the canonical nesting: link 1000, bold 900, italic 800,
 * underline 700, strike 600, highlight 500, colour 400, size 300, code 200.
 * `placeholder` is the text shown while the document is empty, fixed at mount.
 */
export function createExtensions(placeholder: string, chrome?: CodeChrome): AnyExtension[] {
  return [
    Document,
    Paragraph,
    Text,
    HardBreak.configure({ keepMarks: true }),
    RtHeading,
    BulletList,
    RtOrderedList,
    RtListItem,
    RtListKeymap,
    RtLink.extend({ priority: 1000 }),
    RtBold.extend({ priority: 900 }),
    RtItalic.extend({ priority: 800 }),
    Underline.extend({ priority: 700 }),
    RtStrike.extend({ priority: 600 }),
    RtHighlight,
    RtColor,
    RtSize,
    RtCode.extend({ priority: 200 }),
    chrome ? RtCodeBlock.configure({ chrome }) : RtCodeBlock,
    RtTable,
    RtTableRow,
    RtTableCell,
    RtTableHeader,
    UndoRedo,
    Gapcursor,
    Dropcursor,
    Placeholder.configure({
      placeholder,
      showOnlyWhenEditable: true,
    }),
    RtBindings,
    RtKeyboard,
    RtAutoLink,
    RtPaste,
    RtCodeHighlight,
  ];
}

/** The names the editor registers, for the test that pins the list. */
export const EXTENSION_NAMES = [
  'doc',
  'paragraph',
  'text',
  'hardBreak',
  'heading',
  'bulletList',
  'orderedList',
  'listItem',
  'listKeymap',
  // Added by the list nodes: Delete in a branching nested list. It binds no Tab.
  'listItemBranchingDeleteKeymap',
  'link',
  'bold',
  'italic',
  'underline',
  'strike',
  'rtHighlight',
  'rtColor',
  'rtSize',
  'code',
  'codeBlock',
  'table',
  'tableRow',
  'tableCell',
  'tableHeader',
  'undoRedo',
  'gapCursor',
  'dropCursor',
  'placeholder',
  'rtBindings',
  'rtKeyboard',
  'rtAutoLink',
  'rtPaste',
  'rtCodeHighlight',
] as const;
