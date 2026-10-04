import useMediaQuery from '@mui/material/useMediaQuery';
import { useTheme } from '@mui/material/styles';
import type { WidthClass } from '../theme/tokens';

/**
 * The window's width class (design § 3.6): `wide` from the theme's `lg`
 * (1280 px), `medium` from `md` (1024 px), `narrow` below. Read without the
 * server-render default, so the first render already has the right layout.
 */
export function useWidthClass(): WidthClass {
  const theme = useTheme();
  const wide = useMediaQuery(theme.breakpoints.up('lg'), { noSsr: true });
  const medium = useMediaQuery(theme.breakpoints.up('md'), { noSsr: true });
  if (wide) {
    return 'wide';
  }
  return medium ? 'medium' : 'narrow';
}
