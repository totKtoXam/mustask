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
import { PageHeader, type Command } from '../../../shared/ui/PageHeader';
import type { BoardLevelCode } from '../lib/levels';
import { backlogPath, boardPath } from '../paths';
import { BacklogIcon } from './BacklogIcon';
import { LevelMenu } from './LevelMenu';
import { ThinChevronDownIcon } from './ThinIcons';

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

/** The team's address of the same page at the same level. */
function pagePath(
  page: TeamPage,
  organizationId: string,
  projectId: string,
  teamId: string,
  level: BoardLevelCode,
): string {
  return page === 'backlog'
    ? backlogPath(organizationId, projectId, teamId, level)
    : boardPath(organizationId, projectId, teamId, level);
}

/**
 * The head of «Бэклоги» and «Канбан» (feat-027 BR-T-16, design § 3): the team's
 * name as the `h1` with a drop-down to another team of the project, one tab
 * under it — «Бэклог» / «Канбан» — with the level drop-down at its right end,
 * and at the top right the other page's grey button, on «Бэклоги» after the
 * primary «Новый рабочий элемент». It is `PageHeader`'s team variant.
 *
 * The team's name is the page's own read once it is there, else the shell's
 * list of the project's teams for the address's `teamId`, else the page's own
 * name, so the `h1` is never empty. The drop-down is built from that list —
 * no request of its own — and is there whenever it is read, a project with one
 * team included; while it loads or has failed there is no button.
 */
export function TeamHead({
  page,
  organizationId,
  projectId,
  teamId,
  level,
  teamName,
  primaryAction,
}: {
  page: TeamPage;
  organizationId: string;
  projectId: string;
  teamId: string;
  level: BoardLevelCode;
  /** The team's name from the page's own read, once there is one. */
  teamName: string | undefined;
  /** «Новый рабочий элемент», once the read gives the level's types; none on «Канбан». */
  primaryAction?: Command;
}) {
  const own = PAGES[page];
  const context = useWorkContext();
  const project = context.organizations
    .find((organization) => organization.id === organizationId)
    ?.projects.find((candidate) => candidate.id === projectId);
  const known = project?.teams.find((candidate) => candidate.id === teamId);
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
      title={teamName ?? known?.name ?? own.name}
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
            pathOf={(id) => pagePath(page, organizationId, projectId, id, level)}
          />
        ) : undefined
      }
      primaryAction={primaryAction}
      commands={[
        {
          label: own.toOther,
          icon:
            page === 'backlog' ? <ArrowCircleRightOutlinedIcon /> : <ArrowCircleLeftOutlinedIcon />,
          to: pagePath(otherPage, organizationId, projectId, teamId, level),
          tone: 'neutral',
        },
      ]}
      views={[{ label: own.tab, to: pagePath(page, organizationId, projectId, teamId, level) }]}
      viewsLabel={VIEWS_LABEL}
      viewsEnd={
        <LevelMenu
          level={level}
          pathOf={(next) => pagePath(page, organizationId, projectId, teamId, next)}
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
 */
function TeamMenu({
  teams,
  currentId,
  pathOf,
}: {
  teams: readonly { id: string; name: string }[];
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
            width: theme.layout.backlogTable.chevronSlot,
            height: theme.layout.backlogTable.chevronSlot,
            p: 0,
            // The glyph's ink starts about 6 px inside its 24 px box (the 2 px
            // the 20 px glyph is inset and the path's own 4.5 units), which with
            // the title group's 10 px gap left the chevron 16 px after the
            // name; the reference's is 12 (V-6).
            ml: -0.5,
            color: 'text.primary',
            '& .MuiSvgIcon-root': { fontSize: theme.layout.backlogTable.chevronGlyph },
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
        {teams.map((team) => {
          const current = team.id === currentId;
          return (
            <MenuItem
              key={team.id}
              component={RouterLink}
              to={pathOf(team.id)}
              state={TEAM_CHOSEN_STATE}
              selected={current}
              aria-current={current ? 'page' : undefined}
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
