import type { MouseEvent, ReactElement } from 'react';
import Button from '@mui/material/Button';
import ButtonGroup from '@mui/material/ButtonGroup';
import { useTheme, type Theme } from '@mui/material/styles';
import { ThinChevronDownIcon } from '../icons/ThinIcons';
import { InlineSpinner } from './InlineSpinner';
import { MenuButton, type MenuEntry } from './MenuButton';

export interface SplitButtonProps {
  /** The main button's name. */
  label: string;
  icon?: ReactElement;
  /** `primary` (default) is the page's only filled button; `neutral` is for one that is not the main action. */
  tone?: 'primary' | 'neutral';
  type?: 'button' | 'submit';
  onClick?: (event: MouseEvent<HTMLElement>) => void;
  /** The work the main button started is running: both parts inert, focus kept, the fill kept. */
  busy?: boolean;
  /** Pristine: both parts native `disabled`, in the reference's clean look. */
  disabled?: boolean;
  /** The group's name, e.g. «Сохранение». */
  groupLabel: string;
  /**
   * The arrow's name, e.g. «Ещё варианты сохранения». It must not contain the main
   * label's word, so a query for the main button's name finds that button alone.
   */
  menuLabel: string;
  items: readonly MenuEntry[];
}

/**
 * A main action with a short list of variants (tech-015 design § 5.2):
 * «Сохранить» with «Сохранить и закрыть». MUI's `ButtonGroup` of the main button
 * and an arrow, whose menu is a `MenuButton`; the arrow is the thin chevron, never
 * Material's filled drop-down triangle.
 *
 * Pristine (`disabled`) it wears the reference's clean look — `splitDisabledFill`
 * and `splitDisabledText` — not MUI's disabled grey, which is a darker block with
 * a fainter label. `busy` keeps the tone's fill: the work is running, the button
 * is not unavailable.
 */
export function SplitButton({
  label,
  icon,
  tone = 'primary',
  type = 'button',
  onClick,
  busy = false,
  disabled = false,
  groupLabel,
  menuLabel,
  items,
}: SplitButtonProps) {
  const theme = useTheme();
  const inertProps = busy ? { 'aria-disabled': 'true' as const } : {};
  return (
    <ButtonGroup
      variant="contained"
      color={tone === 'primary' ? 'primary' : 'inherit'}
      disableElevation
      aria-label={groupLabel}
    >
      <Button
        type={type}
        startIcon={busy ? <InlineSpinner /> : icon}
        disabled={disabled}
        aria-busy={busy ? 'true' : undefined}
        {...inertProps}
        onClick={(event: MouseEvent<HTMLElement>) => {
          if (busy) {
            event.preventDefault();
            return;
          }
          onClick?.(event);
        }}
        sx={(current) => partSx(current)}
      >
        {label}
      </Button>
      <MenuButton
        label={menuLabel}
        items={items}
        align="end"
        trigger={(trigger) => (
          <Button
            {...trigger}
            aria-label={menuLabel}
            disabled={disabled}
            {...inertProps}
            onClick={
              busy ? (event: MouseEvent<HTMLElement>) => event.preventDefault() : trigger.onClick
            }
            onKeyDown={busy ? undefined : trigger.onKeyDown}
            sx={(current) => ({
              ...partSx(current),
              // About as wide as it is high: the reference's group is 112 × 32 — an
              // 81 px label part and a 31 px arrow part.
              minWidth: 0,
              width: current.density.controlHeight,
              padding: 0,
            })}
          >
            <ThinChevronDownIcon sx={{ fontSize: theme.layout.iconAction.bar.glyph }} />
          </Button>
        )}
      />
    </ButtonGroup>
  );
}

/** Both parts: the reference's clean disabled look, and a white line between them. */
function partSx(theme: Theme) {
  return {
    '&.Mui-disabled': {
      backgroundColor: theme.palette.shell.splitDisabledFill,
      color: theme.palette.shell.splitDisabledText,
      borderRightColor: theme.palette.background.paper,
    },
  };
}
