import { useEffect, useRef, useState, type MutableRefObject } from 'react';
import Box from '@mui/material/Box';
import { styled } from '@mui/material/styles';
import { getSchema, type Editor } from '@tiptap/core';
import type { Node as ProseMirrorNode } from '@tiptap/pm/model';
import { EditorContent, useEditor } from '@tiptap/react';
import { FONT_SIZES, HIGHLIGHTS, TEXT_COLOURS, normaliseRichText } from '../../lib/richText';
import { documentJson } from './behaviour/loadDocument';
import type { RichTextCopy } from './copy';
import { createExtensions } from './schema/extensions';
import { Toolbar } from './toolbar/Toolbar';

/** What the editor reports on a change of the document (plan-frontend § 7.2). */
export interface RichTextChange {
  /** The canonical html; `''` for a blank document (BR-RT-6). */
  html: string;
  /** BR-RT-8, code points. */
  visibleLength: number;
  /** Code points of the canonical string. */
  markupLength: number;
}

/**
 * Reporting is debounced: spike S3 measured the normalisation at 45–660 ms on
 * a 100 000-character document in jsdom (4 s on the 700 007-markup legacy
 * worst case), over the 50 ms the plan allows for a synchronous report
 * (plan § 7.2). The imperative `flush()` the form's submit calls first makes
 * the delay invisible to a save.
 */
export const REPORT_DELAY_MS = 150;

export interface EditorImplProps {
  /** The canonical html (or `''`): the baseline the document is loaded from. */
  value: string;
  onChange: (change: RichTextChange) => void;
  disabled: boolean;
  invalid: boolean;
  /** The body's accessible name. */
  label: string;
  placeholder: string;
  describedBy: string | undefined;
  copy: RichTextCopy;
  /** Set to the editor's `flush()` while it is mounted. */
  flushRef: MutableRefObject<(() => void) | null>;
  /** `@internal`, tests: the live editor, `null` on unmount. */
  onEditorReady?: (editor: Editor | null) => void;
}

/**
 * The editor's content styles: one `styled` root reading the theme object
 * (design § Responsive and theming). Marks take their colours from
 * `theme.richText` by the vocabulary's class names; the stored class is the
 * contract, the look is the theme's.
 */
const Content = styled('div')(({ theme }) => {
  const body = theme.typography.body1;
  const tokenRules = Object.fromEntries([
    ...TEXT_COLOURS.map((token) => [
      `& .${token.class}`,
      { color: theme.richText.colour(token.class) ?? undefined },
    ]),
    ...HIGHLIGHTS.map((token) => [
      `& .${token.class}`,
      {
        backgroundColor: theme.richText.colour(token.class) ?? undefined,
        color: theme.richText.highlightText(),
      },
    ]),
    ...FONT_SIZES.map((token) => [
      `& .${token.class}`,
      {
        fontSize: `${theme.richText.fontSize(token.class) ?? 0}px`,
        lineHeight: theme.richText.sizedLineHeight,
      },
    ]),
  ]);

  return {
    '& .ProseMirror': {
      ...body,
      minHeight: `calc(6 * ${body.lineHeight} * ${body.fontSize})`,
      maxHeight: '70vh',
      overflowY: 'auto',
      padding: theme.spacing(1, 1.5),
      outline: 'none',
      whiteSpace: 'pre-wrap',
      overflowWrap: 'anywhere',
      color: theme.palette.text.primary,
      '& > * + *': { marginTop: theme.spacing(1) },
      '& p': { margin: 0 },
      '& h3': { ...theme.typography.h2, margin: 0 },
      '& h4': { ...theme.typography.h3, margin: 0 },
      '& ul, & ol': { margin: 0, paddingLeft: theme.spacing(3) },
      '& li > p': { margin: 0 },
      '& li > * + *': { marginTop: theme.spacing(0.5) },
      '& a': {
        color: theme.palette.primary.dark,
        textDecoration: 'underline',
        cursor: 'text',
      },
      '& code': {
        fontFamily: 'monospace',
        backgroundColor: theme.palette.shell.tableHeader,
        borderRadius: theme.shape.borderRadius,
        padding: theme.spacing(0, 0.5),
      },
      '& pre': {
        margin: 0,
        padding: theme.spacing(1, 1.5),
        overflowX: 'auto',
        whiteSpace: 'pre',
        tabSize: 4,
        fontFamily: 'monospace',
        fontSize: theme.typography.tableCell.fontSize,
        lineHeight: '20px',
        backgroundColor: theme.richText.codeBackground(),
        border: `1px solid ${theme.palette.divider}`,
        borderRadius: theme.shape.borderRadius,
        '& code': { padding: 0, background: 'none', fontFamily: 'inherit' },
      },
      '& table': { borderCollapse: 'collapse', width: '100%' },
      '& th, & td': {
        border: `1px solid ${theme.palette.divider}`,
        padding: theme.spacing(0.5, 1),
        verticalAlign: 'top',
        minWidth: theme.spacing(12),
      },
      '& th': {
        backgroundColor: theme.palette.shell.tableHeader,
        fontWeight: theme.typography.fontWeightMedium,
        textAlign: 'left',
      },
      '& p.is-editor-empty:first-of-type::before': {
        content: 'attr(data-placeholder)',
        color: theme.palette.text.secondary,
        float: 'left',
        height: 0,
        pointerEvents: 'none',
      },
      ...tokenRules,
    },
  };
});

