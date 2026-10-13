import type { MouseEvent, ReactElement, Ref } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Button from '@mui/material/Button';
import type { Theme } from '@mui/material/styles';
import { ThinChevronDownIcon } from '../icons/ThinIcons';
import type { PassThrough } from '../passThrough';
import { InlineSpinner } from './InlineSpinner';

export type ActionTone = 'primary' | 'neutral' | 'subtle' | 'danger';

export interface ActionButtonProps extends PassThrough<HTMLElement> {
  /** The name; drawn. */
  label: string;
  /** Default `neutral` — blue is deliberate: at most one `primary` per page header or dialog. */
  tone?: ActionTone;
  /** A leading icon, 18 px. */
  icon?: ReactElement;
  /** Renders a link (inside the router) instead of a button. */
  to?: string;
  onClick?: (event: MouseEvent<HTMLElement>) => void;
  /** Default `button`; a form's own submit is `submit`. Ignored for a link. */
  type?: 'button' | 'submit';
  /** The work this button started is running: a spinner replaces the icon; inert, still focusable. */
  busy?: boolean;
  /** Unavailable for a reason of state: native `disabled`, out of the Tab order. */
  disabled?: boolean;
  /** The button opens a menu: `aria-haspopup`, `aria-expanded`, and a thin chevron unless hidden. */
  menu?: { open: boolean; hideIndicator?: boolean };
  /** `compact` has no minimum height — the retry inside a notice. Default 32 px. */
  size?: 'default' | 'compact';
  fullWidth?: boolean;
  /** The button, or the anchor when `to` is set. */
  ref?: Ref<HTMLButtonElement>;
}

const TONES = {
  primary: { variant: 'contained', color: 'primary' },
  neutral: { variant: 'contained', color: 'inherit' },
  subtle: { variant: 'text', color: 'inherit' },
  danger: { variant: 'contained', color: 'error' },
} as const;

/**
 * Every text button (tech-015 design § 5.2): a command, a dialog's primary and
 * cancel, a retry, a state frame's action. MUI's `Button` in one of four tones —
 * `primary` the page's only filled accent, `neutral` the reference's grey,
 * `subtle` no fill, `danger` for what cannot be undone — at the label type
 * (14 / 20 / 600) and `density.controlHeight`.
 *
 * `busy` is not `disabled`: the spinner replaces the icon, the click is ignored
 * and the button stays focusable, so a user who pressed Enter on «Сохранить»
 * keeps their place. A disabled button leaves the Tab order.
 */
export function ActionButton({
  label,
  tone = 'neutral',
  icon,
  to,
  onClick,
  type = 'button',
  busy = false,
  disabled = false,
  menu,
  size = 'default',
  fullWidth = false,
  ref,
  ...passThrough
}: ActionButtonProps) {
  const { variant, color } = TONES[tone];
  const common = {
    variant,
    color,
    fullWidth,
    disabled,
    startIcon: busy ? <InlineSpinner /> : icon,
    endIcon: menu && !menu.hideIndicator ? <ThinChevronDownIcon /> : undefined,
    'aria-busy': busy ? ('true' as const) : undefined,
    'aria-disabled': busy ? ('true' as const) : undefined,
    'aria-haspopup': menu ? ('menu' as const) : undefined,
    'aria-expanded': menu ? menu.open : undefined,
    onClick: (event: MouseEvent<HTMLElement>) => {
      if (busy) {
        // A busy submit must not submit, a busy link must not navigate.
        event.preventDefault();
        return;
      }
      onClick?.(event);
    },
    sx: (theme: Theme) => actionSx(theme, tone, size),
    ...passThrough,
  };
  return to ? (
    <Button component={RouterLink} to={to} ref={ref as Ref<HTMLAnchorElement>} {...common}>
      {label}
    </Button>
  ) : (
    <Button type={type} ref={ref} {...common}>
      {label}
    </Button>
  );
}

function actionSx(theme: Theme, tone: ActionTone, size: 'default' | 'compact') {
  return {
    flexShrink: 0,
    ...(size === 'compact' ? { minHeight: 0 } : {}),
    // The reference's glyph beside a label is 18 px; MUI's start icon is 20.
    '& .MuiButton-startIcon > *:nth-of-type(1), & .MuiButton-endIcon > *:nth-of-type(1)': {
      fontSize: theme.layout.iconAction.bar.glyph,
    },
    // `subtle` is plain text on a light hover; MUI's own text button hover is
    // a 4 % wash, and the legacy accent-iconed text button is not the kit's.
    ...(tone === 'subtle'
      ? {
          color: theme.palette.text.primary,
          '--variant-textColor': theme.palette.text.primary,
          '&:hover': { backgroundColor: theme.palette.action.hover },
        }
      : {}),
  };
}
