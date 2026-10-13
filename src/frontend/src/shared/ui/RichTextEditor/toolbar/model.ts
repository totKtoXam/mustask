import type { SvgIconComponent } from '@mui/icons-material';
import EmojiEmotionsOutlinedIcon from '@mui/icons-material/EmojiEmotionsOutlined';
import FormatBoldIcon from '@mui/icons-material/FormatBold';
import FormatClearIcon from '@mui/icons-material/FormatClear';
import FormatColorTextIcon from '@mui/icons-material/FormatColorText';
import FormatIndentDecreaseIcon from '@mui/icons-material/FormatIndentDecrease';
import FormatIndentIncreaseIcon from '@mui/icons-material/FormatIndentIncrease';
import FormatItalicIcon from '@mui/icons-material/FormatItalic';
import FormatListBulletedIcon from '@mui/icons-material/FormatListBulleted';
import FormatListNumberedIcon from '@mui/icons-material/FormatListNumbered';
import FormatSizeIcon from '@mui/icons-material/FormatSize';
import FormatStrikethroughIcon from '@mui/icons-material/FormatStrikethrough';
import FormatUnderlinedIcon from '@mui/icons-material/FormatUnderlined';
import BorderColorIcon from '@mui/icons-material/BorderColor';
import CodeIcon from '@mui/icons-material/Code';
import LinkIcon from '@mui/icons-material/Link';
import LinkOffIcon from '@mui/icons-material/LinkOff';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import RedoIcon from '@mui/icons-material/Redo';
import UndoIcon from '@mui/icons-material/Undo';
import type { Editor } from '@tiptap/core';
import { FONT_SIZES, HIGHLIGHTS, TEXT_COLOURS, type VocabularyToken } from '../../../lib/richText';
import { clearFormatting } from '../behaviour/clearFormatting';
import type { RichTextCopy } from '../copy';

/**
 * The toolbar's data (design § Toolbar: "control list is data, an ordered
 * array of groups"): tasks B and C append to it without touching layout.
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
  marks: { bold: boolean; italic: boolean; underline: boolean; strike: boolean; code: boolean };
  /** The vocabulary names of the palette marks at the selection, or `null`. */
  colour: string | null;
  highlight: string | null;
  size: string | null;
  inLink: boolean;
  /** The address of the link at the selection, or `null`. */
  href: string | null;
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
  const tokenOf = (mark: string) => {
    const token = editor.getAttributes(mark).token as string | undefined;
    return token ?? null;
  };

  return {
    inCode: editor.isActive('code'),
    inCodeBlock: parent.type.name === 'codeBlock',
    inTable,
    inListItem: listDepth > 0,
    listDepth,
    listKind,
    headingLevel: parent.type.name === 'heading' ? (parent.attrs.level as number) : 0,
    marks: {
      bold: editor.isActive('bold'),
      italic: editor.isActive('italic'),
      underline: editor.isActive('underline'),
      strike: editor.isActive('strike'),
      code: editor.isActive('code'),
    },
    colour: tokenOf('rtColor'),
    highlight: tokenOf('rtHighlight'),
    size: tokenOf('rtSize'),
    inLink: editor.isActive('link'),
    href: (editor.getAttributes('link').href as string | undefined) ?? null,
    canUndo: editor.can().undo(),
    canRedo: editor.can().redo(),
    canSink: editor.can().sinkListItem('listItem'),
    canLift: editor.can().liftListItem('listItem'),
    disabled: !editor.isEditable,
  };
}

/** What a control can ask of the editor's own UI (the dialog and the popovers). */
export interface ToolbarUi {
  openLink: () => void;
}

export interface ToolbarControlState {
  pressed?: boolean;
  disabled: boolean;
  /** For a menu: the checked option's value; `null` is the reset item. */
  value?: string | null;
}

/** One item of a menu: palettes, sizes, languages, table operations. */
export interface MenuOption {
  /** What `choose` receives; `null` is the reset item. */
  value: string | null;
  label: string;
  /** The vocabulary class the item previews (a colour or a highlight swatch). */
  swatch?: { className: string; kind: 'colour' | 'highlight' };
  disabled?: boolean;
  /** An item separated from the ones above by a divider. */
  divided?: boolean;
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
  /**
   * `toggle` is a pressed/unpressed button; `action` a plain one; `menu` opens
   * a `Menu` of `options`; `popover` opens the control's own popover.
   */
  kind: 'toggle' | 'action' | 'menu' | 'popover';
  state: (context: ToolbarContext) => ToolbarControlState;
  run?: (editor: Editor, ui: ToolbarUi) => void;
  options?: (copy: RichTextCopy, context: ToolbarContext) => MenuOption[];
  choose?: (editor: Editor, value: string | null) => void;
}

export interface ToolbarGroup {
  id: string;
  controls: readonly ToolbarControl[];
}

/** Typing marks are disabled inside inline code and a code block (design table, BR-RT-15). */
const inCodeContext = (context: ToolbarContext) => context.inCode || context.inCodeBlock;
const markDisabled = (context: ToolbarContext) => context.disabled || inCodeContext(context);

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

const markToggle = (
  id: 'bold' | 'italic' | 'underline' | 'strike',
  icon: SvgIconComponent,
  shortcut: string,
  toggle: (editor: Editor) => void,
): ToolbarControl => ({
  id,
  name: (copy) => copy.controls[id],
  shortcut,
  icon,
  kind: 'toggle',
  state: (context) => ({ pressed: context.marks[id], disabled: markDisabled(context) }),
  run: toggle,
});

