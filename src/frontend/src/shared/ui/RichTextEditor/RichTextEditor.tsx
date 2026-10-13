import {
  useCallback,
  useEffect,
  useId,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
  type ComponentType,
  type Ref,
} from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import FormHelperText from '@mui/material/FormHelperText';
import Skeleton from '@mui/material/Skeleton';
import { styled } from '@mui/material/styles';
import Typography from '@mui/material/Typography';
import type { Editor } from '@tiptap/core';
import {
  DESCRIPTION_COUNTER_FROM,
  DESCRIPTION_MAX_VISIBLE,
  normaliseRichText,
} from '../../lib/richText';
import { DEFAULT_COPY, type RichTextCopy } from './copy';
import type { EditorImplProps, RichTextChange } from './EditorImpl';
import { loadEditorImpl } from './loadEditorImpl';

export type { RichTextChange } from './EditorImpl';
export type { RichTextCopy } from './copy';

/** The imperative surface: a form's submit calls `flush()` before it reads the draft. */
export interface RichTextEditorHandle {
  /** Reports a change that is still waiting for its delay, now. A no-op while the editor loads. */
  flush(): void;
}

export interface RichTextEditorProps {
  /** The body's accessible name — the section's title («Описание»). */
  label: string;
  /**
   * The canonical html, or `''`. Controlled: the value the editor was loaded
   * from, and what a save returned. A value the document already holds is left
   * alone; another one replaces the document and clears the undo history.
   */
  value: string;
  /** A change of the document: the canonical string and its counts. */
  onChange: (change: RichTextChange) => void;
  /** The form is saving: not editable, every control disabled, nothing moved. */
  disabled?: boolean;
  placeholder?: string;
  /** The line under the card (design § Layout), e.g. «До 100 000 символов». */
  helperText?: string;
  /** A refusal; replaces the helper and the live limit text, and turns the card red. */
  errorText?: string;
  /** The visible limit (BR-RT-7), default 100 000. */
  maxVisible?: number;
  /** From how many visible characters the counter shows, default 90 000. */
  counterFrom?: number;
  /** Overrides of the interface texts. */
  copy?: Partial<RichTextCopy>;
  ref?: Ref<RichTextEditorHandle>;
  /** `@internal`, tests: the live editor, `null` on unmount. TipTap types never leave `shared/ui`. */
  onEditorReady?: (editor: Editor | null) => void;
}

/**
 * The card is styled as a sibling of an outlined field (design § Components):
 * one border for the toolbar and the body, the theme's input border, hover,
 * a 2 px accent on focus anywhere inside, error and disabled.
 */
const Card = styled('div', {
  shouldForwardProp: (name) => name !== 'invalid' && name !== 'disabled',
})<{ invalid: boolean; disabled: boolean }>(({ theme, invalid, disabled }) => {
  const resting = invalid ? theme.palette.error.main : theme.palette.shell.inputBorder;
  const frame = (colour: string) => ({
    borderColor: colour,
    boxShadow: `inset 0 0 0 1px ${colour}`,
  });
  return {
    border: `1px solid ${resting}`,
    borderRadius: theme.shape.borderRadius,
    backgroundColor: theme.palette.background.paper,
    '&:hover': disabled
      ? undefined
      : { borderColor: invalid ? resting : theme.palette.text.primary },
    '&:focus-within': frame(invalid ? theme.palette.error.main : theme.palette.primary.main),
    ...(disabled
      ? { borderColor: theme.palette.action.disabled, color: theme.palette.text.disabled }
      : {}),
  };
});

/**
 * The generic rich-text editor (feat-018, ADR-0027): no business logic. The
 * editor itself is a lazy chunk; until it arrives a skeleton stands in and the
 * form's other controls work, and a failed load is an `Alert` with «Повторить».
 */
