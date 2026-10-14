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
  /**
   * The hover frame of a kit field and of a card control (tech-015 design
   * § 3.2): 1.67:1 on white, 1.58:1 on the application background. Decoration —
   * the label names the field and the focus frame carries the 3:1 — so it sits
   * between 1.5 and 2:1 and under `inputBorder`; `theme.test.tsx` pins that
   * range, not the hex.
   */
  fieldHover: '#C8C8C8',
  /**
   * A field's label, the band's labels and its meta line: the reference's
   * label grey. 4.95:1 on white and 4.66:1 on the application background —
   * `contrast.test.ts` pins both at 4.5, so a lighter grey fails. `#737373` is
   * 4.46:1 on the band and is not used. Hints and empty texts stay
   * `textSecondary`.
   */
  cardLabel: '#707070',
  /**
   * The rule under a section's head: the reference's, 1.20:1 on white, lighter
   * than `divider`. A decorative line — the head and the content carry the
   * structure — so it is exempt from the 3:1 rule for graphics.
   */
  sectionRule: '#EAEAEA',
  /**
   * The disabled split button's fill and label: the reference's clean look
   * (2.63:1; a disabled control is outside the contrast floor), not MUI's
   * disabled grey, which is a darker block with a fainter label.
   */
  splitDisabledFill: '#F0F0F0',
  splitDisabledText: '#959595',
} as const;

/** Semantic colours: meaning only, darker than MUI's defaults (ADR-0007). */
export const semanticColours = {
  error: '#C42B1C',
  warning: '#9D5D00',
  success: '#107C10',
  info: '#01579B',
  contrastText: '#FFFFFF',
} as const;

/**
 * The appearance token set a process seed stores per type (BR-4, D-16); feat-032 BR-C-11
 * adds `testcase`, whose spelling feat-031 adopts.
 */
export const APPEARANCE_TOKENS = ['epic', 'feature', 'story', 'task', 'bug', 'testcase'] as const;
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
  /** feat-032 design § 2: the flask's near-black neutral, 11.4:1 on white, so it needs no contrast exemption. */
  testcase: '#3B3A39',
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
  fieldHover: string;
  cardLabel: string;
  sectionRule: string;
  splitDisabledFill: string;
  splitDisabledText: string;
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
  fieldHover: colourRoles.fieldHover,
  cardLabel: colourRoles.cardLabel,
  sectionRule: colourRoles.sectionRule,
  splitDisabledFill: colourRoles.splitDisabledFill,
  splitDisabledText: colourRoles.splitDisabledText,
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
  /**
   * The width of a control's or a row's frame — a field's hover, rest and focus
   * frames, a row's selection and drop frames, the `Removed` ring (tech-015
   * design § 3.3). Not `dividerWidth` (a rule between surfaces): equal today,
   * different roles.
   */
  frame: FrameTokens;
  /**
   * The measures of a button that is only an icon, by where it stands
   * (tech-015 design § 3.3). The first three were lifted from the backlog's
   * group, where they were first measured.
   */
  iconAction: IconActionTokens;
  /** The type icon, state dot and avatar beside a name (tech-015 design § 3.3). */
  mark: MarkTokens;
  /** The spinner of a busy button and of a field's busy adornment. */
  inlineSpinner: number;
  /** The page head's rhythm and the page panel's spacing (tech-015 design § 3.3). */
  pageHead: PageHeadTokens;
  /** The table family's measures (tech-015 design § 3.3). */
  table: TableTokens;
  /** The field family's measures (tech-015 design § 3.3). */
  field: FieldTokens;
  /** A titled group inside the card or a form. */
  section: SectionTokens;
  /** The body's columns. */
  columns: ColumnsTokens;
  /** The work item card's shell, header, band and tab strip. */
  card: CardTokens;
  /** The backlog table and its page's head (feat-027 design § 2.3). */
  backlogTable: BacklogTableTokens;
}

/** `theme.layout.frame` — the thin and the thick frame. */
export interface FrameTokens {
  thin: number;
  thick: number;
}

/** One icon-only button: the target's box and the glyph inside it, in px. */
export interface IconActionSize {
  box: number;
  glyph: number;
}

/** `theme.layout.iconAction` — a target is at least 24 px (WCAG 2.5.8). */
export interface IconActionTokens {
  /** A table row's «+» and «⋮». */
  row: IconActionSize;
  /** The tree chevron and the team chevron; also the empty box of a row with no children. */
  chevron: IconActionSize;
  /** The two square buttons of the hierarchy header. */
  header: IconActionSize;
  /** The card header's toolbar and the dialog's two icons. */
  bar: IconActionSize;
}

