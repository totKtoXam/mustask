import { createSvgIcon } from '@mui/material/SvgIcon';

/**
 * The user story's open book (feat-027 review V-8). `MenuBook`, the icon the
 * story had, draws a solid left page and a right page of horizontal text
 * lines; at the backlog's 16 px the right half read as a list, a different
 * glyph from the reference's two solid pages. This one is a single path of two
 * closed subpaths in the 24 × 24 box — each a page 9 units wide and 13 tall
 * with a curved top and bottom edge, the two 2 units apart at the spine.
 */
export const StoryIcon = createSvgIcon(
  <path d="M2 5.5c2.5-.9 5.5-.9 9 .5v13c-3.5-1.4-6.5-1.4-9-.5zM22 5.5c-2.5-.9-5.5-.9-9 .5v13c3.5-1.4 6.5-1.4 9-.5z" />,
  'Story',
);
