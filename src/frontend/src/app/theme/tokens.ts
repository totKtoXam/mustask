/**
 * Every appearance value of the panel: mod-015 design.md § 2.1–§ 2.5.
 *
 * This is the only product file that holds colour literals (BR-1). The check,
 * `src/test/colourLiterals.test.ts`, scans `src/` outside `app/theme/`, the
 * test files (`*.test.*`) and `src/test/`, as BR-1 scopes it; tests may hold
 * literals as fixtures. Components and hooks read these values through the
 * theme object (`palette`, `typography`, `spacing`, `layout`, `density`,
 * `workTracking`), and `theme.ts` is what puts them there. Outside
 * `app/theme/` only types are imported from here (`WidthClass`).
 */

/** Colour roles (design § 2.1, BR-2). */
export const colourRoles = {
  applicationBackground: '#F8F8F8',
  navigation: '#EBEBEB',
  navigationHover: '#E2E2E2',
  navigationSelected: '#DCDCDC',
  content: '#FFFFFF',
  textPrimary: '#242424',
  textSecondary: '#616161',
  accent: '#0F6CBD',
  accentDark: '#0C5598',
  accentContrastText: '#FFFFFF',
  divider: '#E0E0E0',
  inputBorder: '#8A8A8A',
  tableHeader: '#F3F3F3',
  rowHover: '#F0F0F0',
  rowSelected: '#E5F0FA',
} as const;

/** Semantic colours: meaning only, darker than MUI's defaults (ADR-0007). */
export const semanticColours = {
  error: '#C42B1C',
  warning: '#9D5D00',
  success: '#107C10',
  info: '#01579B',
  contrastText: '#FFFFFF',
} as const;

/** The appearance token set a process seed stores per type (BR-4, D-16). */
export const APPEARANCE_TOKENS = ['epic', 'feature', 'story', 'task', 'bug'] as const;
export type AppearanceToken = (typeof APPEARANCE_TOKENS)[number];

/** doc-007's state-category codes; the theme keys on the code, never on a label (BR-4). */
export const CATEGORY_CODES = [
  'Proposed',
  'InProgress',
  'Resolved',
  'Completed',
  'Removed',
] as const;
export type CategoryCode = (typeof CATEGORY_CODES)[number];

/** Type colours (design § 2.3). Icons live in `workTracking.ts`. */
export const typeColours: Record<AppearanceToken, string> = {
  epic: '#C05600',
  feature: '#773B93',
  story: '#00739E',
  task: '#8A6A00',
  bug: '#B4233A',
};

/** State-category colours (design § 2.3). */
export const categoryColours: Record<CategoryCode, string> = {
  Proposed: '#707070',
  InProgress: '#005A9E',
  Resolved: '#00796B',
  Completed: '#107C10',
  Removed: '#A4262C',
};

/** `palette.shell` — the shell's own surfaces (design § 2.1). */
export interface ShellPalette {
  navigation: string;
  navigationHover: string;
  navigationSelected: string;
  inputBorder: string;
  tableHeader: string;
  rowSelected: string;
}

export const shellPalette: ShellPalette = {
  navigation: colourRoles.navigation,
  navigationHover: colourRoles.navigationHover,
  navigationSelected: colourRoles.navigationSelected,
  inputBorder: colourRoles.inputBorder,
  tableHeader: colourRoles.tableHeader,
  rowSelected: colourRoles.rowSelected,
};

/** The width classes of BR-10 and D-8 (design § 3.6). */
export type WidthClass = 'wide' | 'medium' | 'narrow';

/** `theme.layout` — fixed layout widths in px (design § 2.5). */
export interface LayoutTokens {
  topBarHeight: number;
  navigationWidth: number;
  railWidth: number;
  settingsNavigationWidth: number;
  sidePaneWidth: number;
  formColumnWidth: number;
  boardColumnWidth: number;
  boardColumnCollapsedWidth: number;
  authCardWidth: number;
  avatarSize: number;
  categoryDot: number;
  contextNameMaxWidth: Record<WidthClass, number>;
  trailSegmentMaxWidth: number;
  /** The switcher's loading placeholder per level (design § 5.4: 120 × 24 px). */
  contextSkeletonWidth: number;
  contextSkeletonHeight: number;
  /** The accent bar on the left edge of a selected navigation entry (design § 2.6). */
  selectedMarkerWidth: number;
  /**
   * A `divider` rule between shell surfaces: under the top bar (design § 3.2)
   * and on the left of «Меню настроек» (design § 4.4).
   */
  dividerWidth: number;
}

export const layout: LayoutTokens = {
  topBarHeight: 48,
  navigationWidth: 240,
  railWidth: 48,
  settingsNavigationWidth: 240,
  sidePaneWidth: 360,
  formColumnWidth: 480,
  boardColumnWidth: 280,
  boardColumnCollapsedWidth: 40,
  authCardWidth: 400,
  avatarSize: 32,
  categoryDot: 8,
  contextNameMaxWidth: { wide: 200, medium: 160, narrow: 96 },
  trailSegmentMaxWidth: 200,
  contextSkeletonWidth: 120,
  contextSkeletonHeight: 24,
  selectedMarkerWidth: 3,
  dividerWidth: 1,
};

/** `theme.density` — one dense layout for everyone (BR-3). */
export interface DensityTokens {
  /** At most 40 px (BR-3). */
  registryRowHeight: number;
  /** Buttons and small text fields' minimum target. */
  controlHeight: number;
  /** Tabs' minimum height (design § 2.6). */
  tabHeight: number;
}

export const density: DensityTokens = {
  registryRowHeight: 36,
  controlHeight: 32,
  tabHeight: 40,
};

/** Breakpoints (design § 2.5): the widths of BR-10 and D-8, not MUI's defaults. */
export const breakpointValues = { xs: 0, sm: 640, md: 1024, lg: 1280, xl: 1536 } as const;

/** Type scale in px (design § 2.4): size, line height, weight. */
export const typeScale = {
  h1: { size: 20, lineHeight: 28, weight: 500 },
  h2: { size: 16, lineHeight: 24, weight: 500 },
  h3: { size: 14, lineHeight: 20, weight: 500 },
  body1: { size: 14, lineHeight: 20, weight: 400 },
  body2: { size: 14, lineHeight: 20, weight: 400 },
  subtitle2: { size: 12, lineHeight: 16, weight: 500 },
  caption: { size: 12, lineHeight: 16, weight: 400 },
  button: { size: 14, lineHeight: 20, weight: 500 },
  /** The initials inside the user menu's avatar (design § 3.2). */
  avatarInitials: { size: 13, lineHeight: 16, weight: 500 },
} as const;

export const fontFamily = 'Roboto, "Segoe UI", system-ui, Arial, sans-serif';

/** The root font size MUI converts px to rem against. */
export const htmlFontSize = 16;

/** The base body text size (BR-3). */
export const baseFontSize = 14;

/** The tooltip's hover delay (design § 2.6). */
export const tooltipEnterDelayMs = 500;

/** The focus ring: 2 px accent outline, offset 2 px (design § 2.6, BR-5). */
export const focusRing = { width: 2, offset: 2 } as const;
