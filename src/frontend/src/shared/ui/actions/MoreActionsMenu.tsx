import Box from '@mui/material/Box';
import { ThinMoreVerticalIcon } from '../icons/ThinIcons';
import { IconAction } from './IconAction';
import { MenuButton, type MenuEntry } from './MenuButton';

/** The kit's default name for the «⋮» menu. */
const DEFAULT_LABEL = 'Ещё действия';

export interface MoreActionsMenuProps {
  /** The menu's and the button's name; a row passes «Ещё действия: MSK-EPIC-1». */
  label?: string;
  items: readonly MenuEntry[];
  /** `row` is the 24 px control of a table row; `bar` (default) the card's 32 px one. */
  size?: 'row' | 'bar';
  /**
   * A table row's control is drawn only while its row is hovered, focused or
   * selected: `false` hides it (it keeps its place in the Tab order and shows when it
   * is focused, hovered or open). Absent or `true`: always drawn.
   */
  revealed?: boolean;
  disableRestoreFocus?: boolean;
}

/**
 * The «⋮» of a card, a row or a toolbar (tech-015 design § 5.3): a `MenuButton`
 * whose trigger is an `IconAction` with the vertical thin dots. Never a
 * horizontal «⋯» — the reference's overflow is vertical.
 */
export function MoreActionsMenu({
  label = DEFAULT_LABEL,
  items,
  size = 'bar',
  revealed,
  disableRestoreFocus,
}: MoreActionsMenuProps) {
  return (
    <MenuButton
      label={label}
      items={items}
      align="end"
      disableRestoreFocus={disableRestoreFocus}
      trigger={(trigger) => {
        const button = (
          <IconAction label={label} icon={<ThinMoreVerticalIcon />} size={size} {...trigger} />
        );
        if (revealed !== false) return button;
        // The trigger says whether its menu is open (`aria-expanded`); the wrapper repeats it
        // as an attribute of its own, so no rule has to look down into the subtree.
        return (
          <Box
            component="span"
            data-revealed="false"
            data-expanded={trigger['aria-expanded'] ? 'true' : 'false'}
            sx={{
              display: 'inline-flex',
              opacity: 0,
              '&:hover, &:focus-within, &[data-expanded="true"]': { opacity: 1 },
              // With no hover to discover it by, the control is simply there.
              '@media (hover: none)': { opacity: 1 },
            }}
          >
            {button}
          </Box>
        );
      }}
    />
  );
}