export function RichTextEditor({
  label,
  value,
  onChange,
  disabled = false,
  placeholder,
  helperText,
  errorText,
  maxVisible = DESCRIPTION_MAX_VISIBLE,
  counterFrom = DESCRIPTION_COUNTER_FROM,
  copy: copyOverrides,
  ref,
  onEditorReady,
}: RichTextEditorProps) {
  const copy = useMemo(() => ({ ...DEFAULT_COPY, ...copyOverrides }), [copyOverrides]);
  const ids = { helper: useId(), counter: useId(), notice: useId() };
  const flushRef = useRef<(() => void) | null>(null);

  // The notice line (design § States, plan § 9.2): one polite live region, always in the DOM.
  // The same message again — a second dropped image — is announced again: the region is
  // emptied first and filled in the next task.
  const [notice, setNotice] = useState('');
  const noticeRef = useRef('');
  const showNotice = useCallback((message: string) => {
    if (message !== '' && message === noticeRef.current) {
      noticeRef.current = '';
      setNotice('');
      window.setTimeout(() => {
        noticeRef.current = message;
        setNotice(message);
      }, 0);
      return;
    }
    noticeRef.current = message;
    setNotice(message);
  }, []);
  useImperativeHandle(ref, () => ({ flush: () => flushRef.current?.() }), []);

  // The editor's chunk: loaded once, and again when «Повторить» is pressed (a failed dynamic
  // import is not retried by the browser on its own, and a rejected `lazy` is cached forever,
  // so the load is the component's own, not `React.lazy`).
  const [attempt, setAttempt] = useState(0);
  const [Impl, setImpl] = useState<ComponentType<EditorImplProps> | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    let current = true;
    loadEditorImpl().then(
      (module) => {
        if (current) setImpl(() => module.default);
      },
      () => {
        if (current) setFailed(true);
      },
    );
    return () => {
      current = false;
    };
  }, [attempt]);

  // The visible length: what the editor last reported for this html, else the
  // value's own (a replacement, or before the first change).
  const [reported, setReported] = useState<{ html: string; visible: number } | null>(null);
  const ownVisible = useMemo(
    () => (reported?.html === value ? reported.visible : normaliseRichText(value).visibleLength),
    [reported, value],
  );
  const visible = ownVisible;
  const over = visible > maxVisible;
  const invalid = over || errorText !== undefined;

  const handleChange = (change: RichTextChange) => {
    setReported({ html: change.html, visible: change.visibleLength });
    onChange(change);
  };

  const shown = errorText ?? (over ? copy.tooLong : helperText);
  const showCounter = visible >= counterFrom;
  const describedBy =
    [shown ? ids.helper : null, showCounter ? ids.counter : null, ids.notice]
      .filter(Boolean)
      .join(' ') || undefined;
  const bodyPlaceholder = placeholder ?? copy.placeholder;

  return (
    <Box>
      <Card invalid={invalid} disabled={disabled}>
        {Impl !== null ? (
          <Impl
            value={value}
            onChange={handleChange}
            disabled={disabled}
            invalid={invalid}
            label={label}
            placeholder={bodyPlaceholder}
            describedBy={describedBy}
            copy={copy}
            flushRef={flushRef}
            onNotice={showNotice}
            onEditorReady={onEditorReady}
          />
        ) : failed ? (
          <Alert
            severity="error"
            action={
              <Button
                color="inherit"
                size="small"
                onClick={() => {
                  setFailed(false);
                  setAttempt((count) => count + 1);
                }}
              >
                {copy.retry}
              </Button>
            }
          >
            {copy.loadFailed}
          </Alert>
        ) : (
          <EditorSkeleton loading={copy.loading} />
        )}
      </Card>

      <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, mt: 0.5, px: 1.75 }}>
        <FormHelperText
          id={ids.helper}
          error={invalid && shown !== undefined}
          role={over && errorText === undefined ? 'alert' : undefined}
          sx={{ m: 0 }}
        >
          {shown}
        </FormHelperText>
        {showCounter && (
          <Typography
            id={ids.counter}
            variant="caption"
            color={over ? 'error' : 'text.secondary'}
            sx={{ whiteSpace: 'nowrap', fontWeight: over ? 600 : undefined }}
          >
            <span aria-hidden="true">{copy.counter(visible, maxVisible)}</span>
            <Box component="span" sx={(theme) => theme.mixins.visuallyHidden}>
              {copy.counterName(visible, maxVisible)}
            </Box>
          </Typography>
        )}
      </Box>

      {/* The notice line: always in the DOM, so a message is announced when it appears. */}
      <Typography
        id={ids.notice}
        variant="caption"
        color="text.secondary"
        role="status"
        aria-live="polite"
        sx={{ display: 'block', px: 1.75, minHeight: 0 }}
      >
        {notice}
      </Typography>
    </Box>
  );
}

/** Toolbar and six lines while the editor's code loads (design § States). */
function EditorSkeleton({ loading }: { loading: string }) {
  return (
    <Box aria-busy="true" sx={{ p: 1 }}>
      <Box component="span" sx={(theme) => theme.mixins.visuallyHidden}>
        {loading}
      </Box>
      <Skeleton variant="rounded" height={32} sx={{ mb: 1 }} />
      {Array.from({ length: 6 }, (_, line) => (
        <Skeleton key={line} variant="text" />
      ))}
    </Box>
  );
}
