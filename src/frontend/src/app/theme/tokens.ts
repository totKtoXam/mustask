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
  /**
   * The reference's blue (feat-027 D-10; was `#0F6CBD`): the filled primary
   * action, the selected tab's underline, the selected row's frame, the
   * focus ring. A white label on it is 4.53:1. As text it is 4.26:1 on the
   * application background, under the 4.5:1 text floor, so links take
   * `accentDark` (`components.ts`, `MuiLink`).
   */
  accent: '#0078D4',
  /** The accent's dark companion: a link's text, the selected row's type icon, hover. */
  accentDark: '#0C5598',
  accentContrastText: '#FFFFFF',
  divider: '#E0E0E0',
  inputBorder: '#8A8A8A',
  tableHeader: '#F3F3F3',
  rowHover: '#F0F0F0',
  rowSelected: '#E5F0FA',
  /**
   * The backlog row's «+» (feat-027 D-10): the reference's pale grey cross, a
   * little darker so it stays findable — 3.5:1 on white, 3.1:1 on the selected
   * row. (D-7 had it orange, the orchestrator's misreading of the screenshot's
   * colour fringing.)
   */
  addAction: '#8A8886',
  /** The grey secondary button of the team head (feat-027 design § 2.5). */
  buttonNeutral: '#EBEBEB',
  buttonNeutralHover: '#E2E2E2',
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

/**
 * Type colours: the reference's own (feat-027 D-10, panel-wide through the one
 * lookup; mod-015 § 2.3 had darker variants). Epic, story and task sit under
 * 3:1 on some surface; the icon repeats the type's name in text, so the
 * contrast test exempts them, with the reason (`contrast.test.ts`). Icons
 * live in `workTracking.ts`.
 */
export const typeColours: Record<AppearanceToken, string> = {
  epic: '#E06C00',
  feature: '#773B93',
  story: '#0098C7',
  task: '#A4880A',
  bug: '#CC293D',
};

/**
 * State-category colours (design § 2.3; D-10 takes the reference's «Новые»
 * `#B2B2B2` and «Завершённые» `#339933`). The dot is followed by the state's
 * name, so «Новые» (about 2:1) is exempt from the 3:1 test; «Завершённые»
 * clears it on every surface.
 */
export const categoryColours: Record<CategoryCode, string> = {
  Proposed: '#B2B2B2',
  InProgress: '#005A9E',
  Resolved: '#00796B',
  Completed: '#339933',
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
  addAction: string;
  buttonNeutral: string;
  buttonNeutralHover: string;
}

export const shellPalette: ShellPalette = {
  navigation: colourRoles.navigation,
  navigationHover: colourRoles.navigationHover,
  navigationSelected: colourRoles.navigationSelected,
  inputBorder: colourRoles.inputBorder,
  tableHeader: colourRoles.tableHeader,
  rowSelected: colourRoles.rowSelected,
  addAction: colourRoles.addAction,
  buttonNeutral: colourRoles.buttonNeutral,
  buttonNeutralHover: colourRoles.buttonNeutralHover,
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
  /** The backlog table and its page's head (feat-027 design § 2.3). */
  backlogTable: BacklogTableTokens;
}

/**
 * `theme.layout.backlogTable` — the backlog table's measures in px (feat-027
 * design § 2.3), taken from the reference screen and checked against the two
 * acceptance windows by `theme.test.tsx`.
 */
