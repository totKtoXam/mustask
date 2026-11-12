import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Popover from '@mui/material/Popover';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import type { RichTextCopy } from '../copy';
import { searchEmoji, type EmojiEntry } from './emoji/search';

const COLUMNS = 8;

interface EmojiData {
  groups: readonly string[];
  emoji: readonly EmojiEntry[];
}

interface EmojiPopoverProps {
  anchorEl: HTMLElement | null;
  copy: RichTextCopy['emoji'];
  /** The load of the data failed (design gap G-1: the editor's own failure copy and button). */
  failure: { message: string; retry: string };
  onPick: (emoji: string) => void;
  /** `escape` is a close from the keyboard: focus goes back to the opener. */
  onClose: (reason: 'escape' | 'other') => void;
}

/** The module is loaded on the first open and kept (its own chunk, ADR-0039 decision 4). */
let loaded: Promise<EmojiData> | null = null;

function loadEmoji(): Promise<EmojiData> {
  loaded ??= import('virtual:rich-text-emoji')
    .then((module) => ({ groups: module.groups, emoji: module.emoji }))
    .catch((error: unknown) => {
      // A failed load is not kept: «Повторить» asks again.
      loaded = null;
      throw error;
    });
  return loaded;
}

/**
 * The emoji picker (design § Emoji, plan § 8.2): a `Popover` named «Эмодзи»
 * with «Поиск эмодзи» and a grid of plain buttons, 8 columns, one tab stop.
 * ←→ move by one, ↑↓ by a row, ↑ from the first row to the search; Enter in the
 * search picks the first match, Enter or Space on an emoji picks it. The data is
 * bundled, loaded on first open: no request, no image, system glyphs.
 */
