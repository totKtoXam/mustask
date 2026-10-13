import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Skeleton from '@mui/material/Skeleton';
import { useTheme } from '@mui/material/styles';
import { lineHeightPx } from '../fields/fieldStyles';

/**
 * The name of every loading region: one `role="status"` per region, the shapes inside it
 * hidden from assistive technology (tech-015 design § 5.12).
 */
export const LOADING_LABEL = 'Загрузка данных';

/** A loading region: one status that names itself, with its shapes `aria-hidden`. */
export function LoadingRegion({ children }: { children: ReactNode }) {
  return (
    <Box role="status" aria-label={LOADING_LABEL}>
      <Box aria-hidden="true">{children}</Box>
    </Box>
  );
}

/** A rounded shape: `width` a px number or a CSS size, `height` in px. */
export function Shape({ width = '100%', height }: { width?: number | string; height: number }) {
  return <Skeleton variant="rounded" sx={{ width, height, maxWidth: '100%', flexShrink: 0 }} />;
}

/** The avatar slot of a person, as a circle: `layout.mark.avatarInline`. */
export function Circle() {
  const size = useTheme().layout.mark.avatarInline;
  return <Skeleton variant="circular" sx={{ width: size, height: size, flexShrink: 0 }} />;
}

/**
 * One field's shape, sized by the field it mirrors (`layout.field`): `top` is a label
 * line of 16, `gap`, a value box of 28 and `gap` — 48; `start` is the band's row of 32 with
 * the label column before the value.
 */
export function FieldShape({ placement }: { placement: 'top' | 'start' }) {
  const theme = useTheme();
  const { field, card } = theme.layout;
  const labelLine = lineHeightPx(theme, theme.typography.caption);
  if (placement === 'start') {
    return (
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: `${card.bandLabelWidth}px minmax(0, 1fr)`,
          alignItems: 'center',
          minHeight: card.bandRow,
        }}
      >
        <Shape width={56} height={labelLine - 6} />
        <Shape height={field.box} />
      </Box>
    );
  }
  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', height: labelLine, mb: `${field.gap}px` }}>
        <Shape width={96} height={labelLine - 6} />
      </Box>
      <Box sx={{ mb: `${field.gap}px` }}>
        <Shape height={field.box} />
      </Box>
    </Box>
  );
}
