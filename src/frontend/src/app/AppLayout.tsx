import type { ReactNode } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Drawer from '@mui/material/Drawer';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import HomeIcon from '@mui/icons-material/Home';
import LanguageIcon from '@mui/icons-material/Language';
import LogoutIcon from '@mui/icons-material/Logout';
import { useAuth } from '../features/auth/AuthContext';
// Deep import, not the slice barrel — `features/dictionaries/index.ts`
// re-exports only the page components, and this is a permission code, not a
// page.
import { dictionariesPermissions } from '../features/dictionaries/permissions';

const DRAWER_WIDTH = 240;

interface NavItem {
  to: string;
  label: string;
  icon: ReactNode;
  /** Пункт скрыт, если задан и `can(permission)` — ложь. */
  permission?: string;
}

const navItems: NavItem[] = [
  { to: '/home', label: 'Главная', icon: <HomeIcon /> },
  {
    to: '/dictionaries/languages',
    label: 'Языки',
    icon: <LanguageIcon />,
    permission: dictionariesPermissions.languagesRead,
  },
];

/** Каркас закрытой части: AppBar, боковая навигация, выход. */
export function AppLayout() {
  const { displayName, signOut, can } = useAuth();
  const visibleNavItems = navItems.filter((item) => item.permission === undefined || can(item.permission));

  return (
    <Box sx={{ display: 'flex' }}>
      <AppBar position="fixed" sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}>
        <Toolbar>
          <Typography variant="h6" component="h1" sx={{ flexGrow: 1 }}>
            Mustask
          </Typography>
          <Typography variant="body2" sx={{ mr: 2 }}>
            {displayName}
          </Typography>
          <Button color="inherit" startIcon={<LogoutIcon />} onClick={signOut}>
            Выйти
          </Button>
        </Toolbar>
      </AppBar>
      <Drawer
        variant="permanent"
        sx={{
          width: DRAWER_WIDTH,
          flexShrink: 0,
          '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box' },
        }}
      >
        <Toolbar />
        <List component="nav">
          {visibleNavItems.map((item) => (
            <ListItemButton
              key={item.to}
              component={NavLink}
              to={item.to}
              sx={{ '&.active': { bgcolor: 'action.selected' } }}
            >
              <ListItemIcon>{item.icon}</ListItemIcon>
              <ListItemText primary={item.label} />
            </ListItemButton>
          ))}
        </List>
      </Drawer>
      <Box component="main" sx={{ flexGrow: 1, p: 3 }}>
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  );
}