/**
 * `theme.layout.mark` — the marks that stand beside a name, with one gap for
 * every mark-and-name pair.
 */
export interface MarkTokens {
  /** The type icon in a card or a form. */
  typeIcon: number;
  /** The type icon in a table row. */
  typeIconCompact: number;
  /** The state dot in a field of the card's band. */
  categoryDotField: number;
  /** The space between a mark and its name. */
  nameGap: number;
  /** The avatar beside a name, and the slot of a person's empty glyph. */
  avatarInline: number;
  /** The empty person's thin glyph, inside the `avatarInline` slot. */
  personGlyph: number;
}

/**
 * `theme.layout.pageHead` — `PageHeader`'s team rhythm: `top` + a 32 px row +
 * `viewsGap` + a 40 px tab row + `bottom` = 132. `trailGap` is the space
 * between a trail drawn above the title and the title; `panelTop` the space
 * above a page's panel.
 */
export interface PageHeadTokens {
  top: number;
  viewsGap: number;
  bottom: number;
  titleGap: number;
  titleIcon: number;
  trailGap: number;
  panelTop: number;
}

/** `theme.layout.table` — what any table of the kit shares. */
export interface TableTokens {
  /** The header row's height. */
  headerHeight: number;
  /** The least height of a table that scrolls inside itself. */
  minHeight: number;
  /** The indent of one depth. */
  treeIndent: number;
  /**
   * Where the title column's header label starts: the depth-0 icon's left edge
   * (the cell's padding, the chevron slot and a gap) and a few px more, as the
   * reference's label stands over the icon.
   */
  titleLabelInset: number;
  /** The static separator at a header cell's right edge. */
  separatorHeight: number;
  /** The draggable strip at the row's left edge. */
  gripHitWidth: number;
  /** The dotted line's distance from the card's left edge. */
  gripDotOffset: number;
  /** The dotted mark's height: four 2 px dots 6 px apart, centred on the row. */
  gripMarkHeight: number;
  /** One dot of the grip. */
  gripDot: number;
  /** The distance between two dots of the grip. */
  gripPitch: number;
  /** A dragged row's opacity. */
  draggedOpacity: number;
}

/**
 * `theme.layout.field` — a value box is the text's line plus 2 × `padY`; label
 * + `gap` + box + `gap` is 48 whether the field is empty or filled.
 */
export interface FieldTokens {
  padY: number;
  /** The box of a body-size value (20 + 2 × `padY`). */
  box: number;
  /** The box of the card's title (24 + 2 × `padY`). */
  titleBox: number;
  gap: number;
  /** A single-line text starts here: the 1 px frame and 5 of padding. */
  textInset: number;
  /** A multi-line text starts here: the frame and 7 of padding. */
  textAreaInset: number;
  /**
   * The space above a text that stands under a head of its own name
   * (`labelPlacement="hidden"`): it replaces the label's line there.
   */
  textAreaTop: number;
  /**
   * The space between a label-column adornment's right edge and the value's
   * text: 19, the reference's, which puts a 10 px dot's centre at label + 64.
   */
  adornmentGap: number;
  /** The least rows of a text area. */
  textMinRows: number;
}

/** `theme.layout.section` — a head, a rule and a body; the area spaces the sections. */
export interface SectionTokens {
  headGap: number;
  bodyTop: number;
  /** The space between two sections of one column; owned by the column, never a margin. */
  gap: number;
  /** The content inset of the card's fields. */
  inset: number;
}

/** `theme.layout.columns` — one, two or three columns by the container's width. */
export interface ColumnsTokens {
  gutter: number;
  top: number;
  bottom: number;
  gap: number;
  sideWidth: number;
  /** The container's width from which three columns stand. */
  threeMin: number;
  /** The container's width from which two columns stand. */
  twoMin: number;
}

/**
 * `theme.layout.card` — measured on the reference's work item card at a pixel
 * ratio of 1.25 (tech-015 design § 3.3, feat-029 design § 2).
 */
