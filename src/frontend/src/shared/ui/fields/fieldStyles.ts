import type { CSSObject, Theme } from '@mui/material/styles';
import type { FieldFrameKind, FieldSize } from './fieldTypes';

// The look of the field family (tech-015 design § 5.7), as functions of the theme.
// Every number is a token of `layout.field`, `layout.frame`, `layout.card`, or the
// spacing unit; every colour a role of the palette.

/** The part of the frame a segment draws: the whole box, or one half of a box split by the label column. */
export type FramePart = 'whole' | 'lead' | 'rest';

/** The attribute the frame's segments carry, so one rule per state can reach all of them. */
export const FRAME_PART = 'data-frame-part';

/**
 * The field's root while its list (a select's menu, a picker's popup) is open. A kind
 * tracks open and close and `FieldFrame` sets the attribute; the rules read it. Not a
 * relational selector over `aria-expanded`: in jsdom every such rule walks the subtree
 * for each element whose style is read, which made role queries on a kit screen 15-20
 * times slower (tech-015 review R1-2); a browser would not feel it, the tests did.
 * `kitGuard.test.ts` fails a relational selector anywhere in the kit.
 */
export const OPEN = '[data-open="true"]';

/** A typography variant's line height in px: MUI keeps it unitless, the size in rem. */
export function lineHeightPx(
  theme: Theme,
  style: { fontSize?: string | number; lineHeight?: string | number },
): number {
  const size =
    typeof style.fontSize === 'number'
      ? style.fontSize
      : Number.parseFloat(String(style.fontSize)) * theme.typography.htmlFontSize;
  return Math.round(size * Number(style.lineHeight));
}

/** The type of the value's text, by `size`. */
export function valueTypography(theme: Theme, size: FieldSize) {
  return size === 'title'
    ? theme.typography.cardTitle
    : size === 'table'
      ? theme.typography.tableCell
      : theme.typography.body1;
}

/** The value box's height: the text's line plus 2 × `padY` (28; the title's 32). */
export function boxHeight(theme: Theme, size: FieldSize): number {
  return size === 'title' ? theme.layout.field.titleBox : theme.layout.field.box;
}

interface Ring {
  width: number;
  colour: string;
}

/** An inset frame: shadows, so no state changes the box. */
function ringFor(part: FramePart, { width, colour }: Ring): string {
  const w = `${width}px`;
  const all = `inset 0 0 0 ${w} ${colour}`;
  const top = `inset 0 ${w} 0 ${colour}`;
  const bottom = `inset 0 -${w} 0 ${colour}`;
  if (part === 'whole') return all;
  // A box split by the label column: each half draws its three outer sides.
  return part === 'lead'
    ? `inset ${w} 0 0 ${colour}, ${top}, ${bottom}`
    : `inset -${w} 0 0 ${colour}, ${top}, ${bottom}`;
}

function ringRules(ring: Ring | null): CSSObject {
  const shadow = (part: FramePart) => ({
    [`& [${FRAME_PART}="${part}"]`]: { boxShadow: ring ? ringFor(part, ring) : 'none' },
  });
  return { ...shadow('whole'), ...shadow('lead'), ...shadow('rest') };
}

/**
 * The frame in every state, as rules on the field's root (the segments carry
 * `data-frame-part`). Rest, hover, focus and error; a field that cannot be edited
 * shows no hover frame; a pointer that cannot hover sees the hover look at rest
 * (design § 5.7, principle 8).
 */
export function frameRules(
  theme: Theme,
  state: { frame: FieldFrameKind; error: boolean; interactive: boolean },
): CSSObject {
  const { thin, thick } = theme.layout.frame;
  const { palette } = theme;
  const rest: Ring | null = state.error
    ? { width: thin, colour: palette.error.main }
    : state.frame === 'always'
      ? { width: thin, colour: palette.shell.inputBorder }
      : null;
  const hover: Ring = state.error
    ? { width: thin, colour: palette.error.main }
    : {
        width: thin,
        colour: state.frame === 'always' ? palette.text.primary : palette.shell.fieldHover,
      };
  const focus: Ring = {
    width: thick,
    colour: state.error ? palette.error.main : palette.primary.main,
  };
  const touch = state.frame === 'hover' && !state.error && state.interactive;
  return {
    ...ringRules(rest),
    ...(touch ? { '@media (hover: none)': ringRules(hover) } : {}),
    ...(state.interactive ? { '&:hover': ringRules(hover) } : {}),
    '&:focus-within': ringRules(focus),
    // A select's menu takes the focus away while it is open; the field keeps its frame.
    [`&${OPEN}`]: ringRules(focus),
  };
}

