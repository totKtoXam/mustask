import type { SvgIconComponent } from '@mui/icons-material';
import FormatBoldIcon from '@mui/icons-material/FormatBold';
import FormatIndentDecreaseIcon from '@mui/icons-material/FormatIndentDecrease';
import FormatIndentIncreaseIcon from '@mui/icons-material/FormatIndentIncrease';
import FormatItalicIcon from '@mui/icons-material/FormatItalic';
import FormatListBulletedIcon from '@mui/icons-material/FormatListBulleted';
import FormatListNumberedIcon from '@mui/icons-material/FormatListNumbered';
import RedoIcon from '@mui/icons-material/Redo';
import UndoIcon from '@mui/icons-material/Undo';
import type { Editor } from '@tiptap/core';
import type { RichTextCopy } from '../copy';

/**
 * The toolbar's data (design § Toolbar: "control list is data, an ordered
 * array of groups"): tasks B and C append to it without touching layout. The
 * controls whose commands exist are listed; the marks menus, links, code and
 * table groups arrive with their slices.
 */

/** A derived snapshot of the selection and the document, from one selector (plan § 6.1). */
export interface ToolbarContext {
  /** The selection is in inline code. */
  inCode: boolean;
  inCodeBlock: boolean;
  /** In a table cell (any ancestor is a table). */
  inTable: boolean;
  inListItem: boolean;
  /** Ancestor lists of the selection: 0 outside a list, 1..3 inside. */
  listDepth: number;
  /** The innermost list the selection is in. */
  listKind: 'bulletList' | 'orderedList' | null;
  /** 0 outside a heading, else 3 or 4. */
  headingLevel: number;
  marks: { bold: boolean; italic: boolean };
  canUndo: boolean;
  canRedo: boolean;
  canSink: boolean;
  canLift: boolean;
  /** The editor is not editable (the form is saving). */
  disabled: boolean;
}

export function selectToolbarContext(editor: Editor): ToolbarContext {
  const { selection } = editor.state;
  const $from = selection.$from;

  let listDepth = 0;
  let listKind: ToolbarContext['listKind'] = null;
  let inTable = false;
  for (let depth = $from.depth; depth > 0; depth--) {
    const name = $from.node(depth).type.name;
    if (name === 'bulletList' || name === 'orderedList') {
      listDepth++;
      listKind ??= name;
    }
    if (name === 'table') inTable = true;
  }
  const parent = $from.parent;

  return {
    inCode: editor.isActive('code'),
    inCodeBlock: parent.type.name === 'codeBlock',
    inTable,
    inListItem: listDepth > 0,
    listDepth,
    listKind,
    headingLevel: parent.type.name === 'heading' ? (parent.attrs.level as number) : 0,
    marks: { bold: editor.isActive('bold'), italic: editor.isActive('italic') },
    canUndo: editor.can().undo(),
    canRedo: editor.can().redo(),
    canSink: editor.can().sinkListItem('listItem'),
    canLift: editor.can().liftListItem('listItem'),
    disabled: !editor.isEditable,
  };
}

export interface ToolbarControlState {
  pressed?: boolean;
  disabled: boolean;
}

export interface ToolbarControl {
  /** Stable: tests, roving and the model test use it. */
  id: string;
  name: (copy: RichTextCopy) => string;
  /** `Mod+B` style; `Mod` is Ctrl, or ⌘ on macOS (the tooltip). */
  shortcut?: string;
  icon?: SvgIconComponent;
  /** A text glyph where there is no icon (the headings). */
  glyph?: string;
  /** `toggle` is a pressed/unpressed button; `action` is a plain one. */
  kind: 'toggle' | 'action';
  state: (context: ToolbarContext) => ToolbarControlState;
  run: (editor: Editor) => void;
}

export interface ToolbarGroup {
  id: string;
  controls: readonly ToolbarControl[];
}

/** Typing marks are disabled inside inline code and a code block (design table, BR-RT-15). */
const markDisabled = (context: ToolbarContext) =>
  context.disabled || context.inCode || context.inCodeBlock;

