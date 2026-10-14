import { useCallback, useMemo, useRef, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import Box from '@mui/material/Box';
import Drawer from '@mui/material/Drawer';
import type { Theme } from '@mui/material/styles';
import Alert from '@mui/material/Alert';
import Snackbar from '@mui/material/Snackbar';
import { useAuth } from '../features/auth/AuthContext';
import {
  useAccessWatcher,
  useOrganizationAccess,
  useOrganizationsContextSource,
  useServedAccessCheck,
} from '../features/organizations';
import { AccessCheckContext } from '../shared/lib/access';
import { MAIN_REGION_ID } from '../shared/lib/mainRegion';
import { NoticeCornerContext } from '../shared/lib/noticeCorner';
import {
  WorkContextContext,
  type SwitchTargetResolver,
  type WorkContextSource,
} from '../shared/lib/workContext';
import { NoticeProvider } from '../shared/ui';
import {
  PageChromeContext,
  type PageChrome,
  type TrailPlacement,
  type TrailSegment,
} from '../shared/ui/pageChrome';
import { useNavigationModel } from './navigation/NavigationModelContext';
import { switchTarget } from './navigation/switchTarget';
import { useNavigationLayout } from './navigation/useNavigationLayout';
import { deriveTrail, isSettingsPage, visibleModel } from './navigation/visibility';
import { MainNavigation } from './shell/MainNavigation';
import { NavigationOverlay } from './shell/NavigationOverlay';
import { SettingsNavigation } from './shell/SettingsNavigation';
import { SkipLink } from './shell/SkipLink';
import { TopBar } from './shell/TopBar';

const MAIN_NAVIGATION_ID = 'main-navigation';
const OVERLAY_ID = 'navigation-overlay';

/** How long the role-change notice stays (mod-016 design § 2.4, as the other notices). */
const ROLE_CHANGE_NOTICE_MS = 6000;

/** Width changes of the permanent navigation; none under reduced motion (design § 2.6). */
function widthTransition(theme: Theme) {
  return {
    transition: theme.transitions.create('width'),
    '@media (prefers-reduced-motion: reduce)': { transition: 'none' },
  };
}

/** The rule on the left of «Меню настроек», beside the rail (design § 4.4). */
function settingsDivider(theme: Theme): string {
  return `${theme.layout.dividerWidth}px solid ${theme.palette.divider}`;
}

/** A permanent navigation column, fixed under the top bar on the navigation surface. */
function navigationDrawerSx(width: (theme: Theme) => number, left?: (theme: Theme) => number) {
  return (theme: Theme) => ({
    width: width(theme),
    flexShrink: 0,
    ...widthTransition(theme),
    '& .MuiDrawer-paper': {
      width: width(theme),
      top: theme.layout.topBarHeight,
      left: left ? left(theme) : 0,
      height: `calc(100% - ${theme.layout.topBarHeight}px)`,
      boxSizing: 'border-box',
      overflowX: 'hidden',
      backgroundColor: theme.palette.shell.navigation,
      borderRight: 0,
      ...(left ? { borderLeft: settingsDivider(theme) } : {}),
      ...widthTransition(theme),
    },
  });
}

export interface AppLayoutProps {
  /**
   * The organization/project/team source: mod-002's real lists unless a
   * component test passes a stub (design § 5.7; mod-002 D-o).
   */
  contextSource?: WorkContextSource;
}

/**
 * The application frame (mod-015 US-2..US-5, US-12; design § 3–§ 5): skip
 * link, top bar, main navigation (expanded, rail or overlay), the settings
 * navigation on settings pages, and the main region hosting the page.
 *
 * It provides what pages share with the frame: the one permission check
 * (BR-8), the work context (BR-11), the page chrome through which a page
 * header registers and the shell derives its trail (BR-6), and the one notice
 * host the kit's `useNotice` talks to (tech-015 design § 5.12) — above the
 * routes, so a notice outlives the page that raised it. The host stands under
 * the work context and the notice corner (mod-016 DC-11): it closes when the
 * shell raises the switcher's fallback or the role-change notice.
 *
 * The check is built from served access (mod-016, ADR-0027): an instance code
 * from the token, an organization code from what `GET …/access` served for
 * the organization asked about. The shell reads the switcher's selection's
 * access so the navigation has its codes; the gate of an organization's pages
 * reads the address's and binds the check to it. A watcher re-reads the codes
 * after a scoped 403/404 and raises the role-change notice (design § 2).
 */
export function AppLayout({ contextSource }: AppLayoutProps) {
  const { can, subject } = useAuth();
  const model = useNavigationModel();
  const { pathname } = useLocation();

  // Instance codes come from the token (ADR-0018); organization codes from
  // the served access in the tab's cache (design § 2.6). Unread, failed or
  // answered 404: "no".
  const { access, isCodesRead } = useServedAccessCheck(can);

  // The switcher-data registry (mod-002 D-n, D-o): the organizations slice's
  // source, handed the switch-target resolver the navigation model gives.
  const resolveSwitchTarget = useCallback<SwitchTargetResolver>(
    (request) => switchTarget(model, access, request, isCodesRead),
    [model, access, isCodesRead],
  );
  const realSource = useOrganizationsContextSource({
    enabled: contextSource === undefined,
    subject,
    resolveSwitchTarget,
  });
  const source = contextSource ?? realSource;

  const { isReal, selection } = source;
  // The access of the switcher's selection, for the navigation's entries; a
  // placeholder context reads nothing.
  useOrganizationAccess(selection.organizationId, { enabled: isReal });
  const fallbackShown = Boolean(source.fallback);
  const organizationName = useCallback(
    (organizationId: string) =>
      source.organizations.find((organization) => organization.id === organizationId)?.name,
    [source.organizations],
  );
  const { roleChange, dismissRoleChange } = useAccessWatcher({ fallbackShown, organizationName });
  const noticeCorner = useMemo(
    () => ({ roleChangeId: roleChange?.id ?? null, claim: dismissRoleChange }),
    [roleChange?.id, dismissRoleChange],
  );
  const visible = useMemo(
    () => visibleModel(model, access, { isReal, selection }),
    [model, access, isReal, selection],
  );

  // A not-found frame registers here: no page serves the address, so none is
  // current, whatever the address starts with (BR-13, BR-19).
  const [notFoundShown, setNotFoundShown] = useState(false);
  const registerNotFound = useCallback(() => {
    setNotFoundShown(true);
    return () => setNotFoundShown(false);
  }, []);
  const currentPage = notFoundShown ? null : pathname;

  // A page that no visible entry matches is beneath no landing: while an
  // organization's codes are unread its settings pages show no settings
  // navigation, no marker and no trail (design § 6.1).
  const settingsShown =
    visible.settingsLanding !== null && isSettingsPage(currentPage, visible, selection);
  const layout = useNavigationLayout(settingsShown);

  // The page header registers here; without one (a full-page frame) there
  // is no trail (plan § 4.5).
  const [headerAncestors, setHeaderAncestors] = useState<readonly TrailSegment[] | null>(null);
  const registerHeader = useCallback((ancestors: readonly TrailSegment[]) => {
    setHeaderAncestors(ancestors);
    return () => setHeaderAncestors(null);
  }, []);
  const trail = useMemo(
    () =>
      headerAncestors ? [...deriveTrail(pathname, visible, selection), ...headerAncestors] : [],
    [headerAncestors, pathname, visible, selection],
  );
  const trailPlacement: TrailPlacement = layout.main === 'overlay' ? 'header' : 'topBar';
  const chrome = useMemo<PageChrome>(
    () => ({ registerHeader, registerNotFound, trail, trailPlacement }),
    [registerHeader, registerNotFound, trail, trailPlacement],
  );

  const mainRef = useRef<HTMLElement>(null);
  const focusMain = useCallback(() => mainRef.current?.focus(), []);

  // The overlay closes whenever the layout changes, so widening the window
  // never leaves it behind (BR-10).
  const [overlayOpen, setOverlayOpen] = useState(false);
  const [overlayLayout, setOverlayLayout] = useState(layout.main);
  if (overlayLayout !== layout.main) {
    setOverlayLayout(layout.main);
    setOverlayOpen(false);
  }
  // Choosing an entry in the overlay moves focus into the main region once
  // the overlay has gone; Escape or a click outside leaves MUI's return of
  // focus to «Меню» (BR-10).
  const focusMainAfterOverlay = useRef(false);
  const chooseInOverlay = useCallback(() => {
    focusMainAfterOverlay.current = true;
    setOverlayOpen(false);
  }, []);
  const overlayExited = useCallback(() => {
    if (focusMainAfterOverlay.current) {
      focusMainAfterOverlay.current = false;
      focusMain();
    }
  }, [focusMain]);

  const overlay = layout.main === 'overlay';

  return (
    <AccessCheckContext.Provider value={access}>
      <WorkContextContext.Provider value={source}>
        <NoticeCornerContext.Provider value={noticeCorner}>
          <NoticeProvider>
            <PageChromeContext.Provider value={chrome}>
              <Box sx={{ display: 'flex', minHeight: '100vh' }}>
                <SkipLink onSkip={focusMain} />
                <TopBar
                  menu={
                    overlay
                      ? {
                          overlayId: OVERLAY_ID,
                          open: overlayOpen,
                          onOpen: () => setOverlayOpen(true),
                        }
                      : undefined
                  }
                  trail={trailPlacement === 'topBar' ? trail : []}
                />
                {overlay ? (
                  <NavigationOverlay
                    id={OVERLAY_ID}
                    open={overlayOpen}
                    onClose={() => setOverlayOpen(false)}
                    onExited={overlayExited}
                  >
                    <Box
                      sx={{
                        width: (theme) =>
                          settingsShown ? theme.layout.railWidth : theme.layout.navigationWidth,
                        flexShrink: 0,
                      }}
                    >
                      <MainNavigation
                        id={MAIN_NAVIGATION_ID}
                        variant={settingsShown ? 'rail' : 'expanded'}
                        visible={visible}
                        currentPage={currentPage}
                        settingsCurrent={settingsShown}
                        onEntryChosen={chooseInOverlay}
                      />
                    </Box>
                    {settingsShown && (
                      <Box
                        sx={{
                          width: (theme) => theme.layout.settingsNavigationWidth,
                          borderLeft: settingsDivider,
                        }}
                      >
                        <SettingsNavigation
                          visible={visible}
                          currentPage={currentPage}
                          onEntryChosen={chooseInOverlay}
                        />
                      </Box>
                    )}
                  </NavigationOverlay>
                ) : (
                  <>
                    <Drawer
                      variant="permanent"
                      sx={navigationDrawerSx((theme) =>
                        layout.main === 'expanded'
                          ? theme.layout.navigationWidth
                          : theme.layout.railWidth,
                      )}
                    >
                      <MainNavigation
                        id={MAIN_NAVIGATION_ID}
                        variant={layout.main === 'expanded' ? 'expanded' : 'rail'}
                        visible={visible}
                        currentPage={currentPage}
                        settingsCurrent={settingsShown}
                        toggle={layout.toggleShown ? { onToggle: layout.toggle } : undefined}
                      />
                    </Drawer>
                    {settingsShown && (
                      <Drawer
                        variant="permanent"
                        sx={navigationDrawerSx(
                          (theme) => theme.layout.settingsNavigationWidth,
                          (theme) => theme.layout.railWidth,
                        )}
                      >
                        <SettingsNavigation visible={visible} currentPage={currentPage} />
                      </Drawer>
                    )}
                  </>
                )}
                <Box
                  component="main"
                  id={MAIN_REGION_ID}
                  tabIndex={-1}
                  ref={mainRef}
                  sx={{
                    flexGrow: 1,
                    minWidth: 0,
                    px: { xs: 2, md: 3 },
                    pb: 3,
                    '&:focus': { outline: 'none' },
                  }}
                >
                  <Box sx={(theme) => theme.mixins.toolbar} />
                  <Outlet />
                </Box>
                {roleChange && (
                  // The shell's own notice (mod-016 design § 2.4, DC-11): it is
                  // here, beside the switcher's fallback notice, so it shows the
                  // same on every organization page whichever slice hosts the
                  // page's own notices. A newer notice of a page replaces it.
                  <Snackbar
                    key={roleChange.id}
                    open
                    autoHideDuration={ROLE_CHANGE_NOTICE_MS}
                    onClose={(_event, reason) => {
                      if (reason !== 'clickaway') {
                        dismissRoleChange();
                      }
                    }}
                    anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
                  >
                    <Alert severity="info" onClose={dismissRoleChange}>
                      {roleChange.text}
                    </Alert>
                  </Snackbar>
                )}
              </Box>
            </PageChromeContext.Provider>
          </NoticeProvider>
        </NoticeCornerContext.Provider>
      </WorkContextContext.Provider>
    </AccessCheckContext.Provider>
  );
}
