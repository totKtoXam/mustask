import { useId, useState, type KeyboardEvent, type MouseEvent } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Button from '@mui/material/Button';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { BOARD_LEVELS, LEVEL_LABELS, type BoardLevelCode } from '../lib/levels';

/**
 * The level drop-down (feat-001 US-6, BR-H-15; feat-027 design § 3.4), at the
 * right end of the head's tab row: a text button whose visible label is the
 * level — «Эпики» — and whose accessible name is «Уровень: Эпики» (the name
 * contains the visible label, WCAG 2.5.3, and is what the E2E selects by). It
 * opens a menu of «Эпики», «Фичи», «Истории» in rank order, each a link to its
 * own address, the current one selected and `aria-current="page"`. Choosing
 * changes only the address's `:level`, so the browser's back returns to the
 * previous level. Keyboard per mod-015 § 12.4: Enter, Space or ArrowDown opens
 * on the current level (MUI's `selectedMenu`), the arrows and Home/End move,
 * Enter chooses, Escape closes; focus comes back to the button either way, and
 * the page keeps the button mounted across levels, so it then reads the new one.
 */
export function LevelMenu({
  level,
  pathOf,
}: {
  level: BoardLevelCode;
  /** The address of the same page at another level. */
  pathOf(level: BoardLevelCode): string;
}) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const buttonId = useId();
  const menuId = useId();
  const open = anchor !== null;

  function openMenu(event: MouseEvent<HTMLElement> | KeyboardEvent<HTMLElement>) {
    setAnchor(event.currentTarget);
  }

  return (
    <>
      <Button
        id={buttonId}
        variant="text"
        size="small"
        endIcon={<ExpandMoreIcon />}
        aria-label={`Уровень: ${LEVEL_LABELS[level]}`}
        aria-haspopup="menu"
        aria-expanded={open ? 'true' : 'false'}
        aria-controls={open ? menuId : undefined}
        onClick={openMenu}
        onKeyDown={(event) => {
          if (event.key === 'ArrowDown') {
            event.preventDefault();
            openMenu(event);
          }
        }}
        sx={{ flexShrink: 0, '& .MuiButton-endIcon': { color: 'text.primary' } }}
      >
        {LEVEL_LABELS[level]}
      </Button>
      <Menu
        id={menuId}
        anchorEl={anchor}
        open={open}
        onClose={() => setAnchor(null)}
        variant="selectedMenu"
        slotProps={{ list: { 'aria-labelledby': buttonId } }}
      >
        {BOARD_LEVELS.map((candidate) => {
          const current = candidate === level;
          return (
            <MenuItem
              key={candidate}
              component={RouterLink}
              to={pathOf(candidate)}
              selected={current}
              aria-current={current ? 'page' : undefined}
              onClick={() => setAnchor(null)}
            >
              {LEVEL_LABELS[candidate]}
            </MenuItem>
          );
        })}
      </Menu>
    </>
  );
}
