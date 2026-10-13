import { createSvgIcon } from '@mui/material/SvgIcon';

// The kit's hairline glyphs (tech-015 design § 5.1; the first three are the
// backlog's, feat-027 review V-5). Material's `ExpandMore`, `ChevronRight`,
// `Add`, `Close`, `Refresh`… are filled shapes with a 2-unit stroke, which at
// the panel's 16–20 px read about twice as heavy as the reference's 1 px lines.
// Each is one open stroked path in the 24 × 24 box, 1.5 units thick — about
// 1.25 px at 20 px and 1 px at 16 px; «⋮» is the one filled glyph, three dots.
//
// `createSvgIcon` returns a `memo(forwardRef(…))` object and sets
// `data-testid="<name>Icon"` and `aria-hidden`; the exports keep the `Icon`
// suffix. A glyph takes its colour from the button that holds it
// (`IconAction`), and its size from the button's token (`layout.iconAction`).
// A new glyph is defined here and nowhere else (guard rule KIT-14).

const STROKE = 1.5;

function line(path: string) {
  return <path d={path} fill="none" stroke="currentColor" strokeWidth={STROKE} />;
}

/**
 * A chevron pointing down: an expanded row, a drop-down and the split button's
 * arrow. About 12.5 × 6 px at 20 px.
 */
export const ThinChevronDownIcon = createSvgIcon(line('M4.5 8.5 12 16l7.5-7.5'), 'ThinChevronDown');

/** A chevron pointing right: a collapsed row. */
export const ThinChevronRightIcon = createSvgIcon(
  line('M8.5 4.5 16 12l-7.5 7.5'),
  'ThinChevronRight',
);

/** A cross: the row's «+». About 9 px across at 16 px. */
export const ThinAddIcon = createSvgIcon(line('M12 5v14M5 12h14'), 'ThinAdd');

/** A bar: the hierarchy header's «−», as light as the «+» beside it. */
export const ThinRemoveIcon = createSvgIcon(line('M5 12h14'), 'ThinRemove');

/** A diagonal cross: «Закрыть» and a tag's remove. */
export const ThinCloseIcon = createSvgIcon(line('M6 6l12 12M18 6 6 18'), 'ThinClose');

/** Two arrowheads pointing outwards, «⤢»: «Развернуть на страницу». */
export const ThinExpandIcon = createSvgIcon(
  line('M14 4h6v6M20 4l-7 7M10 20H4v-6M4 20l7-7'),
  'ThinExpand',
);

/** Three dots in a column, «⋮» — the one filled glyph of the set. */
export const ThinMoreVerticalIcon = createSvgIcon(
  <path d="M12 7.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm0 6a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm0 6a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z" />,
  'ThinMoreVertical',
);

/** A circle open at the upper right, with an arrowhead corner at its end: «Обновить». */
export const ThinRefreshIcon = createSvgIcon(
  line('M19.5 12a7.5 7.5 0 1 1-2.2-5.3M13.3 6.7h4v-4'),
  'ThinRefresh',
);

/** A left arrowhead and a stem that turns back: «Отменить все изменения». */
export const ThinUndoIcon = createSvgIcon(
  line('M9 14 4.5 9.5 9 5M4.5 9.5H15a4.5 4.5 0 0 1 0 9h-4'),
  'ThinUndo',
);

/** A disk with its shutter and its label: the Save button's icon. */
export const ThinSaveIcon = createSvgIcon(
  line(
    'M5 4.5h11.5l3 3V19a.5.5 0 0 1-.5.5H5a.5.5 0 0 1-.5-.5V5a.5.5 0 0 1 .5-.5zM8 4.5V9h7V4.5M7.5 19.5v-6h9v6',
  ),
  'ThinSave',
);

/**
 * A head and open shoulders: the empty person of `PersonLabel` and
 * `PersonField`, drawn at `layout.mark.personGlyph` inside the avatar's slot.
 * The reference's ink is 9 × 12 CSS px, taller than wide: at 16 px this path's
 * ink is 9 × 12 (the design's first path, 14 × 15 units, came out 9 × 10).
 */
export const ThinPersonIcon = createSvgIcon(
  line('M12 10.75a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7zM6 20.25c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5'),
  'ThinPerson',
);
