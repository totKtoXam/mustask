import { Link as RouterLink } from 'react-router-dom';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import Link from '@mui/material/Link';
import Toolbar from '@mui/material/Toolbar';
import MenuIcon from '@mui/icons-material/Menu';
import { HOME_PATH } from '../../shared/config/paths';
import { PRODUCT_NAME } from '../../shared/config/product';
import { IconAction } from '../../shared/ui';
import type { TrailSegment } from '../../shared/ui/pageChrome';
import { PageTrail } from '../../shared/ui/PageTrail';
import { ContextHost } from './ContextHost';
import { ProductMark } from './ProductMark';
import { UserMenu } from './UserMenu';

export interface TopBarProps {
  /** «Меню», below 1024 px only: opens the navigation overlay. */
  menu?: { overlayId: string; open: boolean; onOpen: () => void };
  /** The trail when it stands in the bar (1024 px and wider); empty: no trail, no divider. */
  trail: readonly TrailSegment[];
}

/**
 * The top bar (US-2, BR-6; feat-043 design § 3): on the content surface, 48 px high, above the
 * navigation. Left to right: «Меню» below 1024 px, the product mark linking to «Главная», the four
 * selectors (organization, department, team, project), the page trail and the user menu. Below
 * 1024 px the selectors stand in a second 48 px row of the bar that never wraps and scrolls
 * sideways (design § 9). It is built from the kit (tech-019): the menu button is the kit's icon action.
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
          <IconAction
            label="Меню"
            icon={<MenuIcon />}
            aria-expanded={menu.open ? 'true' : 'false'}
            aria-controls={menu.overlayId}
            onClick={menu.onOpen}
          />
        )}
        <Link
          component={RouterLink}
          to={HOME_PATH}
          underline="none"
          color="text.primary"
          variant="h2"
          aria-label={PRODUCT_NAME}
          sx={{ flexShrink: 0 }}
        >
          <ProductMark />
        </Link>
        {!menu && <ContextHost />}
        {trail.length > 0 && (
          <>
            <Divider orientation="vertical" flexItem sx={{ my: 1.5 }} />
            <PageTrail segments={trail} />
          </>
        )}
        <Box sx={{ flexGrow: 1 }} />
        <UserMenu />
      </Toolbar>
      {menu && (
        // The row never wraps: what does not fit scrolls sideways, so the bar stays two 48 px
        // rows high (design § 9). Its scrollbar is not drawn: it would add to the 48 px.
        <Toolbar
          disableGutters
          sx={{
            px: 2,
            gap: 1.5,
            overflowX: 'auto',
            scrollbarWidth: 'none',
            '& > *': { flexShrink: 0 },
          }}
        >
          <ContextHost />
        </Toolbar>
      )}
    </AppBar>
  );
}
