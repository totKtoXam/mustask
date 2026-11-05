import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import { useTheme, type CSSObject, type Theme } from '@mui/material/styles';
import { InlineSpinner } from '../actions/InlineSpinner';
import type { FieldProps } from './fieldTypes';
import {
  boxHeight,
  frameRules,
  lineHeightPx,
  OPEN,
  valueTypography,
  type FramePart,
} from './fieldStyles';
import { fieldControlState, type FieldIds } from './useFieldWiring';

export interface FieldFrameProps extends Omit<FieldProps, 'id' | 'placeholder'> {
  ids: FieldIds;
  /** The value is empty: `emptyText` shows at rest. */
  empty: boolean;
  /** A `placeholder` is set: while hovered it takes over from `emptyText`. */
  hasPlaceholder?: boolean;
  /** A multi-line text: its text starts at `textAreaInset`, and a hidden label leaves `textAreaTop`. */
  multiline?: boolean;
  /** What the label points at; `null` for a control a label cannot name (a select's display). */
  labelFor?: string | null;
  /** A label whose control it cannot name moves focus itself. */
  onLabelClick?: () => void;
  /** A kind's own trailing slot inside the box (a picker's action). */
  endSlot?: ReactNode;
  /** A kind's own rules on the root: the date's mask, the select's caret. */
  kindRules?: CSSObject;
  /**
   * The kind's list (a select's menu, a picker's popup) is open: the root says
   * `data-open="true"` and the rules that follow it read that attribute (`OPEN`).
   */
  open?: boolean;
  /** The control: an `InputBase`, a `Select`, an `Autocomplete`'s input. */
  children: ReactNode;
}

/**
 * The internal frame of the field family (tech-015 design § 5.7), not exported
 * from the kit: the label (a real `<label for>`), the value's box with its frame,
 * the hint and the error line, and the states that are the same for every kind.
 * A kind brings only its control.
 *
 * The frame is an inset shadow, so no state changes the box: label 16 + `gap` +
 * box 28 + `gap` is 48 whether the field is empty or filled. In the band (`start`)
 * a label-column adornment stands in the label's cell and the frame is two halves
 * that meet, so the box takes the dot in without measuring anything.
 */
export function FieldFrame({
  label,
  labelPlacement = 'top',
  labelWidth,
  size = 'body',
  frame = 'hover',
  hint,
  error,
  required,
  readOnly,
  busy,
  disabled,
  emptyText,
  startAdornment,
  adornmentPlacement,
  ids,
  empty,
  hasPlaceholder = false,
  multiline = false,
  labelFor,
  onLabelClick,
  endSlot,
  kindRules,
  open = false,
  children,
}: FieldFrameProps) {
  const theme = useTheme();
  const { interactive } = fieldControlState(
    { error, hint, required, readOnly, busy, disabled },
    ids,
  );
  const hasAdornment = startAdornment !== undefined && startAdornment !== null;
  const placement = labelPlacement;
  const inLabelColumn =
    placement === 'start' && hasAdornment && (adornmentPlacement ?? 'label') === 'label';
  const inBox = hasAdornment && !inLabelColumn;
  const boxPart: FramePart = inLabelColumn ? 'rest' : 'whole';
  const columnWidth = labelWidth ?? theme.layout.card.bandLabelWidth;

  const labelNode = (
    <Box
      component="label"
      id={ids.label}
      htmlFor={labelFor === null ? undefined : (labelFor ?? ids.control)}
      onClick={onLabelClick}
      sx={labelSx(theme, placement)}
    >
      {label}
      {required && (
        <Box component="span" aria-hidden="true">
          {' *'}
        </Box>
      )}
    </Box>
  );

  const hintNode = hint ? (
    <Box id={ids.hint} data-field-hint sx={hintSx(theme, placement, columnWidth)}>
      {hint}
    </Box>
  ) : null;

  const errorNode = error ? (
    <Box
      id={ids.error}
      role="alert"
      data-field-error
      sx={{
        ...theme.typography.caption,
        color: theme.palette.error.main,
        ...(placement === 'start' ? { gridColumn: 2 } : {}),
      }}
    >
      {error}
    </Box>
  ) : null;

  const boxNode = (
    <Box
      data-frame-part={boxPart}
      data-field-box
      sx={boxSx(theme, { size, multiline, part: boxPart, placement })}
    >
      {inBox && (
        <Box component="span" data-field-adornment sx={adornmentSx(theme)}>
          {startAdornment}
        </Box>
      )}
      <Box
        data-field-control
        sx={{ display: 'grid', flex: '1 1 0', minWidth: 0, '& > *': { gridArea: '1 / 1' } }}
      >
        {children}
        {empty && emptyText && (
          <Box
            component="span"
            aria-hidden="true"
            data-empty-text
            sx={{
              ...valueTypography(theme, size),
              alignSelf: multiline ? 'start' : 'center',
              minWidth: 0,
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
              pointerEvents: 'none',
              color: theme.palette.text.secondary,
            }}
          >
            {emptyText}
          </Box>
        )}
      </Box>
      {busy && (
        <Box
          aria-hidden="true"
          sx={{
            display: 'inline-flex',
            flexShrink: 0,
            ml: 0.5,
            color: theme.palette.text.secondary,
          }}
        >
          <InlineSpinner />
        </Box>
      )}
      {endSlot}
    </Box>
  );

  const rootSx = [
    rootBase(theme, { placement, columnWidth, size, multiline }),
    frameRules(theme, { frame, error: Boolean(error), interactive }),
    placeholderRules(theme, frame),
    emptyTextRules(hasPlaceholder),
    hintVisibility,
    kindRules ?? {},
  ];
  const dataAttributes = {
    'data-field': '',
    'data-label-placement': placement,
    'data-frame': frame,
    'data-size': size,
    'data-readonly': readOnly ? 'true' : undefined,
    'data-busy': busy ? 'true' : undefined,
    'data-open': open ? 'true' : undefined,
  };

  if (placement === 'start') {
    return (
      <Box {...dataAttributes} sx={rootSx}>
        <Box data-field-label-cell sx={labelCellSx(theme, size)}>
          {labelNode}
          {inLabelColumn && (
            <Box data-frame-part="lead" data-field-adornment sx={leadSx(theme, size, multiline)}>
              {startAdornment}
            </Box>
          )}
        </Box>
        {boxNode}
        {errorNode}
        {hintNode}
      </Box>
    );
  }
  return (
    <Box {...dataAttributes} sx={rootSx}>
      {placement === 'top' ? (
        <Box
          data-field-label-row
          sx={{
            display: 'flex',
            alignItems: 'center',
            height: lineHeightPx(theme, theme.typography.caption),
            marginBottom: `${theme.layout.field.gap}px`,
          }}
        >
          {labelNode}
          {hintNode}
        </Box>
      ) : (
        <>
          {labelNode}
          {hintNode}
        </>
      )}
      {boxNode}
      {errorNode}
    </Box>
  );
}

