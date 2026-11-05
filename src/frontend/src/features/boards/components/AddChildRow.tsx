import { useEffect, useRef, useState, type KeyboardEvent, type RefObject } from 'react';
import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';
import InputAdornment from '@mui/material/InputAdornment';
import TableRow from '@mui/material/TableRow';
import TextField from '@mui/material/TextField';
import { useTheme } from '@mui/material/styles';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import { titleError } from '../../workitems';
import type { AddEntry } from '../lib/addChild';
import { clipped, rowSx } from './backlogRowStyle';
import { BacklogCell } from './BacklogCell';

/** How an add row closed without a create: for the table's focus and read rules. */
export type AddCancel = 'escape' | 'empty' | 'blur';

/**
 * The inline add row of a child (feat-027 US-4, BR-T-10; design § 4.8): an
 * editable row directly beneath the parent, one depth deeper and drawn
 * selected, with the fixed type's name in «Тип» and its icon before a focused
 * title field; «Порядок», «Статус» and the three values stay empty, and there
 * is no grip, «+» or «⋮» — there is no item yet, and the row is never a drag
 * source (D-11).
 *
 * Enter makes the create: the title is checked first (mod-003's `titleError`,
 * so «Не более 255 символов.» shows here and nothing is sent) and handed to the
 * table, which makes **one `requestId` at the first Enter and keeps it for
 * every retry of this opening** — a double Enter, a resend after a lost
 * answer, a retry after a refusal (BR-M3-26); the next opening starts afresh.
 * The row is not drawn while an ancestor of its parent is collapsed, and the
 * table keeps its title and `requestId`, so it comes back with both (F-7): the
 * title typed so far is the `initialTitle` it starts from. An empty title is no
 * refusal: Enter cancels. Escape cancels. Leaving the field empty cancels it;
 * with text typed the row stays open. While the create is saved the field is
 * read-only with a progress indicator. The field takes focus only when the row
 * is opened (or brought back to a user who asked for it), not whenever the row
 * is drawn again.
 */
