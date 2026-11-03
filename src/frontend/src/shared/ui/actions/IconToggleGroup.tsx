import { cloneElement, type ReactElement } from 'react';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import Tooltip from '@mui/material/Tooltip';
import type { Theme } from '@mui/material/styles';

export interface IconToggleItem {
  value: string;
  /** The button's accessible name: the value, in words. */
  label: string;
  /** The glyph, sized by the button. */
  icon: ReactElement;
  /** The tooltip, in full — the host writes it («Раскладка: Вертикально. …»). Default `label`. */
  tooltip?: string;
}

export interface IconToggleGroupProps {
  /** The group's name — the control it switches («Раскладка»). */
  label: string;
  items: readonly IconToggleItem[];
  value: string;
  /** Called with the new value only; pressing the chosen one again does nothing. */
  onChange: (value: string) => void;
}

/**
 * A one-of-N switch whose every value is its own icon button, adjoined in one frame (feat-049
 * design § 4.9): a layout, a display mode. MUI's `ToggleButtonGroup` (exclusive, small) with
 * a `Tooltip` on each button. The group is a `role="group"` named after the control, each button
 * is named after its value and carries `aria-pressed`; the group is one Tab stop (MUI's roving focus: arrow keys move between the buttons). The pressed
 * one has the selection tint and a thick underline, and the focus ring is drawn inside the
 * focused button so it cannot drift off it.
 */
export function IconToggleGroup({ label, items, value, onChange }: IconToggleGroupProps) {
  return (
    <ToggleButtonGroup
      exclusive
      size="small"
      aria-label={label}
      value={value}
      onChange={(_event, next: string | null) => {
        if (next !== null && next !== value) onChange(next);
      }}
      sx={(theme) => ({
        backgroundColor: theme.palette.background.paper,
        '& .MuiToggleButtonGroup-grouped': {
          margin: 0,
          border: `${theme.layout.frame.thin}px solid ${theme.palette.shell.inputBorder}`,
          '&:not(:first-of-type)': { marginLeft: `-${theme.layout.frame.thin}px` },
        },
      })}
    >
      {items.map((item) => (
        <IconToggleButton key={item.value} item={item} value={item.value} />
      ))}
    </ToggleButtonGroup>
  );
}

/**
 * `ToggleButtonGroup` clones each child with `selected`, `onChange`, the grouped class and the
 * rest, so the item takes them as a `ToggleButton` does and hands them on: it is a component of
 * its own so the `Tooltip` can stand outside the button without hiding it from the group.
 */
function IconToggleButton({
  item,
  ...groupProps
}: {
  item: IconToggleItem;
  value: string;
  selected?: boolean;
}) {
  return (
    <Tooltip title={item.tooltip ?? item.label}>
      <ToggleButton
        {...groupProps}
        aria-label={item.label}
        sx={(theme: Theme) => ({
          width: theme.spacing(4.25),
          minWidth: 0,
          height: theme.spacing(4),
          padding: 0,
          fontSize: theme.layout.iconAction.bar.glyph,
          color: theme.palette.text.secondary,
          backgroundColor: theme.palette.background.paper,
          '&:hover': { backgroundColor: theme.palette.action.hover },
          '&.Mui-selected': {
            color: theme.palette.primary.dark,
            backgroundColor: theme.palette.shell.rowSelected,
            boxShadow: `inset 0 -${theme.layout.frame.thick + 1}px 0 ${theme.palette.primary.main}`,
          },
          '&.Mui-selected:hover': { backgroundColor: theme.palette.shell.rowSelected },
          '&.Mui-focusVisible': { outlineOffset: -3, zIndex: 1 },
        })}
      >
        {cloneElement(item.icon as ReactElement<{ fontSize?: string; 'aria-hidden'?: boolean }>, {
          fontSize: 'inherit',
          'aria-hidden': true,
        })}
      </ToggleButton>
    </Tooltip>
  );
}
