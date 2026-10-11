import { createSvgIcon } from '@mui/material/SvgIcon';

/**
 * The glyph of the team head on «Бэклоги» (feat-027 design § 3.2): three
 * right-aligned horizontal bars, 13, 18 and 12 units long, 2 thick, 5 apart,
 * with no numerals — `FormatListNumbered` draws «1 2 3» beside its lines, which
 * reads as another glyph. Drawn at 24 px; the navigation entry keeps its own
 * icon. Slice-local: only this page draws it.
 */
export const BacklogIcon = createSvgIcon(
  <path d="M8 6h13v2H8zM3 11h18v2H3zM9 16h12v2H9z" />,
  'Backlog',
);