/** A palette menu: one item per vocabulary token, then the reset item, over a mark. */
function paletteMenu(
  id: 'colour' | 'highlight' | 'size',
  icon: SvgIconComponent,
  family: readonly VocabularyToken[],
  mark: 'rtColor' | 'rtHighlight' | 'rtSize',
  swatch: 'colour' | 'highlight' | null,
  labels: (copy: RichTextCopy) => Record<string, string>,
  reset: (copy: RichTextCopy) => string,
  current: (context: ToolbarContext) => string | null,
): ToolbarControl {
  return {
    id,
    name: (copy) => copy.controls[id],
    icon,
    kind: 'menu',
    state: (context) => ({
      // Size is also disabled in a heading (BR-RT-15).
      disabled: markDisabled(context) || (id === 'size' && context.headingLevel > 0),
      pressed: current(context) !== null,
      value: current(context),
    }),
    options: (copy) => [
      { value: null, label: reset(copy) },
      ...family.map((token) => ({
        value: token.name,
        label: labels(copy)[token.name] ?? token.name,
        ...(swatch ? { swatch: { className: token.class, kind: swatch } } : {}),
      })),
    ],
    choose: (editor, value) => {
      if (value === null) {
        editor.chain().unsetMark(mark).run();
      } else {
        editor.chain().setMark(mark, { token: value }).run();
      }
    },
  };
}

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
  {
    id: 'block',
    controls: [
      heading(3),
      heading(4),
      paletteMenu(
        'size',
        FormatSizeIcon,
        FONT_SIZES,
        'rtSize',
        null,
        (copy) => copy.sizes,
        (copy) => copy.resetSize,
        (context) => context.size,
      ),
    ],
  },
  {
    id: 'marks',
    controls: [
      markToggle('bold', FormatBoldIcon, 'Mod+B', (editor) => {
        editor.chain().toggleBold().run();
      }),
      markToggle('italic', FormatItalicIcon, 'Mod+I', (editor) => {
        editor.chain().toggleItalic().run();
      }),
      markToggle('underline', FormatUnderlinedIcon, 'Mod+U', (editor) => {
        editor.chain().toggleUnderline().run();
      }),
      markToggle('strike', FormatStrikethroughIcon, 'Mod+Shift+S', (editor) => {
        editor.chain().toggleStrike().run();
      }),
      {
        id: 'code',
        name: (copy) => copy.controls.code,
        shortcut: 'Mod+E',
        icon: CodeIcon,
        kind: 'toggle',
        // Only a code block disables the inline code button: inside inline code it is the way out.
        state: (context) => ({
          pressed: context.marks.code,
          disabled: context.disabled || context.inCodeBlock,
        }),
        run: (editor) => {
          // A caret inside inline code takes the whole run out of it, not only the next typing.
          if (editor.isActive('code') && editor.state.selection.empty) {
            editor.chain().extendMarkRange('code').unsetCode().run();
          } else {
            editor.chain().toggleCode().run();
          }
        },
      },
      paletteMenu(
        'colour',
        FormatColorTextIcon,
        TEXT_COLOURS,
        'rtColor',
        'colour',
        (copy) => copy.colours,
        (copy) => copy.resetColour,
        (context) => context.colour,
      ),
      paletteMenu(
        'highlight',
        BorderColorIcon,
        HIGHLIGHTS,
        'rtHighlight',
        'highlight',
        (copy) => copy.highlights,
        (copy) => copy.resetHighlight,
        (context) => context.highlight,
      ),
      {
        id: 'clear',
        name: (copy) => copy.controls.clear,
        icon: FormatClearIcon,
        kind: 'action',
        state: (context) => ({ disabled: markDisabled(context) }),
        run: (editor) => {
          clearFormatting(editor);
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
  {
    id: 'links',
    controls: [
      {
        id: 'link',
        name: (copy) => copy.controls.link,
        shortcut: 'Mod+K',
        icon: LinkIcon,
        kind: 'action',
        state: (context) => ({
          pressed: context.inLink,
          disabled: context.disabled || context.inCodeBlock,
        }),
        run: (_editor, ui) => ui.openLink(),
      },
      {
        id: 'unlink',
        name: (copy) => copy.controls.unlink,
        icon: LinkOffIcon,
        kind: 'action',
        state: (context) => ({ disabled: context.disabled || !context.inLink }),
        run: (editor) => {
          editor.chain().unsetLink().run();
        },
      },
      {
        id: 'open',
        name: (copy) => copy.controls.open,
        icon: OpenInNewIcon,
        kind: 'action',
        state: (context) => ({ disabled: context.disabled || !context.inLink }),
        run: (editor) => {
          const href = editor.getAttributes('link').href as string | undefined;
          if (href) window.open(href, '_blank', 'noopener,noreferrer');
        },
      },
      {
        id: 'emoji',
        name: (copy) => copy.controls.emoji,
        icon: EmojiEmotionsOutlinedIcon,
        kind: 'popover',
        state: (context) => ({ disabled: context.disabled }),
      },
      // RESERVED (feat-NNN images, mentions-links): append «Изображение», «Упомянуть»,
      // «Ссылка на рабочий элемент» here, after 'emoji', before group 'code'.
    ],
  },
];