export function AddChildRow({
  parentIdentifier,
  entry,
  tier,
  block,
  busy,
  error,
  focusOnMount,
  initialTitle,
  inputRef,
  onFocused,
  onEdit,
  onSubmit,
  onCancel,
}: {
  parentIdentifier: string;
  entry: AddEntry;
  /** The add row's depth: its parent's plus one. */
  tier: number;
  /** The top-level block the row stands in, as its parent's. */
  block: number;
  /** The create is being saved. */
  busy: boolean;
  /** The refusal the table holds for the field, `null` for none. */
  error: string | null;
  /** The row has just been opened: it scrolls into view and its field takes focus. */
  focusOnMount: boolean;
  /** The title typed before the row was last drawn: the table keeps it; empty on a fresh opening. */
  initialTitle: string;
  inputRef: RefObject<HTMLInputElement | null>;
  /** The field has taken focus: the table does not ask again for this opening. */
  onFocused(): void;
  /** The title was edited: the table keeps it, and typing again takes the refusal off the field. */
  onEdit(title: string): void;
  onSubmit(title: string): void;
  onCancel(how: AddCancel): void;
}) {
  const theme = useTheme();
  const { table, iconAction } = theme.layout;
  const [title, setTitle] = useState(initialTitle);
  const [checked, setChecked] = useState<string | null>(null);
  const row = useRef<HTMLTableRowElement>(null);
  const takeFocus = useRef(focusOnMount);

  // A new row is brought into view (design § 4.8), and its field takes focus —
  // after the closing menu that opened the row has given focus back to its
  // button, which it does as its own effect runs; jsdom has no scrolling.
  useEffect(() => {
    if (!takeFocus.current) {
      return;
    }
    row.current?.scrollIntoView?.({ block: 'nearest' });
    const focus = setTimeout(() => {
      inputRef.current?.focus();
      onFocused();
    }, 0);
    return () => clearTimeout(focus);
  }, [inputRef, onFocused]);

  function submit() {
    if (busy) {
      return;
    }
    if (title.trim() === '') {
      onCancel('empty');
      return;
    }
    const refused = titleError(title);
    if (refused) {
      setChecked(refused);
      return;
    }
    onSubmit(title);
  }

  function handleKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.nativeEvent.isComposing) {
      return;
    }
    if (event.key === 'Enter') {
      event.preventDefault();
      submit();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      if (!busy) {
        onCancel('escape');
      }
    }
  }

  const shown = checked ?? error;
  return (
    <TableRow
      ref={row}
      data-add-row={parentIdentifier}
      data-block={block}
      data-tier={tier}
      data-selected="true"
      sx={rowSx(true)}
    >
      <BacklogCell kind="hierarchy" />
      <BacklogCell />
      <BacklogCell>
        <Box component="span" title={entry.name} sx={clipped}>
          {entry.name}
        </Box>
      </BacklogCell>
      <BacklogCell kind="title">
        <Box
          sx={{
            display: 'flex',
            alignItems: 'flex-start',
            minWidth: 0,
            paddingLeft: `${tier * table.treeIndent}px`,
          }}
        >
          <Box
            aria-hidden="true"
            sx={{
              width: iconAction.chevron.box,
              height: theme.density.registryRowHeight,
              flexShrink: 0,
            }}
          />
          <Box
            component="span"
            sx={{
              display: 'inline-flex',
              alignItems: 'center',
              height: theme.density.registryRowHeight,
              flexShrink: 0,
              marginLeft: 0.5,
              marginRight: 0.5,
            }}
          >
            <TypeMark appearance={entry.appearance} compact colour={theme.palette.primary.dark} />
          </Box>
          <TextField
            variant="outlined"
            size="small"
            hiddenLabel
            fullWidth
            value={title}
            placeholder="Введите заголовок"
            error={shown !== null}
            helperText={shown ?? undefined}
            inputRef={inputRef}
            onChange={(event) => {
              setTitle(event.target.value);
              setChecked(null);
              onEdit(event.target.value);
            }}
            onKeyDown={handleKeyDown}
            onBlur={() => {
              if (!busy && title.trim() === '') {
                onCancel('blur');
              }
            }}
            slotProps={{
              htmlInput: {
                'aria-label': `Заголовок: ${entry.name} (${parentIdentifier})`,
                'aria-invalid': shown !== null,
                'aria-busy': busy,
                readOnly: busy,
              },
              input: {
                endAdornment: busy ? (
                  <InputAdornment position="end">
                    <CircularProgress size={16} aria-hidden="true" />
                  </InputAdornment>
                ) : undefined,
              },
              formHelperText: { role: 'alert' },
            }}
            sx={{
              marginTop: `${(theme.density.registryRowHeight - 28) / 2}px`,
              marginBottom: `${(theme.density.registryRowHeight - 28) / 2}px`,
              '& .MuiOutlinedInput-root': { height: 28, paddingRight: 1 },
              '& .MuiOutlinedInput-input': {
                ...theme.typography.tableCell,
                padding: theme.spacing(0.5, 1),
              },
              '& .MuiOutlinedInput-notchedOutline': {
                borderColor: theme.palette.primary.main,
                borderWidth: 1,
              },
              '& .MuiFormHelperText-root': { marginLeft: 0, marginRight: 0, marginBottom: 0.5 },
            }}
          />
        </Box>
      </BacklogCell>
      {/* «Статус», «Команда», «Проект» (feat-040 D-1), «Трудоёмкость», «Бизнес-ценность», «Вид ценности»: nothing yet. */}
      <BacklogCell />
      <BacklogCell />
      <BacklogCell />
      <BacklogCell />
      <BacklogCell />
      <BacklogCell />
    </TableRow>
  );
}
