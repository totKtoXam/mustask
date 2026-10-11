import {
  useId,
  useLayoutEffect,
  useRef,
  useState,
  type MouseEvent,
  type ReactElement,
  type ReactNode,
  type Ref,
} from 'react';
import { Link as RouterLink, useLocation } from 'react-router-dom';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import Stack from '@mui/material/Stack';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Typography from '@mui/material/Typography';
import { useTheme } from '@mui/material/styles';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import MoreHorizIcon from '@mui/icons-material/MoreHoriz';
import { useDocumentTitle } from '../lib/documentTitle';
import { splitCommands } from './commandOverflow';
import { usePageChrome, type TrailSegment } from './pageChrome';
import { PageTrail } from './PageTrail';

/** A page command: a button with `onClick`, or a link with `to`. */
export interface Command {
  label: string;
  /** The leading icon (design § 6.1: every command has one). */
  icon: ReactElement;
  /**
   * Receives the click when the command is a button on the bar, so a command
   * that opens a menu anchors it on `currentTarget`; chosen from «Ещё
   * действия» it is called without one.
   */
  onClick?: (event?: MouseEvent<HTMLElement>) => void;
  to?: string;
  /** Unavailable for a reason of state (BR-9); a missing code hides the command instead. */
  disabled?: boolean;
  /**
   * The command opens a menu (mod-015 § 10.1, «Новый рабочий элемент»): the
   * button gets `aria-haspopup="menu"` and `aria-expanded` from this flag, and
   * `ExpandMore` as its trailing icon. Absent: a plain command, as before.
   */
  menuOpen?: boolean;
  /**
   * `menuOpen` is set but the button draws no trailing `ExpandMore`
   * (feat-027 design § 3.5: the reference screen's primary has none);
   * `aria-haspopup` and `aria-expanded` stay, so the picker is still
   * announced. Absent: as before.
   */
  hideMenuIndicator?: boolean;
  /**
   * `neutral`: the command is the grey filled button of feat-027 design § 2.5
   * instead of a text button — a neutral colour, so the header still has one
   * accent action. Absent: a text button, as before.
   */
  tone?: 'neutral';
}

/** One view of a page: a tab that is its own address (design § 6.1). */
export interface PageView {
  label: string;
  to: string;
}

export interface PageHeaderProps {
  /** The page's only level-1 heading; the browser tab repeats it (BR-6). */
  title: string;
  /** Ancestors below the ones the shell derives from its navigation model. */
  ancestors?: readonly TrailSegment[];
  /** At most one filled command per header — one by type (US-6, BR-2). */
  primaryAction?: Command;
  /** Secondary commands: text buttons with a leading icon, in this order. */
  commands?: readonly Command[];
  views?: readonly PageView[];
  /**
   * The views' own accessible name, e.g. «Организации по статусу» (mod-002
   * design § 5.2). Absent: the tabs are named by the title, as before.
   */
  viewsLabel?: string;
  /**
   * A handle on the title, for a page that moves focus to it — after a retry
   * that succeeded (mod-001 design § Focus and announcements). Given, the
   * `h1` takes the ref and `tabIndex={-1}`; absent, the title is as before.
   */
  titleRef?: Ref<HTMLHeadingElement>;
  /**
   * What the browser's tab reads when it is not the `title` (feat-027 design
   * § 3.3: the `h1` is the team, the tab says which page of it).
   */
  documentTitle?: string;
  /** The leading icon of the first row, `aria-hidden` by the caller (feat-027 design § 3.2). */
  titleIcon?: ReactElement;
  /** A control right after the `h1`, in the same row — the team drop-down (feat-027 design § 3.3). */
  titleControl?: ReactNode;
  /** The right end of the views row — the level drop-down (feat-027 design § 3.4). */
  viewsEnd?: ReactNode;
  /**
   * `team`: the spacing of the team head (feat-027 design § 3.2) — 28 px above
   * the first row, the views row 12 px under it, 20 px below — the title
   * group's 10 px gap, and a tab that stands flush with the content's left
   * edge. Absent: the spacing every other page has.
   */
  variant?: 'team';
}

/** Class names the tests and later patterns can address. */
const pageHeaderClasses = {
  root: 'PageHeader-root',
  commands: 'PageHeader-commands',
} as const;

const NO_ANCESTORS: readonly TrailSegment[] = [];
const NO_COMMANDS: readonly Command[] = [];