function rootBase(
  theme: Theme,
  o: {
    placement: string;
    columnWidth: number;
    size: 'body' | 'title' | 'table';
    multiline: boolean;
  },
): CSSObject {
  const { field, card } = theme.layout;
  const type = valueTypography(theme, o.size);
  return {
    position: 'relative',
    width: '100%',
    minWidth: 0,
    ...(o.placement === 'start'
      ? {
          display: 'grid',
          gridTemplateColumns: `${o.columnWidth}px minmax(0, 1fr)`,
          alignItems: 'center',
          minHeight: card.bandRow,
        }
      : { display: 'flex', flexDirection: 'column' }),
    // A text under a head of its own name leaves the room its label's line would have.
    ...(o.placement === 'hidden' && o.multiline ? { paddingTop: `${field.textAreaTop}px` } : {}),
    // The input family: one type, the value box's text, no browser chrome.
    '& .MuiInputBase-root': {
      ...type,
      color: theme.palette.text.primary,
      width: '100%',
      minWidth: 0,
      // A multi-line root brings 4 px above and 5 below: the box's own `padY` is the room.
      padding: 0,
    },
    '& .MuiInputBase-input': {
      ...type,
      padding: 0,
      height: 'auto',
      textOverflow: 'ellipsis',
    },
    '& .MuiInputBase-input:focus': { textOverflow: 'clip' },
    '& .MuiInputBase-root.Mui-disabled, & .MuiInputBase-input.Mui-disabled': {
      color: theme.palette.text.disabled,
      WebkitTextFillColor: theme.palette.text.disabled,
    },
    '& .MuiInputBase-input[readonly]': { caretColor: 'transparent' },
  };
}

/** The placeholder's look follows the states; its attribute is always there (design § 5.7). */
function placeholderRules(theme: Theme, frame: 'hover' | 'always'): CSSObject {
  const placeholder = '& input::placeholder, & textarea::placeholder';
  const shown = { opacity: 1, color: theme.palette.text.secondary };
  if (frame === 'always') return { [placeholder]: shown };
  return {
    [placeholder]: { ...shown, opacity: 0 },
    '&:hover input::placeholder, &:hover textarea::placeholder': shown,
    '&:focus-within input::placeholder, &:focus-within textarea::placeholder': shown,
    '@media (hover: none)': { [placeholder]: shown },
  };
}

