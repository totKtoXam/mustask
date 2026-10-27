import { useEffect, useId, useRef, useState, type KeyboardEvent, type MouseEvent } from 'react';
import { Link as RouterLink, useLocation, useNavigate } from 'react-router-dom';
import IconButton from '@mui/material/IconButton';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import Tooltip from '@mui/material/Tooltip';
import ArrowCircleLeftOutlinedIcon from '@mui/icons-material/ArrowCircleLeftOutlined';
import ArrowCircleRightOutlinedIcon from '@mui/icons-material/ArrowCircleRightOutlined';
import ViewWeekOutlinedIcon from '@mui/icons-material/ViewWeekOutlined';
import { useWorkContext } from '../../../shared/lib/workContext';
import { teamFilterSearch } from '../../workitems';
import { BacklogIcon, ThinChevronDownIcon } from '../../../shared/ui';
import { PageHeader, type Command } from '../../../shared/ui/PageHeader';
import { projectFilterSearch } from '../hooks/useViewFilter';
import type { BoardLevelCode } from '../lib/levels';
import { scopeProjectId, scopeTeamId, type BoardScope } from '../lib/scope';
import { backlogPath, boardPath, projectBacklogPath, projectBoardPath } from '../paths';
import { LevelMenu } from './LevelMenu';

/** The two pages that wear the head (feat-027 BR-T-16). */
export type TeamPage = 'backlog' | 'board';

/**
 * What each page calls itself in the head: the tab, the browser's tab (which
 * says which page of the team it is, the `h1` being the team), the fallback
 * `h1` while the team's name is not known, and the command to the other page
 * (feat-027 design § 3.1, § 3.3).
 */
const PAGES = {
  backlog: {
    name: 'Бэклоги',
    tab: 'Бэклог',
    toOther: 'Показать канбан',
  },
  board: {
    name: 'Канбан',
    tab: 'Канбан',
    toOther: 'Показать бэклог',
  },
} as const satisfies Record<TeamPage, { name: string; tab: string; toOther: string }>;

const CHANGE_TEAM_LABEL = 'Сменить команду';
/** feat-040 design § 3.2: the menu lists the organization's teams, no longer the project's. */
const TEAMS_LABEL = 'Команды';
const VIEWS_LABEL = 'Вид страницы';

/**
 * The navigation state a team chosen in «Сменить команду» carries (F-9): the page
 * is keyed by its team, so the page of the other team is made anew, with its own
 * «Сменить команду» — and the one the user pressed is gone, and with it the focus.
 * The new button takes focus when it finds this on arrival.
 */
const TEAM_CHOSEN_STATE = { teamChosen: true } as const;

function isTeamChosen(state: unknown): boolean {
  return (
    typeof state === 'object' &&
    state !== null &&
    (state as { teamChosen?: unknown }).teamChosen === true
  );
}

/** The address of the same page under a scope — a team's or the project's — at a level. */
function pagePath(
  page: TeamPage,
  organizationId: string,
  scope: BoardScope,
  level: BoardLevelCode,
): string {
  if (scope.kind === 'project') {
    return page === 'backlog'
      ? projectBacklogPath(organizationId, scope.projectId, level)
      : projectBoardPath(organizationId, scope.projectId, level);
  }
  return page === 'backlog'
    ? backlogPath(organizationId, scope.teamId, level)
    : boardPath(organizationId, scope.teamId, level);
}

/**
 * The head of «Бэклоги» and «Канбан» (feat-027 BR-T-16, design § 3): the team's
 * name as the `h1` with a drop-down to another team, one tab under it — «Бэклог» /
 * «Канбан» — with the level drop-down at its right end, and at the top right the
 * other page's grey button, on «Бэклоги» after the primary «Новый рабочий элемент».
 * It is `PageHeader`'s team variant.
 *
 * feat-040 (BR-TS-11): a team's page is at the team's address, so the drop-down lists the
 * organization's teams — the switcher's team level, in its order — with this one selected,
 * and no longer «Весь проект». The project's own pages (feat-039) have the same head with the
 * project's name as the `h1` and **no drop-down button**, so no menu puts a team under a project;
 * their tab, level drop-down and grey button lead to the project's addresses.
 *
 * The name is the page's own read once it is there, else the shell's list for the address's team
 * (a project page: the shell's name for the project), else the page's own name, so the `h1` is
 * never empty. The drop-down is built from the shell's list — no request of its own — and is there
 * whenever the organization's teams are read; while they load or have failed there is no button.
 *
 * The project filter of a team's views (BR-TS-8) is kept by the level menu, the tab and the grey
 * command; the team filter of the project's (mod-020) by the level menu and the tab. A team chosen
 * in the drop-down starts unfiltered.
 */
