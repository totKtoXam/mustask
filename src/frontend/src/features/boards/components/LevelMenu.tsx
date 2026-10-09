import { useId, useState, type KeyboardEvent, type MouseEvent } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { BOARD_LEVELS, LEVEL_LABELS, type BoardLevelCode } from '../lib/levels';

/**
 * The level drop-down (feat-001 US-6, BR-H-15; its spec § Level drop-down),
 * in mod-015 § 6.1's views row under the `h1`, where mod-006's tabs stood:
 * «Уровень: <уровень>» opens a menu of «Эпики», «Фичи», «Истории» in rank
 * order, each a link to its own address, the current one selected and
 * `aria-current="page"`. Choosing changes only the address's `:level`, so
 * the browser's back returns to the previous level. Keyboard per mod-015
 * § 12.4: Enter, Space or ArrowDown opens on the current level (MUI's
 * `selectedMenu`), the arrows and Home/End move, Enter chooses, Escape closes;
 * focus comes back to the button either way, and the page keeps the button
 * mounted across levels, so it then reads the new one.
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
    <Box sx={{ pb: 1 }}>
      <Button
        id={buttonId}
        variant="text"
        endIcon={<ExpandMoreIcon />}
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
      >
        {`Уровень: ${LEVEL_LABELS[level]}`}
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
    </Box>
  );
}
