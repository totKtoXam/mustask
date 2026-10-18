import { useEffect, useLayoutEffect, useRef, useState, type KeyboardEvent, type Ref } from 'react';
import Box from '@mui/material/Box';
import { InlineTextField } from '../../../shared/ui';
import { SEARCH_DEBOUNCE_MS } from '../hooks/useDebouncedValue';
import { SEARCH_MAX_LENGTH, filterTexts } from '../lib/workItemFilter';

/**
 * «Поиск» of the list's band (feat-003 US-3, BR-WS-9; design § 2.1): the kit's `InlineTextField`,
 * label above, the frame always drawn, hint and rest text «Заголовок или ID», at most 255
 * characters. The page keeps the address's keyword (`value`, trimmed); this field keeps the text
 * typed. The address takes the **trimmed** text after a pause of 250 ms, on Enter and on leaving
 * the field (D-3), and focus stays in the input throughout. A blank text sets nothing. When the
 * address's keyword changes for any other reason (Back, a link, a clear), the field takes the
 * address's text; a longer keyword in a link is shown whole. The field never matches, highlights or
 * cuts the text: the server judges it.
 *
 * The router applies a change of the address as a transition, so the address can answer a write a
 * moment later than the typing goes on. The field therefore remembers the keywords it wrote and
 * does not take such an echo for another's change: the text typed meanwhile is never overwritten.
 */
export function KeywordFilter({
  value,
  onChange,
  ref,
}: {
  /** The address's keyword; `""` for none. */
  value: string;
  /** The trimmed keyword the address should carry; `""` for none. */
  onChange: (keyword: string) => void;
  /** Focuses the input («Очистить фильтры» sends the focus here). */
  ref?: Ref<HTMLInputElement>;
}) {
  const [text, setText] = useState(value);
  // The keyword the address carried when this field last looked, and the keywords this field wrote
  // that the address has not answered yet, oldest first.
  const [seen, setSeen] = useState(value);
  const [written, setWritten] = useState<string[]>([]);
  if (value !== seen) {
    setSeen(value);
    const echoed = written.lastIndexOf(value);
    if (echoed >= 0) {
      setWritten(written.slice(echoed + 1));
    } else {
      // Another change: Back, a link, a clear. The field shows what the address says.
      setWritten([]);
      setText(value);
    }
  }
  // What the address will carry once every write has been answered.
  const intended = written.length > 0 ? (written[written.length - 1] ?? seen) : seen;

  const latestOnChange = useRef(onChange);
  useLayoutEffect(() => {
    latestOnChange.current = onChange;
  });

  function apply(keyword: string) {
    if (keyword !== intended) {
      setWritten((before) => [...before, keyword]);
      latestOnChange.current(keyword);
    }
  }

  useEffect(() => {
    const keyword = text.trim();
    if (keyword === intended) {
      return undefined;
    }
    const timer = setTimeout(() => {
      setWritten((before) => [...before, keyword]);
      latestOnChange.current(keyword);
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [text, intended]);

  return (
    <Box
      sx={(theme) => ({ width: theme.spacing(30), maxWidth: '100%' })}
      // The field's own `onBlur` is not wired by the kit's text field (it reaches the input only
      // through the legacy `inputProps`), but a blur bubbles: leaving the input is seen here.
      onBlur={() => {
        // Leaving shows the trimmed text; a text of spaces reads blank again. Text that is already
        // trimmed is not set again: a no-op update queued here would be replayed over the field's
        // own reset when the address drops the keyword (a clear, a link), and the keyword would
        // come back 250 ms later (qa D-F3).
        const keyword = text.trim();
        if (keyword !== text) {
          setText(keyword);
        }
        apply(keyword);
      }}
    >
      <InlineTextField
        ref={ref}
        label={filterTexts.searchLabel}
        frame="always"
        hint={filterTexts.searchHint}
        emptyText={filterTexts.searchHint}
        maxLength={SEARCH_MAX_LENGTH}
        value={text}
        onChange={setText}
        inputProps={{
          onKeyDown: (event: KeyboardEvent<HTMLInputElement>) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              apply(text.trim());
            }
          },
        }}
      />
    </Box>
  );
}
