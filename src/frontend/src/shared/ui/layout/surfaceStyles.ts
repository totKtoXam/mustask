import type { Theme } from '@mui/material/styles';

export type SurfaceVariant = 'card' | 'band';

/** `none` is the content's own; `dense` a filter band's 12 × 16; `form` a form column's 24. */
export type SurfacePadding = 'none' | 'dense' | 'form';

const PADDING = {
  none: (_theme: Theme) => 0,
  dense: (theme: Theme) => theme.spacing(1.5, 2),
  form: (theme: Theme) => theme.spacing(3),
} as const;

/**
 * The look of a surface, in one place: `Surface` draws it, and `PanelShell` — which must
 * also declare a container on the same element — draws it on its own `Paper`.
 *
 * `card`: the paper colour with the reference's soft glow (`theme.cardShadow`) and the
 * theme's corner. `band`: the application background, flat and square.
 */
export function surfaceSx(theme: Theme, variant: SurfaceVariant, padding: SurfacePadding = 'none') {
  return {
    padding: PADDING[padding](theme),
    ...(variant === 'card'
      ? {
          backgroundColor: theme.palette.background.paper,
          boxShadow: theme.cardShadow,
          borderRadius: `${theme.shape.borderRadius}px`,
        }
      : {
          backgroundColor: theme.palette.background.default,
          boxShadow: 'none',
          borderRadius: 0,
        }),
  };
}
