import { createTheme, type Theme } from '@mui/material/styles';
import type { TypographyVariantsOptions } from '@mui/material/styles';
import { ruRU } from '@mui/material/locale';
import { components } from './components';
import {
  baseFontSize,
  cardShadow,
  breakpointValues,
  colourRoles,
  density,
  focusRing,
  fontFamily,
  htmlFontSize,
  layout,
  mediumWeight,
  semanticColours,
  shellPalette,
  typeScale,
} from './tokens';
import { createWorkTrackingLookups } from './workTracking';

type ScaleEntry = { size: number; lineHeight: number; weight: number };

function rem(px: number): string {
  return `${px / htmlFontSize}rem`;
}

function variant({ size, lineHeight, weight }: ScaleEntry) {
  return { fontSize: rem(size), lineHeight: lineHeight / size, fontWeight: weight };
}

// Every variant is sized explicitly: `typography.fontSize` alone leaves
// `body1` at 16 px, because MUI builds `body1` from 16 (plan § 9, D-2).
const typography: TypographyVariantsOptions = {
  fontFamily,
  fontSize: baseFontSize,
  // `fontWeightMedium` is what `sx` and the shell's `fontWeight: 'fontWeightMedium'` read.
  fontWeightMedium: mediumWeight,
  htmlFontSize,
  h1: variant(typeScale.h1),
  h2: variant(typeScale.h2),
  h3: variant(typeScale.h3),
  body1: variant(typeScale.body1),
  body2: variant(typeScale.body2),
  subtitle2: variant(typeScale.subtitle2),
  caption: variant(typeScale.caption),
  button: { ...variant(typeScale.button), textTransform: 'none' },
  tableCell: variant(typeScale.tableCell),
};

const baseTheme = createTheme(
  {
    breakpoints: { values: breakpointValues },
    palette: {
      mode: 'light',
      primary: {
        main: colourRoles.accent,
        dark: colourRoles.accentDark,
        contrastText: colourRoles.accentContrastText,
      },
      // The neutral text colour: selection controls and progress default to
      // it and stay off the accent (design § 2.2).
      secondary: { main: colourRoles.textPrimary, contrastText: colourRoles.content },
      error: { main: semanticColours.error, contrastText: semanticColours.contrastText },
      warning: { main: semanticColours.warning, contrastText: semanticColours.contrastText },
      success: { main: semanticColours.success, contrastText: semanticColours.contrastText },
      info: { main: semanticColours.info, contrastText: semanticColours.contrastText },
      text: { primary: colourRoles.textPrimary, secondary: colourRoles.textSecondary },
      background: { default: colourRoles.applicationBackground, paper: colourRoles.content },
      divider: colourRoles.divider,
      action: { hover: colourRoles.rowHover },
      shell: shellPalette,
    },
    typography,
    shape: { borderRadius: 4 },
    mixins: {
      // MUI's default toolbar is 56/64 px, responsive; the top bar is 48 px
      // at every width (BR-3).
      toolbar: { minHeight: layout.topBarHeight },
      visuallyHidden: {
        border: 0,
        clip: 'rect(0 0 0 0)',
        height: '1px',
        margin: '-1px',
        overflow: 'hidden',
        padding: 0,
        position: 'absolute',
        whiteSpace: 'nowrap',
        width: '1px',
      },
    },
    layout,
    density,
    cardShadow,
    // MUI's own keyboard focus ring: a solid accent outline of `focusRing`
    // on every button, list item, tab, link, sort label and icon button,
    // inset automatically where a clipping ancestor would cut it (BR-5).
    focusVisible: { outlineWidth: focusRing.width, outlineOffset: focusRing.offset },
    // Transitions — the overlay's slide, menus, the skeleton's pulse — honour
    // `prefers-reduced-motion: reduce` (design § 2.6, US-4).
    motion: { reducedMotion: 'system' },
    components,
  },
  // Russian texts of MUI's own components: «1–10 из 25» (NFR Localization).
  ruRU,
);

/** The panel's one theme: `app/` provides it, everything else reads it. */
export const theme: Theme = Object.assign(baseTheme, {
  workTracking: createWorkTrackingLookups(baseTheme.palette.text),
});