/**
 * The caret shows with the frame; the display fills the box and keeps the caret's room.
 * A state rule names the caret as a descendant of `&` (the root, once): a second `&` in it
 * would be the root's class again, and no element holds that class inside the root.
 */
export function selectRules(theme: Theme, frame: FieldFrameKind): CSSObject {
  const icon = '.MuiSelect-icon';
  const caret = `&& ${icon}`;
  const shown = { opacity: 1 };
  return {
    '&& .MuiSelect-select': {
      display: 'flex',
      alignItems: 'center',
      minWidth: 0,
      padding: 0,
      paddingRight: `${theme.layout.iconAction.chevron.glyph}px`,
      height: 'auto',
      minHeight: 0,
      overflow: 'hidden',
      textOverflow: 'ellipsis',
      whiteSpace: 'nowrap',
    },
    [caret]: {
      right: 0,
      fontSize: theme.layout.iconAction.chevron.glyph,
      color: theme.palette.text.primary,
      pointerEvents: 'none',
      opacity: frame === 'always' ? 1 : 0,
    },
    [`&:hover ${icon}`]: shown,
    [`&:focus-within ${icon}`]: shown,
    [`&${OPEN} ${icon}`]: shown,
    '@media (hover: none)': { [caret]: shown },
    '&& .MuiSelect-select.Mui-disabled': { WebkitTextFillColor: theme.palette.text.disabled },
  };
}

/**
 * A picker's popup arrow and clear button are small, in the flow, and shown with the frame.
 * Each state's key repeats the state before *each* indicator and names the root once (`&`):
 * a second `&`, or an indicator left without its state, is a rule that never matches or
 * always does. `wrap` lets the chosen tags of a multiple picker wrap onto rows; a single
 * picker's input stays on one line.
 */
export function pickerRules(
  theme: Theme,
  frame: FieldFrameKind,
  { wrap = false }: { wrap?: boolean } = {},
): CSSObject {
  const popup = '.MuiAutocomplete-popupIndicator';
  const clear = '.MuiAutocomplete-clearIndicator';
  const indicators = `&& ${popup}, && ${clear}`;
  const when = (state: string) => `&${state} ${popup}, &${state} ${clear}`;
  const shown = { opacity: 1 };
  return {
    '&&& .MuiAutocomplete-inputRoot': wrap
      ? { padding: 0, flexWrap: 'wrap', gap: theme.spacing(0.5) }
      : { padding: 0, flexWrap: 'nowrap' },
    '&&& .MuiAutocomplete-input': wrap
      ? { padding: 0, minWidth: theme.spacing(8), width: 0, flexGrow: 1 }
      : { padding: 0, minWidth: 0, width: 0, flexGrow: 1 },
    '&&& .MuiAutocomplete-endAdornment': {
      position: 'static',
      transform: 'none',
      display: 'inline-flex',
      alignItems: 'center',
      flexShrink: 0,
      gap: '2px',
      // The arrow stays at the end of the last row.
      ...(wrap ? { marginLeft: 'auto' } : {}),
    },
    [indicators]: {
      width: theme.layout.iconAction.row.box,
      height: theme.layout.iconAction.row.box,
      padding: 0,
      margin: 0,
      fontSize: theme.layout.iconAction.chevron.glyph,
      color: theme.palette.text.primary,
      opacity: frame === 'always' ? 1 : 0,
    },
    '&& .MuiAutocomplete-popupIndicator, && .MuiAutocomplete-popupIndicatorOpen': {
      transform: 'none',
    },
    [when(':hover')]: shown,
    [when(':focus-within')]: shown,
    [when(OPEN)]: shown,
    '@media (hover: none)': { [indicators]: shown },
  };
}
