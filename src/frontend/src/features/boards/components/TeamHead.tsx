import { useEffect, useId, useRef, useState, type KeyboardEvent, type MouseEvent } from 'react';
import { Link as RouterLink, useLocation, useNavigate } from 'react-router-dom';
import Divider from '@mui/material/Divider';
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
import type { BoardLevelCode } from '../lib/levels';
import { PROJECT_SCOPE, scopeTeamId, teamScope, type BoardScope } from '../lib/scope';
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
const PROJECT_TEAMS_LABEL = 'Команды проекта';
const VIEWS_LABEL = 'Вид страницы';
/** feat-039 design § 3.4: the menu's last entry, which opens the project's own page. */
const WHOLE_PROJECT_LABEL = 'Весь проект';

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
  projectId: string,
  scope: BoardScope,
  level: BoardLevelCode,
): string {
  if (scope.kind === 'project') {
    return page === 'backlog'
      ? projectBacklogPath(organizationId, projectId, level)
      : projectBoardPath(organizationId, projectId, level);
  }
  return page === 'backlog'
    ? backlogPath(organizationId, projectId, scope.teamId, level)
    : boardPath(organizationId, projectId, scope.teamId, level);
}

/**
 * The head of «Бэклоги» and «Канбан» (feat-027 BR-T-16, design § 3): the team's
 * name as the `h1` with a drop-down to another team of the project, one tab
 * under it — «Бэклог» / «Канбан» — with the level drop-down at its right end,
 * and at the top right the other page's grey button, on «Бэклоги» after the
 * primary «Новый рабочий элемент». It is `PageHeader`'s team variant.
 *
 * feat-039 gives the project's own pages the same head (design § 3): the `h1` is
 * the project's name, the drop-down gains «Весь проект» after the teams — the
 * selected entry on a project page, where no team is — and the tab, the level
 * drop-down and the grey button lead to the project's addresses; there is no
 * primary command (BR-PB-6).
 *
 * The name is the page's own read once it is there, else the shell's list of the
 * project's teams for the address's `teamId` (a project page: the list's name for
 * the project), else the page's own name, so the `h1` is never empty. The
 * drop-down is built from that list — no request of its own — and is there
 * whenever it is read, a project with one team included; while it loads or has
 * failed there is no button.
 */
export function TeamHead({
  page,
  organizationId,
  projectId,
  scope,
  level,
  name,
  primaryAction,
}: {
  page: TeamPage;
  organizationId: string;
  projectId: string;
  scope: BoardScope;
  level: BoardLevelCode;
  /** The team's name, or the project's on a project page, from the page's own read, once there is one. */
  name: string | undefined;
  /** «Новый рабочий элемент», once the read gives the level's types; none on «Канбан». */
  primaryAction?: Command;
}) {
  const own = PAGES[page];
  const teamId = scopeTeamId(scope);
  const context = useWorkContext();
  // mod-020: the project's views keep their team filter in the address, and another level of the
  // same view carries it (BR-OT-8: a level change keeps it). A team's views have none, and the
  // other page, another team and the whole project are other views: they start unfiltered.
  const { search } = useLocation();
  const levelSearch = scope.kind === 'project' ? teamFilterSearch(search) : '';
  const project = context.organizations
    .find((organization) => organization.id === organizationId)
    ?.projects.find((candidate) => candidate.id === projectId);
  const known =
    teamId === null ? project : project?.teams.find((candidate) => candidate.id === teamId);
  const teamsReady =
    context.status === 'ready' &&
    context.levelStatus?.project !== 'loading' &&
    context.levelStatus?.project !== 'failed' &&
    context.levelStatus?.team !== 'loading' &&
    context.levelStatus?.team !== 'failed';
  const teams = teamsReady && project ? project.teams : [];

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
        teams.length > 0 ? (
          <TeamMenu
            teams={teams}
            currentId={teamId}
            pathOf={(id) => pagePath(page, organizationId, projectId, teamScope(id), level)}
            wholeProjectPath={pagePath(page, organizationId, projectId, PROJECT_SCOPE, level)}
          />
        ) : undefined
      }
      primaryAction={primaryAction}
      commands={[
        {
          label: own.toOther,
          icon:
            page === 'backlog' ? <ArrowCircleRightOutlinedIcon /> : <ArrowCircleLeftOutlinedIcon />,
          to: pagePath(otherPage, organizationId, projectId, scope, level),
          tone: 'neutral',
        },
      ]}
      views={[{ label: own.tab, to: pagePath(page, organizationId, projectId, scope, level) }]}
      viewsLabel={VIEWS_LABEL}
      viewsEnd={
        <LevelMenu
          level={level}
          pathOf={(next) =>
            `${pagePath(page, organizationId, projectId, scope, next)}${levelSearch}`
          }
        />
      }
    />
  );
}

/**
 * «Сменить команду» (feat-027 design § 3.3): the chevron after the team's name
 * and its menu of the project's teams, the current one selected and
 * `aria-current="page"`; each is a link to the same page of that team at the
 * same level. Keyboard per mod-015 § 12.4.
 *
 * feat-039 design § 3.4: «Весь проект» closes the list, after a divider — the
 * teams keep their order, their positions and the keyboard path among them. It
 * links to the project's page of the same entry and level, and is the selected
 * entry on a project page, where `currentId` is `null` and no team is.
 */
function TeamMenu({
  teams,
  currentId,
  pathOf,
  wholeProjectPath,
}: {
  teams: readonly { id: string; name: string }[];
  /** The team whose page this is, or `null` on the project's own page. */
  currentId: string | null;
  pathOf(teamId: string): string;
  wholeProjectPath: string;
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

  /** One entry of «Команды проекта» — a team, or the project as a whole — as a link to a page. */
  function renderEntry(key: string, label: string, to: string, selected: boolean) {
    return (
      <MenuItem
        key={key}
        component={RouterLink}
        to={to}
        state={TEAM_CHOSEN_STATE}
        selected={selected}
        aria-current={selected ? 'page' : undefined}
        onClick={() => setAnchor(null)}
      >
        {label}
      </MenuItem>
    );
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
        slotProps={{ list: { 'aria-label': PROJECT_TEAMS_LABEL } }}
      >
        {/* The entries and the divider are direct children of the menu: a Fragment is not
            supported by `MenuList`'s focus handling, and the divider, with no `tabindex`,
            is skipped by the arrows. */}
        {teams.map((team) =>
          renderEntry(team.id, team.name, pathOf(team.id), team.id === currentId),
        )}
        <Divider />
        {renderEntry('project', WHOLE_PROJECT_LABEL, wholeProjectPath, currentId === null)}
      </Menu>
    </>
  );
}
