import { useId, useState, type KeyboardEvent, type MouseEvent } from 'react';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import IconButton from '@mui/material/IconButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import LogoutIcon from '@mui/icons-material/Logout';
import PersonOutlineOutlinedIcon from '@mui/icons-material/PersonOutlineOutlined';
import { useAuth } from '../../features/auth/AuthContext';
import { initials } from './initials';

/**
 * The display name at the top of the open menu: text, not an item — the
 * menu's keyboard focus skips it (MUI reads `muiSkipListHighlight`).
 */
function MenuNameRow({ name }: { name: string }) {
  return (
    <Box
      component="li"
      role="none"
      sx={{ px: 2, py: 1, typography: 'body1', fontWeight: 'fontWeightMedium' }}
    >
      {name}
    </Box>
  );
}
MenuNameRow.muiSkipListHighlight = true;

/**
 * The user menu (US-2, C-1, C-2; design § 3.2–§ 3.3): an avatar button named
 * «Профиль: <имя>» that opens the name and «Выйти». «Выйти» starts the same
 * sign-out the former top-bar button did.
 */
export function UserMenu() {
  const { displayName, signOut } = useAuth();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const menuId = useId();
  const name = displayName.trim();
  const letters = initials(name);
  const open = anchor !== null;

  function openMenu(event: MouseEvent<HTMLElement> | KeyboardEvent<HTMLElement>) {
    setAnchor(event.currentTarget);
  }

  return (
    <>
      <IconButton
        aria-label={name ? `Профиль: ${name}` : 'Профиль'}
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
        <Avatar>{letters ?? <PersonOutlineOutlinedIcon fontSize="small" />}</Avatar>
      </IconButton>
      <Menu
        id={menuId}
        anchorEl={anchor}
        open={open}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        {name ? <MenuNameRow name={name} /> : null}
        {name ? <Divider /> : null}
        <MenuItem
          onClick={() => {
            setAnchor(null);
            signOut();
          }}
        >
          <ListItemIcon>
            <LogoutIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText>Выйти</ListItemText>
        </MenuItem>
      </Menu>
    </>
  );
}