const heading = (level: 3 | 4): ToolbarControl => ({
  id: `h${level}`,
  name: (copy) => copy.controls[`h${level}` as 'h3' | 'h4'],
  glyph: `H${level}`,
  kind: 'toggle',
  shortcut: `Mod+Alt+${level}`,
  // A heading takes no list item, cell or code block: the schema says so, the
  // control follows it (the heading is at the top level only).
  state: (context) => ({
    pressed: context.headingLevel === level,
    disabled: context.disabled || context.inListItem || context.inTable || context.inCodeBlock,
  }),
  run: (editor) => {
    editor.chain().toggleHeading({ level }).run();
  },
});

export const TOOLBAR_GROUPS: readonly ToolbarGroup[] = [
  {
    id: 'history',
    controls: [
      {
        id: 'undo',
        name: (copy) => copy.controls.undo,
        shortcut: 'Mod+Z',
        icon: UndoIcon,
        kind: 'action',
        state: (context) => ({ disabled: context.disabled || !context.canUndo }),
        run: (editor) => {
          editor.chain().undo().run();
        },
      },
      {
        id: 'redo',
        name: (copy) => copy.controls.redo,
        shortcut: 'Mod+Y',
        icon: RedoIcon,
        kind: 'action',
        state: (context) => ({ disabled: context.disabled || !context.canRedo }),
        run: (editor) => {
          editor.chain().redo().run();
        },
      },
    ],
  },
  { id: 'block', controls: [heading(3), heading(4)] },
  {
    id: 'marks',
    controls: [
      {
        id: 'bold',
        name: (copy) => copy.controls.bold,
        shortcut: 'Mod+B',
        icon: FormatBoldIcon,
        kind: 'toggle',
        state: (context) => ({ pressed: context.marks.bold, disabled: markDisabled(context) }),
        run: (editor) => {
          editor.chain().toggleBold().run();
        },
      },
      {
        id: 'italic',
        name: (copy) => copy.controls.italic,
        shortcut: 'Mod+I',
        icon: FormatItalicIcon,
        kind: 'toggle',
        state: (context) => ({ pressed: context.marks.italic, disabled: markDisabled(context) }),
        run: (editor) => {
          editor.chain().toggleItalic().run();
        },
      },
    ],
  },
  {
    id: 'lists',
    controls: [
      {
        id: 'bullet',
        name: (copy) => copy.controls.bullet,
        shortcut: 'Mod+Shift+8',
        icon: FormatListBulletedIcon,
        kind: 'toggle',
        state: (context) => ({
          pressed: context.listKind === 'bulletList',
          disabled: context.disabled || context.inTable || context.inCodeBlock,
        }),
        run: (editor) => {
          editor.chain().toggleBulletList().run();
        },
      },
      {
        id: 'ordered',
        name: (copy) => copy.controls.ordered,
        shortcut: 'Mod+Shift+7',
        icon: FormatListNumberedIcon,
        kind: 'toggle',
        state: (context) => ({
          pressed: context.listKind === 'orderedList',
          disabled: context.disabled || context.inTable || context.inCodeBlock,
        }),
        run: (editor) => {
          editor.chain().toggleOrderedList().run();
        },
      },
      {
        id: 'indent',
        name: (copy) => copy.controls.indent,
        shortcut: 'Mod+]',
        icon: FormatIndentIncreaseIcon,
        kind: 'action',
        // Not at level 3 (BR-RT-20: three levels), not on a first item.
        state: (context) => ({
          disabled:
            context.disabled ||
            context.listDepth === 0 ||
            context.listDepth >= 3 ||
            !context.canSink,
        }),
        run: (editor) => {
          editor.chain().sinkListItem('listItem').run();
        },
      },
      {
        id: 'outdent',
        name: (copy) => copy.controls.outdent,
        shortcut: 'Mod+[',
        icon: FormatIndentDecreaseIcon,
        kind: 'action',
        // Only at level 2..3: at level 1 the list is left by the list button.
        state: (context) => ({
          disabled: context.disabled || context.listDepth < 2 || !context.canLift,
        }),
        run: (editor) => {
          editor.chain().liftListItem('listItem').run();
        },
      },
    ],
  },
];