function commandProps(command: Command) {
  if (command.to) {
    return { component: RouterLink, to: command.to } as const;
  }
  const opensMenu = command.menuOpen !== undefined;
  return {
    onClick: command.onClick,
    endIcon: opensMenu && !command.hideMenuIndicator ? <ExpandMoreIcon /> : undefined,
    'aria-haspopup': opensMenu ? ('menu' as const) : undefined,
    'aria-expanded': opensMenu
      ? command.menuOpen
        ? ('true' as const)
        : ('false' as const)
      : undefined,
  };
}

/**
 * The page header (US-6, design § 6): the title at the left as the page's
 * only `h1`, the command bar at the right, and the page's views as tabs
 * below. Below 1024 px the shell asks for the trail here, above the title.
 */
export function PageHeader({
  title,
  ancestors = NO_ANCESTORS,
  primaryAction,
  commands = NO_COMMANDS,
  views,
  viewsLabel,
  titleRef,
  documentTitle,
  titleIcon,
  titleControl,
  viewsEnd,
  variant,
}: PageHeaderProps) {
  useDocumentTitle(documentTitle ?? title);
  const { registerHeader, trail, trailPlacement } = usePageChrome();
  const titleId = useId();

  // Keyed by the serialized ancestors, so a new array on every render of the
  // page does not register again and again.
  const ancestorsKey = JSON.stringify(ancestors);
  useLayoutEffect(
    () => registerHeader(JSON.parse(ancestorsKey) as TrailSegment[]),
    [registerHeader, ancestorsKey],
  );

  const commandsKey = commands.map((command) => command.label).join('\u0000');

  return (
    <Box
      className={pageHeaderClasses.root}
      sx={variant === 'team' ? { pt: 3.5, pb: 2.5 } : { pt: 2, pb: 1 }}
    >
      {trailPlacement === 'header' && trail.length > 0 && (
        <Box sx={{ mb: 1 }}>
          <PageTrail segments={trail} />
        </Box>
      )}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Box
          sx={{
            flex: '0 1 auto',
            minWidth: 0,
            display: 'flex',
            alignItems: 'center',
            gap: variant === 'team' ? 1.25 : 1,
          }}
        >
          {titleIcon}
          <Typography
            variant="h1"
            component="h1"
            id={titleId}
            ref={titleRef}
            tabIndex={titleRef ? -1 : undefined}
            sx={{ flex: '0 1 auto', minWidth: 0, overflowWrap: 'anywhere' }}
          >
            {title}
          </Typography>
          {titleControl}
        </Box>
        <CommandBar key={commandsKey} primaryAction={primaryAction} commands={commands} />
      </Box>
      {views && views.length > 0 && (
        <PageViews
          views={views}
          labelledBy={titleId}
          label={viewsLabel}
          end={viewsEnd}
          variant={variant}
        />
      )}
    </Box>
  );
}

/**
 * The command bar. It keeps `density.controlHeight` with no command at all,
 * so a reader's header is as high as an administrator's (US-6). Where a
 * `ResizeObserver` exists it measures the bar, and secondary commands that do
 * not fit move into «Ещё действия», last first; without one (jsdom) every
 * command stays inline.
 */
