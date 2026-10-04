import { Link as RouterLink } from 'react-router-dom';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import IconButton from '@mui/material/IconButton';
import Link from '@mui/material/Link';
import Toolbar from '@mui/material/Toolbar';
import MenuIcon from '@mui/icons-material/Menu';
import { HOME_PATH } from '../../shared/config/paths';
import { PRODUCT_NAME } from '../../shared/config/product';
import type { TrailSegment } from '../../shared/ui/pageChrome';
import { PageTrail } from '../../shared/ui/PageTrail';
import { ContextSwitcher } from './ContextSwitcher';
import { UserMenu } from './UserMenu';

export interface TopBarProps {
  /** «Меню», below 1024 px only: opens the navigation overlay. */
  menu?: { overlayId: string; open: boolean; onOpen: () => void };
  /** The trail when it stands in the bar (1024 px and wider); empty: no trail, no divider. */
  trail: readonly TrailSegment[];
}

/**
 * The top bar (US-2, BR-6; design § 3.1–§ 3.2): on the content surface,
 * 48 px high, above the navigation. Left to right: «Меню» below 1024 px, the
 * product mark linking to «Главная», the context switcher, the page trail
 * and the user menu.
 */
export function TopBar({ menu, trail }: TopBarProps) {
  return (
    <AppBar
      position="fixed"
      color="inherit"
      elevation={0}
      sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}
    >
      <Toolbar disableGutters sx={{ px: 2, gap: 1.5 }}>
        {menu && (
          <IconButton
            aria-label="Меню"
            aria-expanded={menu.open ? 'true' : 'false'}
            aria-controls={menu.overlayId}
            onClick={menu.onOpen}
            edge="start"
          >
            <MenuIcon />
          </IconButton>
        )}
        <Link
          component={RouterLink}
          to={HOME_PATH}
          underline="none"
          color="text.primary"
          variant="h2"
          sx={{ flexShrink: 0 }}
        >
          {PRODUCT_NAME}
        </Link>
        <ContextSwitcher />
        {trail.length > 0 && (
          <>
            <Divider orientation="vertical" flexItem sx={{ my: 1.5 }} />
            <PageTrail segments={trail} />
          </>
        )}
        <Box sx={{ flexGrow: 1 }} />
        <UserMenu />
      </Toolbar>
    </AppBar>
  );
}