export interface CardTokens {
  /** The dialog's share of the window. */
  dialogShare: number;
  typeBar: number;
  typeBarCompact: number;
  headerInset: number;
  rightInset: number;
  headerPadTop: number;
  headerRowGap: number;
  headerPadBottom: number;
  /** The space between the actions of row 3 (the Save group and each icon button). */
  actionGap: number;
  /** The distance of the dialog's ✕ box from the card's right edge. */
  dialogIconInset: number;
  /** The space between the dialog's ⤢ and ✕. */
  dialogIconGap: number;
  bandMinHeight: number;
  bandPaddingTop: number;
  bandRow: number;
  bandRowGap: number;
  /** The label column of a band row — the label's own width. */
  bandLabelWidth: number;
  /** A property block's fixed width. */
  bandBlockWidth: number;
  /** The least a block shrinks to, and the width it keeps when the blocks stack. */
  bandBlockMin: number;
  bandBlockGap: number;
  tabStripHeight: number;
  tabPadX: number;
  /** How far the selected tab's indicator stands above the strip's bottom. */
  tabIndicatorOffset: number;
  /** The shell's width below which the header's third row wraps and the band's blocks stack. */
  stackBelow: number;
}

/**
 * `theme.layout.backlogTable` — the backlog table's own measures in px
 * (feat-027 design § 2.3): its columns and what its page keeps round the card,
 * taken from the reference screen and checked against the two acceptance
 * windows by `theme.test.tsx`. The generic ones (indent, glyphs, grip,
 * separator) are the kit's `iconAction`, `mark` and `table` groups since
 * tech-015.
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
  viewportOffset: 204,
};

const frame: FrameTokens = { thin: 1, thick: 2 };

const iconAction: IconActionTokens = {
  row: { box: 24, glyph: 16 },
  chevron: { box: 24, glyph: 20 },
  header: { box: 24, glyph: 18 },
  bar: { box: 32, glyph: 18 },
};

const mark: MarkTokens = {
  typeIcon: 20,
  typeIconCompact: 16,
  categoryDotField: 10,
  nameGap: 6,
  avatarInline: 24,
  personGlyph: 16,
};

const pageHead: PageHeadTokens = {
  top: 28,
  viewsGap: 12,
  bottom: 20,
  titleGap: 10,
  titleIcon: 24,
  trailGap: 8,
  panelTop: 16,
};

const table: TableTokens = {
  headerHeight: 40,
  minHeight: 240,
  treeIndent: 16,
  titleLabelInset: 36,
  separatorHeight: 20,
  gripHitWidth: 16,
  gripDotOffset: 7,
  gripMarkHeight: 20,
  gripDot: 2,
  gripPitch: 6,
  draggedOpacity: 0.6,
};

const field: FieldTokens = {
  padY: 4,
  box: 28,
  titleBox: 32,
  gap: 2,
  textInset: 6,
  textAreaInset: 8,
  textAreaTop: 15,
  adornmentGap: 19,
  textMinRows: 3,
};

const section: SectionTokens = { headGap: 12, bodyTop: 4, gap: 32, inset: 8 };

const columns: ColumnsTokens = {
  gutter: 12,
  top: 16,
  bottom: 24,
  gap: 16,
  sideWidth: 320,
  threeMin: 1040,
  twoMin: 680,
};

const card: CardTokens = {
  dialogShare: 0.9,
  typeBar: 10,
  typeBarCompact: 4,
  headerInset: 18,
  rightInset: 8,
  headerPadTop: 4,
  headerRowGap: 8,
  headerPadBottom: 8,
  actionGap: 11,
  dialogIconInset: 5,
  dialogIconGap: 2,
  bandMinHeight: 76,
  bandPaddingTop: 6,
  bandRow: 32,
  bandRowGap: 4,
  bandLabelWidth: 82,
  bandBlockWidth: 253,
  bandBlockMin: 240,
  bandBlockGap: 32,
  tabStripHeight: 36,
  tabPadX: 10,
  tabIndicatorOffset: 4,
  stackBelow: 720,
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
  frame,
  iconAction,
  mark,
  inlineSpinner: 16,
  pageHead,
  table,
  field,
  section,
  columns,
  card,
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
  /**
   * The work item card's id and title (tech-015 design § 3.2): 17 / 24, Regular,
   * between the 16 px section head and the 20 px page title.
   */
  cardTitle: { size: 17, lineHeight: 24, weight: 400 },
} as const;

/** The corner radius of every surface and control (was a literal in `theme.ts`). */
export const shapeRadius = 4;

/**
 * How long a notice stays before it goes (tech-015 design § 3.2): one value
 * for `NoticeProvider`, where five copies of 6000 stood in the slices.
 */
export const noticeDurationMs = 6000;

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