export interface BacklogTableTokens {
  hierarchyColumn: number;
  orderColumn: number;
  typeColumn: number;
  /** «Заголовок» takes the rest of the width, and at least this much. */
  titleColumnMin: number;
  stateColumn: number;
  effortColumn: number;
  businessValueColumn: number;
  valueAreaColumn: number;
  /** The indent of one depth. */
  treeIndent: number;
  /** The chevron's box, and the empty box of a row with no children. */
  chevronSlot: number;
  /** The box of a row's «+» and «⋮». */
  rowControl: number;
  /** The two square buttons of the hierarchy header. */
  headerButton: number;
  typeIcon: number;
  /** The tree chevron and the team chevron. */
  chevronGlyph: number;
  /** The glyph of a row's «+» and «⋮». */
  actionGlyph: number;
  /** The glyph of the two square header buttons. */
  headerGlyph: number;
  /** The draggable strip at the row's left edge. */
  gripHitWidth: number;
  /** The dotted line's distance from the card's left edge. */
  gripDotOffset: number;
  /** The dotted mark's height: four 2 px dots 6 px apart, centred on the row. */
  gripMarkHeight: number;
  /** The static separator at a header cell's right edge. */
  separatorHeight: number;
  /** What the page keeps above and below the card: top bar + head + bottom padding. */
  viewportOffset: number;
}

export const backlogTable: BacklogTableTokens = {
  hierarchyColumn: 72,
  orderColumn: 68,
  typeColumn: 184,
  titleColumnMin: 400,
  stateColumn: 104,
  effortColumn: 64,
  businessValueColumn: 64,
  valueAreaColumn: 104,
  treeIndent: 16,
  chevronSlot: 24,
  rowControl: 24,
  headerButton: 24,
  typeIcon: 16,
  chevronGlyph: 20,
  actionGlyph: 16,
  headerGlyph: 18,
  gripHitWidth: 16,
  gripDotOffset: 7,
  gripMarkHeight: 20,
  separatorHeight: 20,
  viewportOffset: 204,
};

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
  backlogTable,
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

/**
 * The weight of emphasis. Segoe UI has no 500 face — a browser draws a
 * requested 500 as Regular — so the step above regular is its Semibold, 600
 * (feat-027 D-7 item 9: the panel's font is Segoe UI).
 */
export const mediumWeight = 600;

/** Type scale in px (design § 2.4): size, line height, weight. */
export const typeScale = {
  h1: { size: 20, lineHeight: 28, weight: mediumWeight },
  h2: { size: 16, lineHeight: 24, weight: mediumWeight },
  h3: { size: 14, lineHeight: 20, weight: mediumWeight },
  body1: { size: 14, lineHeight: 20, weight: 400 },
  body2: { size: 14, lineHeight: 20, weight: 400 },
  subtitle2: { size: 12, lineHeight: 16, weight: mediumWeight },
  caption: { size: 12, lineHeight: 16, weight: 400 },
  button: { size: 14, lineHeight: 20, weight: mediumWeight },
  /** The initials inside the user menu's avatar (design § 3.2). */
  avatarInitials: { size: 13, lineHeight: 16, weight: mediumWeight },
  /**
   * The backlog table's body cells (feat-027 design § 2.6): 13 px, measured on
   * the reference screen. The rest of the panel keeps the 14 px body.
   */
  tableCell: { size: 13, lineHeight: 20, weight: 400 },
} as const;

/**
 * The soft glow round the backlog's card (feat-027 design § 2.6) — not MUI's
 * elevation 1, which is a tight shadow darker at the bottom.
 */
export const cardShadow = '0 3px 7px rgba(0,0,0,0.13), 0 0.6px 1.8px rgba(0,0,0,0.11)';

/**
 * The panel's one font (feat-027 D-7 item 9): Segoe UI, then the system's own
 * UI font. No web font is loaded — the panel names a font the system has.
 */
export const fontFamily =
  '"Segoe UI", -apple-system, BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif';

/** The root font size MUI converts px to rem against. */
export const htmlFontSize = 16;

/** The base body text size (BR-3). */
export const baseFontSize = 14;

/** The tooltip's hover delay (design § 2.6). */
export const tooltipEnterDelayMs = 500;

/** The focus ring: 2 px accent outline, offset 2 px (design § 2.6, BR-5). */
export const focusRing = { width: 2, offset: 2 } as const;
