import type { ElementType } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import Tooltip from '@mui/material/Tooltip';

export interface TruncatedTextProps {
  /** The whole text: the DOM keeps it entire, so the accessible name is complete. */
  children: string;
  /**
   * `title` (default) — the full text in the native hover title, as the reference
   * does; `tooltip` — a MUI tooltip after the theme's delay. One position per screen.
   */
  hint?: 'title' | 'tooltip';
  /** Given, the text is a router link. */
  to?: string;
  /**
   * With `to`: `plain` (default) takes the surrounding colour and is underlined on
   * hover only — the backlog's title; `link` is the theme's link colour.
   */
  look?: 'plain' | 'link';
  /** The element drawn, default `span`; ignored when `to` is given. */
  component?: ElementType;
}

const TRUNCATE = {
  display: 'block',
  minWidth: 0,
  maxWidth: '100%',
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap',
} as const;

/**
 * A long text cut to one line with an ellipsis (tech-015 design § 5.5): `min-width: 0`
 * so a flex or grid cell lets it shrink, the full text in the native `title` or in
 * a tooltip. Never copy an ellipsis `sx` into a slice.
 */
export function TruncatedText({
  children,
  hint = 'title',
  to,
  look = 'plain',
  component = 'span',
}: TruncatedTextProps) {
  const title = hint === 'title' ? children : undefined;
  const text = to ? (
    <Link
      component={RouterLink}
      to={to}
      title={title}
      color={look === 'plain' ? 'inherit' : 'primary'}
      underline={look === 'plain' ? 'hover' : 'always'}
      sx={TRUNCATE}
    >
      {children}
    </Link>
  ) : (
    <Box component={component} title={title} sx={TRUNCATE}>
      {children}
    </Box>
  );
  return hint === 'tooltip' ? <Tooltip title={children}>{text}</Tooltip> : text;
}
