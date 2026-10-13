import { cloneElement, type MouseEvent, type ReactElement, type Ref } from 'react';
import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import { useTheme, type Theme } from '@mui/material/styles';
import type { PassThrough } from '../passThrough';

/** Where the button stands, which fixes its box and its glyph (`theme.layout.iconAction`). */
export type IconActionSize = 'row' | 'chevron' | 'header' | 'bar';

export interface IconActionProps extends PassThrough<HTMLButtonElement> {
  /** The accessible name **and** the tooltip. Name the row when many sit on a screen. */
  label: string;
  /** The glyph: a thin icon of `shared/ui/icons`, or a Material `*Outlined`. Sized by the button. */
  icon: ReactElement;
  /** Default `bar`. */
  size?: IconActionSize;
  /** The glyph's colour: `text.primary`, `text.secondary` or the «+» grey. Default `default`. */
  tone?: 'default' | 'muted' | 'add';
  /** `header` only: a 1 px square frame, as the hierarchy header's «+» and «−». */
  outlined?: boolean;
  onClick?: (event: MouseEvent<HTMLElement>) => void;
  /**
   * Nothing to do now (an idle expand, a write in flight): `aria-disabled`, muted,
   * the click does nothing — and the button stays in the Tab order.
   */
  inactive?: boolean;
  /** Unavailable for good: native `disabled`, out of the Tab order. */
  disabled?: boolean;
  /** The work this button started is running: a spinner replaces the glyph; inert, still focusable. */
  busy?: boolean;
  /** Default true; a tree chevron, which a row already names, passes false. */
  tooltip?: boolean;
  ref?: Ref<HTMLButtonElement>;
}

const TONE_COLOUR = {
  default: (theme: Theme) => theme.palette.text.primary,
  muted: (theme: Theme) => theme.palette.text.secondary,
  add: (theme: Theme) => theme.palette.shell.addAction,
} as const;

/**
 * Every icon-only button (tech-015 design § 5.2): a row's «+» and «⋮», a tree
 * chevron, the header's two square buttons, the card's toolbar and the dialog's
 * «Развернуть на страницу» and «Закрыть». MUI's medium `IconButton` with its own
 * box and glyph from the tokens, inside a `Tooltip` that shows on hover and on
 * keyboard focus, and on a disabled button too.
 *
 * `inactive` and `busy` are not `disabled`: the button keeps its place in the
 * Tab order and says why it does nothing. A host never sizes a glyph — the
 * button does, through the glyph's `fontSize="inherit"`.
 */
export function IconAction({
  label,
  icon,
  size = 'bar',
  tone = 'default',
  outlined = false,
  onClick,
  inactive = false,
  disabled = false,
  busy = false,
  tooltip = true,
  ref,
  ...passThrough
}: IconActionProps) {
  const theme = useTheme();
  const inert = inactive || busy;
  const button = (
    <IconButton
      ref={ref}
      aria-label={label}
      aria-disabled={inert ? 'true' : undefined}
      aria-busy={busy ? 'true' : undefined}
      disabled={disabled}
      onClick={(event: MouseEvent<HTMLElement>) => {
        if (inert) {
          event.preventDefault();
          return;
        }
        onClick?.(event);
      }}
      sx={iconActionSx(theme, { size, tone, outlined, inactive, disabled })}
      {...passThrough}
    >
      {busy ? (
        <CircularProgress color="inherit" size={theme.layout.iconAction[size].glyph} />
      ) : (
        cloneElement(icon as ReactElement<{ fontSize?: string; 'aria-hidden'?: boolean }>, {
          fontSize: 'inherit',
          'aria-hidden': true,
        })
      )}
    </IconButton>
  );
  if (!tooltip) return button;
  return (
    <Tooltip title={label}>
      {disabled ? (
        <Box component="span" sx={{ display: 'inline-flex' }}>
          {button}
        </Box>
      ) : (
        button
      )}
    </Tooltip>
  );
}

function iconActionSx(
  theme: Theme,
  state: {
    size: IconActionSize;
    tone: keyof typeof TONE_COLOUR;
    outlined: boolean;
    inactive: boolean;
    disabled: boolean;
  },
) {
  const { box, glyph } = theme.layout.iconAction[state.size];
  const colour = state.disabled
    ? theme.palette.text.disabled
    : state.inactive
      ? theme.palette.text.secondary
      : TONE_COLOUR[state.tone](theme);
  const framed = state.outlined && state.size === 'header';
  const fill = framed ? theme.palette.background.paper : 'transparent';
  return {
    boxSizing: 'border-box' as const,
    width: box,
    height: box,
    padding: 0,
    flexShrink: 0,
    // The glyph is `fontSize="inherit"`: this is its size.
    fontSize: glyph,
    color: colour,
    backgroundColor: fill,
    ...(framed
      ? {
          border: `${theme.layout.frame.thin}px solid ${colour}`,
          borderRadius: `${Number(theme.shape.borderRadius) / 2}px`,
        }
      : {}),
    // The reference's glyphs are near-black and its hover is a light fill; a
    // button that does nothing (inactive, disabled) shows no hover.
    '&:hover': {
      backgroundColor: state.inactive || state.disabled ? fill : theme.palette.action.hover,
    },
    // MUI's own disabled grey is `action.disabled`; the kit's is `text.disabled`.
    '&.Mui-disabled': { color: colour },
  };
}
