import { createSvgIcon } from '@mui/material/SvgIcon';

// The backlog's hairline glyphs (feat-027 review V-5). Material's `ExpandMore`,
// `ChevronRight` and `Add` are filled shapes with a 2-unit stroke, which at the
// panel's 16–20 px read about twice as heavy as the reference's 1 px lines. Each
// is one open stroked path in the 24 × 24 box, 1.5 units thick — about 1.25 px
// at 20 px and 1 px at 16 px. Slice-local, as `BacklogIcon` is: only this
// slice's table and head draw them.

const STROKE = 1.5;

/** A chevron pointing down: an expanded row, and the team drop-down. About 12.5 × 6 px at 20 px. */
export const ThinChevronDownIcon = createSvgIcon(
  <path d="M4.5 8.5 12 16l7.5-7.5" fill="none" stroke="currentColor" strokeWidth={STROKE} />,
  'ThinChevronDown',
);

/** A chevron pointing right: a collapsed row. */
export const ThinChevronRightIcon = createSvgIcon(
  <path d="M8.5 4.5 16 12l-7.5 7.5" fill="none" stroke="currentColor" strokeWidth={STROKE} />,
  'ThinChevronRight',
);

/** A cross: the row's «+». About 9 px across at 16 px. */
export const ThinAddIcon = createSvgIcon(
  <path d="M12 5v14M5 12h14" fill="none" stroke="currentColor" strokeWidth={STROKE} />,
  'ThinAdd',
);
