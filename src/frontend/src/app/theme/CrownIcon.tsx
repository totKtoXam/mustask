import { createSvgIcon } from '@mui/material/SvgIcon';

/**
 * The epic's crown (feat-027 design § 2.4). `@mui/icons-material` has none, so
 * the theme builds one with MUI's own `createSvgIcon` from one closed path in
 * the 24 × 24 box: a rectangular body with vertical sides, three spikes of
 * nearly equal height — the middle tip at y 3, the outer tips at y 5 — and V
 * notches that run down to y 12. At the 16 px of the backlog's title cell it
 * measures about 14 × 12 px, as the reference's crown does.
 */
export const CrownIcon = createSvgIcon(<path d="M1.5 5L7 12l5-9 5 9 5.5-7V21H1.5z" />, 'Crown');
