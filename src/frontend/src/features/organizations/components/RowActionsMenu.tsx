import { useId, useState, type ReactElement } from 'react';
import IconButton from '@mui/material/IconButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import MoreHorizIcon from '@mui/icons-material/MoreHoriz';

export interface RowAction {
  label: string;
  icon: ReactElement;
  onClick(): void;
  /** Cannot be undone: the entry in the error colour (design § 9.2). */
  destructive?: boolean;
}

/**
 * A table row's «Действия: <название>» (design § 9.2): an icon button that
 * opens the row's `Menu`; choosing an entry closes it and runs the entry.
 * Focus returns to the button when the menu closes.
 */
export function RowActionsMenu({ name, actions }: { name: string; actions: readonly RowAction[] }) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const menuId = useId();

  return (
    <>
      <IconButton
        size="small"
        aria-label={`Действия: ${name}`}
        aria-haspopup="menu"
        aria-expanded={anchor ? 'true' : 'false'}
        aria-controls={anchor ? menuId : undefined}
        onClick={(event) => setAnchor(event.currentTarget)}
      >
        <MoreHorizIcon fontSize="small" />
      </IconButton>
      <Menu
        id={menuId}
        anchorEl={anchor}
        open={anchor !== null}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        {actions.map((action) => (
          <MenuItem
            key={action.label}
            onClick={() => {
              setAnchor(null);
              action.onClick();
            }}
            sx={action.destructive ? { color: 'error.main' } : undefined}
          >
            <ListItemIcon sx={action.destructive ? { color: 'error.main' } : undefined}>
              {action.icon}
            </ListItemIcon>
            <ListItemText>{action.label}</ListItemText>
          </MenuItem>
        ))}
      </Menu>
    </>
  );
}
