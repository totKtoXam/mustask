// MUI module augmentation for the panel's own theme keys (design § 2.1,
// § 2.3, § 2.5). Types only: `shared/ui` and the slices type-check against
// `theme.palette.shell`, `theme.layout`, `theme.density`,
// `theme.workTracking` and `theme.mixins.visuallyHidden` without importing
// `app/` — they read the values through `useTheme()` or an `sx` callback.
import type { CSSProperties } from '@mui/material/styles/createMixins';
import type { DensityTokens, LayoutTokens, ShellPalette } from './tokens';
import type { WorkTrackingLookups } from './workTracking';

declare module '@mui/material/styles' {
  interface Palette {
    shell: ShellPalette;
  }
  interface PaletteOptions {
    shell?: ShellPalette;
  }
  interface Theme {
    layout: LayoutTokens;
    density: DensityTokens;
    workTracking: WorkTrackingLookups;
  }
  interface ThemeOptions {
    layout?: LayoutTokens;
    density?: DensityTokens;
  }
  interface Mixins {
    /** Hides content visually and keeps it for assistive technology. */
    visuallyHidden: CSSProperties;
  }
}
