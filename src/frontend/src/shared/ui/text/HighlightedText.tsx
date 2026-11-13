import { Fragment, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import { normaliseRanges, rangesOfQuery, type HighlightRange } from './highlightRanges';

export type { HighlightRange };

export interface HighlightedTextProps {
  /** The plain text. It is drawn as text, never as markup. */
  text: string;
  /** Pieces to emphasise, as the server found them; wins over `query`. */
  ranges?: readonly HighlightRange[];
  /** Emphasise every occurrence of this text; case-insensitive, «ё» equals «е». */
  query?: string;
}

/**
 * A text with its matched pieces emphasised (feat-065 design § 1): each piece is a
 * `<mark>` built from a range of the text, so nothing is parsed as markup. The emphasis
 * is bold plus the `search.match` tint, so colour is not the only carrier. With neither
 * `ranges` nor a matching `query` the text is drawn as it is.
 */
export function HighlightedText({ text, ranges, query }: HighlightedTextProps) {
  const marked = normaliseRanges(ranges ?? rangesOfQuery(text, query ?? ''), text.length);
  if (marked.length === 0) return <>{text}</>;
  const parts: ReactNode[] = [];
  let cursor = 0;
  marked.forEach(([start, end], index) => {
    if (start > cursor)
      parts.push(<Fragment key={`t${index}`}>{text.slice(cursor, start)}</Fragment>);
    parts.push(
      <Box
        key={`m${index}`}
        component="mark"
        sx={(theme) => ({
          backgroundColor: theme.palette.search.match,
          color: 'inherit',
          fontWeight: theme.typography.fontWeightMedium,
          padding: '0 1px',
          borderRadius: '2px',
        })}
      >
        {text.slice(start, end)}
      </Box>,
    );
    cursor = end;
  });
  if (cursor < text.length) parts.push(<Fragment key="tail">{text.slice(cursor)}</Fragment>);
  return <>{parts}</>;
}
