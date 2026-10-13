import type { ElementType, ReactNode, Ref } from 'react';
import Paper from '@mui/material/Paper';
import type { PassThrough } from '../passThrough';
import { surfaceSx, type SurfacePadding, type SurfaceVariant } from './surfaceStyles';

export type { SurfacePadding, SurfaceVariant };

export interface SurfaceProps extends PassThrough<HTMLElement> {
  /** `card`: white with the soft glow. `band`: the application background, flat and square. */
  variant: SurfaceVariant;
  /** Default `none`: the content's own. `dense` is 12 × 16, `form` 24. */
  padding?: SurfacePadding;
  /** The element drawn: `form`, `section`… Default `div`. */
  component?: ElementType;
  children?: ReactNode;
  ref?: Ref<HTMLDivElement>;
}

/**
 * Any panel on the page (tech-015 design § 5.8): the card, a band, a form's column, a
 * filter band. MUI's `Paper` at elevation 0, so no outline and no elevation shadow:
 * the `card` draws the reference's soft glow (`theme.cardShadow`) with the theme's
 * 4 px corners, the `band` the application background.
 */
export function Surface({
  variant,
  padding = 'none',
  component = 'div',
  children,
  ref,
  ...passThrough
}: SurfaceProps) {
  return (
    <Paper
      ref={ref}
      component={component}
      elevation={0}
      square={variant === 'band'}
      sx={(theme) => surfaceSx(theme, variant, padding)}
      {...passThrough}
    >
      {children}
    </Paper>
  );
}