export function TeamHead({
  page,
  organizationId,
  scope,
  level,
  name,
  primaryAction,
}: {
  page: TeamPage;
  organizationId: string;
  scope: BoardScope;
  level: BoardLevelCode;
  /** The team's name, or the project's on a project page, from the page's own read, once there is one. */
  name: string | undefined;
  /** «Новый рабочий элемент», once the read gives the level's types; none on «Канбан». */
  primaryAction?: Command;
}) {
  const own = PAGES[page];
  const teamId = scopeTeamId(scope);
  const projectId = scopeProjectId(scope);
  const context = useWorkContext();
  // A link to the same view carries its filter: another level and the page's own tab, on both kinds
  // of view; on a team's also the grey command to the other page, since the project filter is the
  // view's of that team (BR-TS-8). The other kinds of link — another team, the switcher — start
  // unfiltered. The project's grey command carries no filter: mod-020 starts the other page unfiltered.
  const { search } = useLocation();
  const filterSearch =
    scope.kind === 'project' ? teamFilterSearch(search) : projectFilterSearch(search);
  const otherPageSearch = scope.kind === 'team' ? filterSearch : '';
  const organization = context.organizations.find((candidate) => candidate.id === organizationId);
  const known =
    teamId !== null
      ? organization?.teams.find((candidate) => candidate.id === teamId)
      : organization?.projects.find((candidate) => candidate.id === projectId);
  const teamsReady =
    context.status === 'ready' &&
    context.levelStatus?.team !== 'loading' &&
    context.levelStatus?.team !== 'failed';
  const teams = teamsReady && organization ? organization.teams : [];

  const otherPage = page === 'backlog' ? 'board' : 'backlog';
  return (
    <PageHeader
      variant="team"
      title={name ?? known?.name ?? own.name}
      documentTitle={own.name}
      titleIcon={
        page === 'backlog' ? (
          <BacklogIcon aria-hidden="true" sx={{ fontSize: 24, color: 'text.primary' }} />
        ) : (
          <ViewWeekOutlinedIcon aria-hidden="true" sx={{ fontSize: 24, color: 'text.primary' }} />
        )
      }
      titleControl={
        teamId !== null && teams.length > 0 ? (
          <TeamMenu
            teams={teams}
            currentId={teamId}
            pathOf={(id) => pagePath(page, organizationId, { kind: 'team', teamId: id }, level)}
          />
        ) : undefined
      }
      primaryAction={primaryAction}
      commands={[
        {
          label: own.toOther,
          icon:
            page === 'backlog' ? <ArrowCircleRightOutlinedIcon /> : <ArrowCircleLeftOutlinedIcon />,
          to: `${pagePath(otherPage, organizationId, scope, level)}${otherPageSearch}`,
          tone: 'neutral',
        },
      ]}
      views={[
        {
          label: own.tab,
          to: `${pagePath(page, organizationId, scope, level)}${filterSearch}`,
        },
      ]}
      viewsLabel={VIEWS_LABEL}
      viewsEnd={
        <LevelMenu
          level={level}
          pathOf={(next) => `${pagePath(page, organizationId, scope, next)}${filterSearch}`}
        />
      }
    />
  );
}

/**
 * «Сменить команду» (feat-027 design § 3.3): the chevron after the team's name
 * and its menu «Команды» of the organization's teams, the current one selected and
 * `aria-current="page"`; each is a link to the same page of that team at the
 * same level (feat-040 design § 3.2). Keyboard per mod-015 § 12.4.
 */
function TeamMenu({
  teams,
  currentId,
  pathOf,
}: {
  teams: readonly { id: string; name: string }[];
  /** The team whose page this is. */
  currentId: string;
  pathOf(teamId: string): string;
}) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const buttonId = useId();
  const menuId = useId();
  const open = anchor !== null;
  const button = useRef<HTMLButtonElement>(null);
  const location = useLocation();
  const navigate = useNavigate();
  const arrivedByMenu = isTeamChosen(location.state);

  // The user chose this team in the menu of the page before: focus is on the control
  // that did it (F-9). The state is spent at once, so going back to this entry later is
  // a plain visit.
  useEffect(() => {
    if (!arrivedByMenu) {
      return;
    }
    button.current?.focus();
    navigate(
      { pathname: location.pathname, search: location.search, hash: location.hash },
      { replace: true, state: null },
    );
  }, [arrivedByMenu, navigate, location.pathname, location.search, location.hash]);

  function openMenu(event: MouseEvent<HTMLElement> | KeyboardEvent<HTMLElement>) {
    setAnchor(event.currentTarget);
  }

  return (
    <>
      <Tooltip title={CHANGE_TEAM_LABEL}>
        <IconButton
          id={buttonId}
          ref={button}
          size="small"
          aria-label={CHANGE_TEAM_LABEL}
          aria-haspopup="menu"
          aria-expanded={open ? 'true' : 'false'}
          aria-controls={open ? menuId : undefined}
          onClick={openMenu}
          onKeyDown={(event: KeyboardEvent<HTMLElement>) => {
            if (event.key === 'ArrowDown') {
              event.preventDefault();
              openMenu(event);
            }
          }}
          sx={(theme) => ({
            width: theme.layout.iconAction.chevron.box,
            height: theme.layout.iconAction.chevron.box,
            p: 0,
            // The glyph's ink starts about 6 px inside its 24 px box (the 2 px
            // the 20 px glyph is inset and the path's own 4.5 units), which with
            // the title group's 10 px gap left the chevron 16 px after the
            // name; the reference's is 12 (V-6).
            ml: -0.5,
            color: 'text.primary',
            '& .MuiSvgIcon-root': { fontSize: theme.layout.iconAction.chevron.glyph },
          })}
        >
          <ThinChevronDownIcon />
        </IconButton>
      </Tooltip>
      <Menu
        id={menuId}
        anchorEl={anchor}
        open={open}
        onClose={() => setAnchor(null)}
        variant="selectedMenu"
        slotProps={{ list: { 'aria-label': TEAMS_LABEL } }}
      >
        {teams.map((team) => {
          const selected = team.id === currentId;
          return (
            <MenuItem
              key={team.id}
              component={RouterLink}
              to={pathOf(team.id)}
              state={TEAM_CHOSEN_STATE}
              selected={selected}
              aria-current={selected ? 'page' : undefined}
              onClick={() => setAnchor(null)}
            >
              {team.name}
            </MenuItem>
          );
        })}
      </Menu>
    </>
  );
}