function CommandBar({
  primaryAction,
  commands,
}: {
  primaryAction?: Command;
  commands: readonly Command[];
}) {
  const theme = useTheme();
  const barRef = useRef<HTMLDivElement>(null);
  const primaryRef = useRef<HTMLElement | null>(null);
  const commandRefs = useRef<(HTMLElement | null)[]>([]);
  const measuredWidths = useRef<number[]>([]);
  const [inlineCount, setInlineCount] = useState(commands.length);
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null);

  const commandCount = commands.length;
  useLayoutEffect(() => {
    const bar = barRef.current;
    if (!bar || typeof ResizeObserver === 'undefined' || commandCount === 0) {
      return undefined;
    }
    const gap = parseFloat(theme.spacing(1));
    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (!entry) {
        return;
      }
      // Commands still inline are measured now; the ones already in the menu
      // keep the width they had when they were last shown.
      commandRefs.current.forEach((element, index) => {
        if (element) {
          measuredWidths.current[index] = element.offsetWidth + gap;
        }
      });
      const primaryWidth = primaryRef.current ? primaryRef.current.offsetWidth + gap : 0;
      const widths = Array.from(
        { length: commandCount },
        (_, index) => measuredWidths.current[index] ?? 0,
      );
      const reserved = theme.density.controlHeight + gap;
      setInlineCount(splitCommands(widths, entry.contentRect.width - primaryWidth, reserved));
    });
    observer.observe(bar);
    return () => observer.disconnect();
  }, [commandCount, theme]);

  const inline = commands.slice(0, inlineCount);
  const overflow = commands.slice(inlineCount);

  function chooseFromMenu(command: Command) {
    setMenuAnchor(null);
    command.onClick?.();
  }

  return (
    <Stack
      ref={barRef}
      className={pageHeaderClasses.commands}
      direction="row"
      spacing={1}
      sx={{
        flex: '1 1 0',
        minWidth: 0,
        minHeight: (themeArg) => themeArg.density.controlHeight,
        justifyContent: 'flex-end',
        alignItems: 'center',
      }}
    >
      {primaryAction && (
        <Button
          ref={(element: HTMLElement | null) => {
            primaryRef.current = element;
          }}
          variant="contained"
          startIcon={primaryAction.icon}
          disabled={primaryAction.disabled}
          sx={{ flexShrink: 0 }}
          {...commandProps(primaryAction)}
        >
          {primaryAction.label}
        </Button>
      )}
      {inline.map((command, index) => (
        <Button
          key={command.label}
          ref={(element: HTMLElement | null) => {
            commandRefs.current[index] = element;
          }}
          variant={command.tone === 'neutral' ? 'contained' : 'text'}
          color={command.tone === 'neutral' ? 'inherit' : 'primary'}
          startIcon={command.icon}
          disabled={command.disabled}
          sx={{ flexShrink: 0, whiteSpace: 'nowrap' }}
          {...commandProps(command)}
        >
          {command.label}
        </Button>
      ))}
      {overflow.length > 0 && (
        <>
          <IconButton
            size="small"
            aria-label="Ещё действия"
            aria-haspopup="menu"
            aria-expanded={menuAnchor ? 'true' : 'false'}
            onClick={(event: MouseEvent<HTMLElement>) => setMenuAnchor(event.currentTarget)}
          >
            <MoreHorizIcon />
          </IconButton>
          <Menu
            anchorEl={menuAnchor}
            open={menuAnchor !== null}
            onClose={() => setMenuAnchor(null)}
            anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
            transformOrigin={{ vertical: 'top', horizontal: 'right' }}
          >
            {overflow.map((command) =>
              command.to ? (
                <MenuItem
                  key={command.label}
                  component={RouterLink}
                  to={command.to}
                  disabled={command.disabled}
                  onClick={() => setMenuAnchor(null)}
                >
                  <ListItemIcon sx={{ color: 'primary.main' }}>{command.icon}</ListItemIcon>
                  <ListItemText>{command.label}</ListItemText>
                </MenuItem>
              ) : (
                <MenuItem
                  key={command.label}
                  disabled={command.disabled}
                  onClick={() => chooseFromMenu(command)}
                >
                  <ListItemIcon sx={{ color: 'primary.main' }}>{command.icon}</ListItemIcon>
                  <ListItemText>{command.label}</ListItemText>
                </MenuItem>
              ),
            )}
          </Menu>
        </>
      )}
    </Stack>
  );
}

/** The page's views: tabs that are addresses, the current one taken from the address. */
function PageViews({
  views,
  labelledBy,
  label,
  end,
  variant,
}: {
  views: readonly PageView[];
  labelledBy: string;
  label?: string;
  end?: ReactNode;
  variant?: 'team';
}) {
  const { pathname } = useLocation();
  const selected =
    views.find((view) => view.to === pathname) ??
    [...views]
      .sort((first, second) => second.to.length - first.to.length)
      .find((view) => pathname.startsWith(`${view.to}/`));

  const team = variant === 'team';
  const tabs = (
    <Tabs
      value={selected?.to ?? false}
      aria-label={label}
      aria-labelledby={label === undefined ? labelledBy : undefined}
      sx={team ? undefined : { mt: 1 }}
    >
      {views.map((view) => (
        <Tab
          key={view.to}
          component={RouterLink}
          to={view.to}
          value={view.to}
          label={view.label}
          // The team head's tab stands flush with the table's left edge, its
          // underline the label's width (feat-027 design § 3.2). Its own
          // vertical padding — 16 px over the 17.5 px line and 6 px under — makes
          // the row 40 px (`density.tabHeight`) with the label 8 px above the
          // underline, as the reference draws it; the default 12 + 12 made it
          // 42 px, the label 14 px above the underline, and the head 2 px
          // taller than `layout.backlogTable.viewportOffset` assumes (V-2).
          sx={team ? { px: 0, minWidth: 0, mr: 2, pt: 2, pb: 0.75 } : undefined}
        />
      ))}
    </Tabs>
  );
  if (end === undefined && !team) {
    return tabs;
  }
  return (
    <Box
      sx={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: 2,
        mt: team ? 1.5 : 1,
      }}
    >
      {tabs}
      {end}
    </Box>
  );
}