/** `emptyText` stands at rest and gives way to focus — and, when there is a placeholder, to hover. */
function emptyTextRules(hasPlaceholder: boolean): CSSObject {
  const hidden = { visibility: 'hidden' };
  return {
    '&:focus-within [data-empty-text]': hidden,
    [`&${OPEN} [data-empty-text]`]: hidden,
    ...(hasPlaceholder
      ? {
          '&:hover [data-empty-text]': hidden,
          '@media (hover: none)': { '& [data-empty-text]': hidden },
        }
      : {}),
  };
}

/** The hint shows while the field has focus; it stays in the DOM, so the description is whole. */
const hintVisibility: CSSObject = {
  '& [data-field-hint]': { visibility: 'hidden' },
  '&:focus-within [data-field-hint]': { visibility: 'visible' },
  [`&${OPEN} [data-field-hint]`]: { visibility: 'visible' },
};

function labelSx(theme: Theme, placement: 'top' | 'start' | 'hidden'): CSSObject {
  return {
    ...theme.typography.caption,
    color: theme.palette.shell.cardLabel,
    minWidth: 0,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
    ...(placement === 'top' ? { flex: '0 1 auto' } : {}),
    ...(placement === 'start' ? { flex: '1 1 0' } : {}),
    // Hidden, the label still names the control.
    ...(placement === 'hidden' ? theme.mixins.visuallyHidden : {}),
  };
}

/** On the label's own line, right-aligned (`top`); under the value on an opaque ground otherwise. */
function hintSx(
  theme: Theme,
  placement: 'top' | 'start' | 'hidden',
  columnWidth: number,
): CSSObject {
  const base = {
    ...theme.typography.caption,
    color: theme.palette.text.secondary,
    whiteSpace: 'nowrap',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
  };
  if (placement === 'top') {
    return { ...base, flex: '1 1 0', minWidth: 0, textAlign: 'right', marginLeft: 1 };
  }
  return {
    ...base,
    position: 'absolute',
    top: '100%',
    left: placement === 'start' ? columnWidth : 0,
    right: 0,
    zIndex: 1,
    backgroundColor: theme.palette.background.paper,
  };
}

function boxSx(
  theme: Theme,
  o: {
    size: 'body' | 'title' | 'table';
    multiline: boolean;
    part: FramePart;
    placement: 'top' | 'start' | 'hidden';
  },
): CSSObject {
  const { field } = theme.layout;
  const radius = Number(theme.shape.borderRadius);
  return {
    position: 'relative',
    display: 'flex',
    alignItems: o.multiline ? 'flex-start' : 'center',
    boxSizing: 'border-box',
    minWidth: 0,
    ...(o.multiline
      ? {
          minHeight: boxHeight(theme, o.size),
          padding: `${field.padY}px ${field.textAreaInset}px`,
        }
      : { height: boxHeight(theme, o.size), padding: `0 ${field.textInset}px` }),
    borderRadius: o.part === 'rest' ? `0 ${radius}px ${radius}px 0` : `${radius}px`,
    ...(o.placement === 'start' ? {} : { marginBottom: `${field.gap}px` }),
  };
}

function adornmentSx(theme: Theme): CSSObject {
  return {
    display: 'inline-flex',
    alignItems: 'center',
    flexShrink: 0,
    marginRight: `${theme.layout.mark.nameGap}px`,
  };
}

/** The label's cell: the label, clipped where the box begins, and the label-column adornment. */
function labelCellSx(theme: Theme, size: 'body' | 'title' | 'table'): CSSObject {
  return { display: 'flex', alignItems: 'center', minWidth: 0, height: boxHeight(theme, size) };
}

/**
 * The first half of a split box: `textInset` of padding before the adornment and
 * `adornmentGap` less the box's own inset after it, so the adornment's right edge stands
 * `adornmentGap` before the value's text and its left edge `textInset` inside the frame.
 */
function leadSx(theme: Theme, size: 'body' | 'title' | 'table', multiline: boolean): CSSObject {
  const { field } = theme.layout;
  const radius = Number(theme.shape.borderRadius);
  return {
    display: 'inline-flex',
    alignItems: 'center',
    alignSelf: 'stretch',
    flexShrink: 0,
    boxSizing: 'border-box',
    height: boxHeight(theme, size),
    paddingLeft: `${field.textInset}px`,
    paddingRight: `${field.adornmentGap - (multiline ? field.textAreaInset : field.textInset)}px`,
    borderRadius: `${radius}px 0 0 ${radius}px`,
  };
}