interface SyncState {
  /** The document right after load, or after the caller adopted what it held. */
  doc: ProseMirrorNode;
  /** The caller's string for that document: reported while the document equals it. */
  baseline: string;
  /** What the caller last saw from this editor. */
  lastReported: string;
}

/**
 * The editor of the lazy chunk: TipTap in a styled root with the product's
 * toolbar. A loaded value that differs from what the document holds replaces
 * it by remounting the inner editor (TipTap has no history reset), which is
 * the spec's "replaced and the undo history cleared" (plan § 7.2).
 */
export default function EditorImpl(props: EditorImplProps) {
  const [generation, setGeneration] = useState(0);
  return (
    <EditorBody
      key={`${generation}:${props.placeholder}`}
      {...props}
      onReplace={() => setGeneration((current) => current + 1)}
    />
  );
}

function EditorBody({
  value,
  onChange,
  disabled,
  invalid,
  label,
  placeholder,
  describedBy,
  copy,
  flushRef,
  onEditorReady,
  onReplace,
}: EditorImplProps & { onReplace: () => void }) {
  const onChangeRef = useRef(onChange);
  const sync = useRef<SyncState | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    onChangeRef.current = onChange;
  });

  const [extensions] = useState(() => createExtensions(placeholder));
  // The document is built once, from the value the editor mounts with; later
  // values are the caller's echo or a replacement (the sync effect below).
  const [initial] = useState(() => documentJson(getSchema(extensions), value));

  const editor = useEditor({
    extensions,
    content: initial,
    editable: !disabled,
    editorProps: {
      attributes: {
        role: 'textbox',
        'aria-multiline': 'true',
        'aria-label': label,
        'aria-placeholder': placeholder,
      },
    },
  });

  useEffect(() => {
    if (!editor) return undefined;
    sync.current = { doc: editor.state.doc, baseline: value, lastReported: value };
    onEditorReady?.(editor);
    return () => onEditorReady?.(null);
    // The sync state is the document as loaded; `value` here is the mount value.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editor]);

  // Reports the document's state: the caller's own baseline string while the
  // document equals the loaded one (so edited-and-edited-back is no change),
  // otherwise the normalised serialisation (BR-RT-5, BR-RT-11).
  useEffect(() => {
    if (!editor) return undefined;

    const flush = () => {
      if (timer.current !== null) {
        clearTimeout(timer.current);
        timer.current = null;
      }
      const state = sync.current;
      if (!state) return;

      let change: RichTextChange;
      if (state.doc.eq(editor.state.doc)) {
        const { visibleLength, markupLength } = normaliseRichText(state.baseline);
        change = { html: state.baseline, visibleLength, markupLength };
      } else {
        const { html, visibleLength, markupLength } = normaliseRichText(editor.getHTML());
        change = { html, visibleLength, markupLength };
      }
      if (change.html === state.lastReported) return;
      state.lastReported = change.html;
      onChangeRef.current(change);
    };

    const schedule = () => {
      if (timer.current !== null) clearTimeout(timer.current);
      timer.current = setTimeout(flush, REPORT_DELAY_MS);
    };

    editor.on('update', schedule);
    flushRef.current = flush;
    return () => {
      editor.off('update', schedule);
      if (timer.current !== null) clearTimeout(timer.current);
      timer.current = null;
      if (flushRef.current === flush) flushRef.current = null;
    };
  }, [editor, flushRef]);

  // A new `value`: the echo of what was reported is left alone; what the
  // document already holds is adopted (caret and undo kept); anything else
  // replaces the document and clears the history (plan § 7.2).
  useEffect(() => {
    const state = sync.current;
    if (!editor || !state) return;
    if (value === state.lastReported) return;

    const held = state.doc.eq(editor.state.doc)
      ? state.baseline
      : normaliseRichText(editor.getHTML()).html;
    if (value === held) {
      sync.current = { doc: editor.state.doc, baseline: value, lastReported: value };
      return;
    }
    onReplace();
    // Runs on a new `value` only.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value]);

  useEffect(() => {
    if (editor && editor.isEditable !== !disabled) editor.setEditable(!disabled, false);
  }, [editor, disabled]);

  useEffect(() => {
    editor?.setOptions({
      editorProps: {
        attributes: {
          role: 'textbox',
          'aria-multiline': 'true',
          'aria-label': label,
          'aria-placeholder': placeholder,
          ...(invalid ? { 'aria-invalid': 'true' } : {}),
          ...(disabled ? { 'aria-disabled': 'true' } : {}),
          ...(describedBy ? { 'aria-describedby': describedBy } : {}),
        },
      },
    });
  }, [editor, label, placeholder, invalid, disabled, describedBy]);

  if (!editor) return null;

  return (
    <Box>
      <Toolbar editor={editor} copy={copy} />
      <Content>
        <EditorContent editor={editor} />
      </Content>
    </Box>
  );
}