export function EmojiPopover({ anchorEl, copy, failure, onPick, onClose }: EmojiPopoverProps) {
  const open = anchorEl !== null;
  const [data, setData] = useState<EmojiData | null>(null);
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);
  const grid = useRef<HTMLDivElement>(null);
  const search = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!open || data !== null) return undefined;
    let current = true;
    loadEmoji().then(
      (loadedData) => {
        if (current) setData(loadedData);
      },
      () => {
        if (current) setFailed(true);
      },
    );
    return () => {
      current = false;
    };
  }, [open, data, attempt]);

  const matches = useMemo(() => (data ? searchEmoji(data.emoji, query) : []), [data, query]);
  const grouped = query.trim().length === 0;
  const activeIndex = Math.min(active, Math.max(matches.length - 1, 0));

  const focusAt = (index: number) => {
    setActive(index);
    grid.current?.querySelector<HTMLElement>(`[data-emoji-index="${index}"]`)?.focus();
  };

  const onGridKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    const target = (event.target as HTMLElement).closest<HTMLElement>('[data-emoji-index]');
    if (!target) return;
    const index = Number(target.dataset.emojiIndex);
    const groupOf = (i: number) => matches[i]?.[3];
    const groupStart = (i: number) => {
      let start = i;
      while (start > 0 && groupOf(start - 1) === groupOf(i)) start--;
      return start;
    };

    let next: number | null = null;
    switch (event.key) {
      case 'ArrowRight':
        next = Math.min(index + 1, matches.length - 1);
        break;
      case 'ArrowLeft':
        next = Math.max(index - 1, 0);
        break;
      case 'ArrowDown': {
        const below = index + COLUMNS;
        if (below < matches.length && (!grouped || groupOf(below) === groupOf(index))) {
          next = below;
        } else {
          // The next group's first row, same column when it has one.
          const nextStart = matches.findIndex((_, i) => i > index && groupOf(i) !== groupOf(index));
          if (grouped && nextStart >= 0) {
            const column = (index - groupStart(index)) % COLUMNS;
            next = Math.min(nextStart + column, matches.length - 1);
          } else {
            next = Math.min(matches.length - 1, index);
          }
        }
        break;
      }
      case 'ArrowUp': {
        const above = index - COLUMNS;
        if (above >= 0 && (!grouped || groupOf(above) === groupOf(index))) {
          next = above;
        } else if (grouped && groupStart(index) > 0) {
          const column = (index - groupStart(index)) % COLUMNS;
          const previousStart = groupStart(groupStart(index) - 1);
          const rows = Math.floor((groupStart(index) - 1 - previousStart) / COLUMNS);
          next = Math.min(previousStart + rows * COLUMNS + column, groupStart(index) - 1);
        } else {
          event.preventDefault();
          search.current?.focus();
          return;
        }
        break;
      }
      default:
        return;
    }
    event.preventDefault();
    focusAt(next);
  };

  const close = (reason: 'escape' | 'other') => {
    setQuery('');
    setActive(0);
    onClose(reason);
  };

  const pick = (emoji: string) => {
    setQuery('');
    setActive(0);
    onPick(emoji);
  };

  const renderButton = (entry: EmojiEntry, index: number) => (
    // A plain element, styled once by the grid's selector: some 1 600 of them
    // would each cost an emotion style computation otherwise.
    <button
      key={`${entry[0]}-${index}`}
      type="button"
      data-emoji-index={index}
      tabIndex={index === activeIndex ? 0 : -1}
      aria-label={entry[1]}
      title={entry[1]}
      onClick={() => pick(entry[0])}
      onFocus={() => setActive(index)}
    >
      {entry[0]}
    </button>
  );

  const gridSx = {
    display: 'grid',
    gridTemplateColumns: `repeat(${COLUMNS}, max-content)`,
    justifyContent: 'start',
  } as const;

  let content;
  if (failed) {
    content = (
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
            {failure.retry}
          </Button>
        }
      >
        {failure.message}
      </Alert>
    );
  } else if (data === null) {
    content = (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
        <CircularProgress size={24} />
      </Box>
    );
  } else if (matches.length === 0) {
    content = (
      <Typography variant="body2" color="text.secondary" sx={{ p: 1 }}>
        {copy.empty}
      </Typography>
    );
  } else if (!grouped) {
    content = <Box sx={gridSx}>{matches.map(renderButton)}</Box>;
  } else {
    // Groups by name, each its own grid; the buttons keep one running index.
    const sections: { group: number; items: { entry: EmojiEntry; index: number }[] }[] = [];
    matches.forEach((entry, index) => {
      const last = sections[sections.length - 1];
      if (last && last.group === entry[3]) last.items.push({ entry, index });
      else sections.push({ group: entry[3], items: [{ entry, index }] });
    });
    content = sections.map((section) => (
      <Box key={section.group} sx={{ contentVisibility: 'auto', containIntrinsicSize: '0 160px' }}>
        <Typography
          variant="caption"
          color="text.secondary"
          component="div"
          sx={{ px: 0.5, pt: 1 }}
        >
          {data.groups[section.group]}
        </Typography>
        <Box sx={gridSx}>{section.items.map(({ entry, index }) => renderButton(entry, index))}</Box>
      </Box>
    ));
  }

  return (
    <Popover
      open={open}
      anchorEl={anchorEl}
      onClose={(_event, reason) => close(reason === 'escapeKeyDown' ? 'escape' : 'other')}
      disableRestoreFocus
      anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
      slotProps={{
        paper: {
          role: 'dialog',
          'aria-label': copy.title,
          sx: { width: 'min(340px, calc(100vw - 32px))', p: 1 },
        },
      }}
    >
      <TextField
        autoFocus
        fullWidth
        size="small"
        placeholder={copy.search}
        value={query}
        inputRef={search}
        onChange={(event) => {
          setQuery(event.target.value);
          setActive(0);
        }}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            const first = matches[0];
            if (first) pick(first[0]);
          } else if (event.key === 'ArrowDown' && matches.length > 0) {
            event.preventDefault();
            focusAt(0);
          }
        }}
        slotProps={{ htmlInput: { 'aria-label': copy.search, autoComplete: 'off' } }}
      />
      <Box
        ref={grid}
        onKeyDown={onGridKeyDown}
        sx={(theme) => ({
          mt: 1,
          maxHeight: 280,
          overflowY: 'auto',
          '& button[data-emoji-index]': {
            font: 'inherit',
            fontSize: '1.25rem',
            lineHeight: 1,
            width: theme.density.controlHeight,
            height: theme.density.controlHeight,
            border: 0,
            borderRadius: `${theme.shape.borderRadius}px`,
            background: 'transparent',
            cursor: 'pointer',
            '&:hover': { backgroundColor: theme.palette.action.hover },
            '&:focus-visible': {
              outline: `${theme.spacing(0.25)} solid ${theme.palette.primary.main}`,
            },
          },
        })}
      >
        {content}
      </Box>
    </Popover>
  );
}
