import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  useQuery,
  useQueryClient,
  type QueryClient,
  type UseQueryResult,
} from '@tanstack/react-query';
import { HOME_PATH } from '../../../shared/config/paths';
import {
  defaultEntry,
  type SwitchTargetResolver,
  type WorkContextFallback,
  type WorkContextLevel,
  type WorkContextOrganization,
  type WorkContextSelection,
  type WorkContextSource,
  type WorkContextStatus,
} from '../../../shared/lib/workContext';
import {
  getOrganizationTeams,
  getOrganizations,
  getProjects,
  getTeams,
  type ProjectTeam,
  type TeamListItem,
} from '../api/organizationsApi';
import { organizationsKeys } from '../api/queryKeys';
import { ORGANIZATIONS_PATH } from '../paths';
import { addressContext } from './addressContext';
import {
  LOADING_LIST,
  addressSync,
  effectiveId,
  organizationTeamLevel,
  resolveSelection,
  sameSelection,
  type LevelList,
} from './resolveSelection';
import { readRemembered, writeRemembered } from './storage';

export interface OrganizationsContextSourceOptions {
  /** `false` while a test hands the shell its own source: nothing is read or stored. */
  enabled: boolean;
  /** The token's `sub`: the remembered selection's key (design § 3.4). */
  subject?: string;
  /** Where a choice at a level the address carries moves the page (D-o). */
  resolveSwitchTarget?: SwitchTargetResolver;
}

/**
 * Other users' changes show when the window regains focus (design § 3.1,
 * P-6): the switcher's three lists opt in, against the client's default.
 */
const LEVEL_QUERY_OPTIONS = { refetchOnWindowFocus: true } as const;

const NOTHING: LevelList['items'] = [];

function statusOf(query: UseQueryResult<unknown>): WorkContextStatus {
  if (query.isSuccess) {
    return 'ready';
  }
  return query.isError && !query.isFetching ? 'failed' : 'loading';
}

function listOf(query: UseQueryResult<LevelList['items']>): LevelList {
  const status = statusOf(query);
  return { status, items: status === 'ready' ? (query.data ?? NOTHING) : NOTHING };
}

/** The organization's teams as the address sync reads them: the team level without the default hint. */
function teamsListOf(query: UseQueryResult<readonly TeamListItem[]>): LevelList {
  const status = statusOf(query);
  return {
    status,
    items: status === 'ready' ? organizationTeamLevel(query.data ?? []) : NOTHING,
  };
}

/** The effective project's default team: the linked team its settings name, else its only one (mod-020). */
function defaultTeamIdOf(linked: readonly ProjectTeam[] | undefined): string | undefined {
  return linked?.find((team) => Boolean(team.defaultMark))?.id;
}

/**
 * The team level (feat-040 BR-TS-10): the organization's teams, the user's own first, with the
 * effective project's default team marked once its linked teams are read. A team the user chose
 * and the list holds needs no default, so the level is ready with the teams alone; with none
 * chosen, the default is read before the level is: showing the first entry and then swapping it
 * for the default would move «Бэклоги» under the user's cursor. A linked-teams read that fails
 * names no default — the hint is best-effort, the level falls back to its first entry.
 */
function teamLevelOf(
  teams: UseQueryResult<readonly TeamListItem[]>,
  linked: UseQueryResult<readonly ProjectTeam[]>,
  chosenTeamId: string | undefined,
): LevelList {
  const status = statusOf(teams);
  if (status !== 'ready') {
    return { status, items: NOTHING };
  }
  const data = teams.data ?? [];
  const held = chosenTeamId !== undefined && data.some((team) => team.id === chosenTeamId);
  if (!held && linked.isFetching && !linked.isSuccess) {
    return { status: 'loading', items: NOTHING };
  }
  return { status: 'ready', items: organizationTeamLevel(data, defaultTeamIdOf(linked.data)) };
}

/** The address sync of the page on screen: decided once per pathname. */
interface SyncState {
  path: string;
  done: boolean;
}

/** The levels below a choice that the address carries, whose first entries the move needs. */
interface FirstEntriesWanted {
  /** An organization chosen on a project-level page: its first project. */
  project: boolean;
  /** An organization chosen on a team-level page: the team its switcher falls back to. */
  team: boolean;
}

/**
 * The selection a move asks the resolver with: the choice, its first project
 * where the address carries a project, and the team the new organization's switcher falls back
 * to — the first project's default team (the one the project names or has alone), else the
 * level's first entry, the user's own first (mod-020, mod-016 D-5; feat-040 BR-TS-10) — where
 * the address carries a team, each read first on the switcher's own keys (design § 2.2, § 3.5;
 * plan-frontend § 4.5, R-8). A level with no entry stays unset, and the resolver answers
 * «Главная» for it (mod-015 BR-18: the entry is not visible there).
 */
async function withFirstEntries(
  queryClient: QueryClient,
  next: WorkContextSelection,
  wanted: FirstEntriesWanted,
): Promise<WorkContextSelection> {
  const { organizationId } = next;
  if (organizationId === undefined) {
    return next;
  }
  let { projectId } = next;
  if (wanted.project || wanted.team) {
    const projectsOf = () =>
      queryClient.fetchQuery({
        queryKey: organizationsKeys.projects(organizationId, 'active'),
        queryFn: () => getProjects(organizationId, 'active'),
      });
    if (wanted.project) {
      projectId = (await projectsOf())[0]?.id;
    } else {
      // A team page names no project: the project only decides the default team, which is best-effort.
      projectId = await projectsOf().then(
        (projects) => projects[0]?.id,
        () => undefined,
      );
    }
  }
  if (!wanted.team) {
    return { organizationId, projectId };
  }
  const teams = await queryClient.fetchQuery({
    queryKey: organizationsKeys.orgTeams(organizationId),
    queryFn: () => getOrganizationTeams(organizationId),
  });
  const hintOf = projectId;
  const linked =
    hintOf === undefined
      ? undefined
      : await queryClient
          .fetchQuery({
            queryKey: organizationsKeys.teams(organizationId, hintOf),
            queryFn: () => getTeams(organizationId, hintOf),
          })
          .catch(() => undefined);
  const level = organizationTeamLevel(teams, defaultTeamIdOf(linked));
  return { organizationId, projectId, teamId: defaultEntry(level)?.id };
}

/**
 * The switcher's data source on real lists (US-8; D-b, D-n, D-o; design § 3).
 * It reads one level at a time — the active organizations, the effective
 * organization's active projects and its active teams (feat-040 BR-TS-10: the
 * interim team level is the organization's, whatever its projects; the effective
 * project's linked teams are read for their default only) —
 * on the same query keys as the pages, so a page's write refreshes the
 * switcher through its invalidation (design § 3.7). It resolves the
 * selection from the address, then the remembered or chosen selection, then
 * the first entries (P-1 (a): a carried level is found when its active list
 * holds the id, for every page alike); remembers it under
 * `mustask.context.<sub>`; raises the fallback notice when a remembered or
 * chosen entry is gone (P-2 (a)); and moves the page on a choice at a level
 * the address carries, through the resolver `app/` hands it, once the first
 * entries of the carried levels below the choice are read.
 */
export function useOrganizationsContextSource({
  enabled,
  subject,
  resolveSwitchTarget,
}: OrganizationsContextSourceOptions): WorkContextSource {
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [loadedSubject, setLoadedSubject] = useState(subject);
  const [chosen, setChosen] = useState<WorkContextSelection>(() =>
    enabled ? readRemembered(subject) : {},
  );
  // The remembered selection is read once the subject is known.
  if (loadedSubject !== subject) {
    setLoadedSubject(subject);
    setChosen(enabled ? readRemembered(subject) : {});
  }

  const [sync, setSync] = useState<SyncState>({ path: pathname, done: false });
  if (sync.path !== pathname) {
    setSync({ path: pathname, done: false });
  }
  const syncDone = sync.path === pathname && sync.done;
  const [writes, setWrites] = useState(0);
  const [fallback, setFallback] = useState<WorkContextFallback | null>(null);

  const organizationsQuery = useQuery({
    queryKey: organizationsKeys.organizations('active'),
    queryFn: () => getOrganizations('active'),
    enabled,
    ...LEVEL_QUERY_OPTIONS,
  });
  const organizations = listOf(organizationsQuery);
  const organizationId = effectiveId(organizations, chosen.organizationId);

  const projectsQuery = useQuery({
    queryKey: organizationsKeys.projects(organizationId ?? '', 'active'),
    queryFn: () => getProjects(organizationId ?? '', 'active'),
    enabled: enabled && organizationId !== undefined,
    ...LEVEL_QUERY_OPTIONS,
  });
  const projects = organizationId ? listOf(projectsQuery) : LOADING_LIST;
  const projectId = effectiveId(projects, chosen.projectId);

  // The address's organization's lists, while it is not yet the effective
  // one: a project or a team the address carries is found before anything
  // is set (plan-frontend § 4.4).
  const address = addressContext(pathname);
  const addressOrganizationId =
    !syncDone &&
    address.organizationId !== organizationId &&
    organizations.items.some((item) => item.id === address.organizationId)
      ? address.organizationId
      : undefined;
  const addressProjectsQuery = useQuery({
    queryKey: organizationsKeys.projects(addressOrganizationId ?? '', 'active'),
    queryFn: () => getProjects(addressOrganizationId ?? '', 'active'),
    enabled: enabled && addressOrganizationId !== undefined && address.projectId !== undefined,
    ...LEVEL_QUERY_OPTIONS,
  });
  const addressTeamsQuery = useQuery({
    queryKey: organizationsKeys.orgTeams(addressOrganizationId ?? ''),
    queryFn: () => getOrganizationTeams(addressOrganizationId ?? ''),
    enabled: enabled && addressOrganizationId !== undefined && address.teamId !== undefined,
    ...LEVEL_QUERY_OPTIONS,
  });

  // The team level (feat-040 BR-TS-10): the organization's teams, whatever its projects. The
  // effective project's linked teams — the pages read the same key — name its default team, the
  // fallback of a level with no team chosen (mod-020).
  const orgTeamsQuery = useQuery({
    queryKey: organizationsKeys.orgTeams(organizationId ?? ''),
    queryFn: () => getOrganizationTeams(organizationId ?? ''),
    enabled: enabled && organizationId !== undefined,
    ...LEVEL_QUERY_OPTIONS,
  });
  const linkedTeamsQuery = useQuery({
    queryKey: organizationsKeys.teams(organizationId ?? '', projectId ?? ''),
    queryFn: () => getTeams(organizationId ?? '', projectId ?? ''),
    enabled: enabled && organizationId !== undefined && projectId !== undefined,
    ...LEVEL_QUERY_OPTIONS,
  });
  const teams = organizationId
    ? teamLevelOf(orgTeamsQuery, linkedTeamsQuery, chosen.teamId)
    : LOADING_LIST;

  const addressProjects =
    address.organizationId === organizationId ? projects : listOf(addressProjectsQuery);
  const addressTeams =
    address.organizationId === organizationId
      ? teamsListOf(orgTeamsQuery)
      : teamsListOf(addressTeamsQuery);

  // 1. The address, once per pathname, once its lists are read.
  if (enabled && !syncDone) {
    const decision = addressSync({
      address,
      chosen,
      organizations,
      addressProjects,
      addressTeams,
    });
    if (decision.kind !== 'wait') {
      setSync({ path: pathname, done: true });
      if (decision.kind === 'set') {
        setChosen(decision.selection);
        setWrites((count) => count + 1);
      }
    }
  }

  // 2. The remembered or chosen selection, then the first entries.
  const resolution = resolveSelection({
    chosen,
    organizations,
    projectsOf: (id) => (id === organizationId ? projects : LOADING_LIST),
    teamsOf: (id) => (id === organizationId ? teams : LOADING_LIST),
  });

  // 3. A gone entry is replaced, remembered and announced once — never while
  //    the address is still to set the levels it carries.
  if (enabled && syncDone && resolution.replacement) {
    const { selection, names } = resolution.replacement;
    if (!sameSelection(selection, chosen)) {
      setChosen(selection);
      setWrites((count) => count + 1);
      if (names) {
        setFallback((current) => ({ id: (current?.id ?? 0) + 1, names }));
      }
    }
  }

  const effectiveOrganizationId = resolution.selection.organizationId;
  const effectiveProjectId = resolution.selection.projectId;
  const effectiveTeamId = resolution.selection.teamId;
  const effective = useMemo<WorkContextSelection>(
    () => ({
      organizationId: effectiveOrganizationId,
      projectId: effectiveProjectId,
      teamId: effectiveTeamId,
    }),
    [effectiveOrganizationId, effectiveProjectId, effectiveTeamId],
  );

  // Written on a choice, an address sync and a fallback, once every level is
  // read, so a reload shows the same context (design § 3.4). The team is remembered only when it
  // was chosen: a fallback team is derived again from the project, so a reload neither makes it
  // chosen nor a chosen one a fallback (feat-040 BR-TS-10).
  const { settled, chosenTeamId } = resolution;
  const remembered = useMemo<WorkContextSelection>(
    () => ({
      organizationId: effectiveOrganizationId,
      projectId: effectiveProjectId,
      teamId: chosenTeamId,
    }),
    [effectiveOrganizationId, effectiveProjectId, chosenTeamId],
  );
  useEffect(() => {
    if (enabled && writes > 0 && settled) {
      writeRemembered(subject, remembered);
    }
  }, [enabled, subject, writes, settled, remembered]);

  // The move of a choice that waits for a read. Only the latest choice moves
  // the page, and only from the page it was made on (design § 3.6: only a
  // choice moves pages): a later choice or another page drops it.
  const pendingMove = useRef<object | null>(null);
  useEffect(() => {
    pendingMove.current = null;
  }, [pathname]);

  const select = useCallback(
    (level: WorkContextLevel, id: string) => {
      // An organization choice drops the team (one remembered selection, mod-002 BR-M1-24); a
      // project choice keeps a chosen team and applies the fallback again otherwise (feat-040
      // BR-TS-10); a team choice is a choice.
      const next: WorkContextSelection =
        level === 'organization'
          ? { organizationId: id }
          : level === 'project'
            ? { organizationId: effective.organizationId, projectId: id, teamId: chosenTeamId }
            : {
                organizationId: effective.organizationId,
                projectId: effective.projectId,
                teamId: id,
              };
      setChosen(next);
      setWrites((count) => count + 1);
      // The choice is not undone by the address it leaves (plan-frontend § 4.5).
      setSync({ path: pathname, done: true });
      pendingMove.current = null;

      const carried = addressContext(pathname);
      const carriesLevel =
        level === 'organization'
          ? carried.organizationId !== undefined
          : level === 'project'
            ? carried.projectId !== undefined
            : carried.teamId !== undefined;
      if (!carriesLevel || !resolveSwitchTarget) {
        return;
      }
      const moveTo = (selection: WorkContextSelection) =>
        navigate(resolveSwitchTarget({ pathname, carried, next: selection }));

      // The levels the address carries below the choice take their first
      // entries, read before the page moves: a project-level page goes to the
      // new organization's first project (design § 2.2, plan R-8), a
      // team-level page to the team its switcher falls back to (design
      // § 3.5; mod-015 BR-18 for «Бэклоги» and «Канбан»). A project choice on a team page
      // carries no level and moves nothing (feat-040 BR-TS-10). The page
      // stays while the read is pending; a failed read opens «Главная»
      // (plan-frontend § 4.5).
      const wanted: FirstEntriesWanted = {
        project: level === 'organization' && carried.projectId !== undefined,
        team: level === 'organization' && carried.teamId !== undefined,
      };
      if (!wanted.project && !wanted.team) {
        moveTo(next);
        return;
      }
      const move = {};
      pendingMove.current = move;
      withFirstEntries(queryClient, next, wanted).then(
        (selection) => {
          if (pendingMove.current === move) {
            moveTo(selection);
          }
        },
        () => {
          if (pendingMove.current === move) {
            navigate(HOME_PATH);
          }
        },
      );
    },
    [effective, chosenTeamId, pathname, navigate, queryClient, resolveSwitchTarget],
  );

  const refetchOrganizations = organizationsQuery.refetch;
  const refetchProjects = projectsQuery.refetch;
  const refetchTeams = orgTeamsQuery.refetch;
  const retry = useCallback(
    (level?: WorkContextLevel) => {
      const refetch =
        level === 'project'
          ? refetchProjects
          : level === 'team'
            ? refetchTeams
            : refetchOrganizations;
      void refetch();
    },
    [refetchOrganizations, refetchProjects, refetchTeams],
  );

  const dismissFallback = useCallback(() => setFallback(null), []);

  const organizationItems = organizations.items;
  const projectItems = projects.items;
  const teamItems = teams.items;
  const tree = useMemo<WorkContextOrganization[]>(
    () =>
      organizationItems.map((organization) => ({
        id: organization.id,
        name: organization.name,
        projects:
          organization.id === effectiveOrganizationId
            ? projectItems.map((project) => ({ id: project.id, name: project.name }))
            : [],
        // The team level is the organization's (feat-040 BR-TS-10).
        teams:
          organization.id === effectiveOrganizationId
            ? teamItems.map((team) => ({
                id: team.id,
                name: team.name,
                isMember: team.isMember,
                isDefault: team.isDefault,
              }))
            : [],
      })),
    [organizationItems, projectItems, teamItems, effectiveOrganizationId],
  );

  const { status } = resolution;
  const projectStatus = resolution.levelStatus.project;
  const teamStatus = resolution.levelStatus.team;
  return useMemo<WorkContextSource>(
    () => ({
      status,
      levelStatus: { project: projectStatus, team: teamStatus },
      organizations: tree,
      selection: effective,
      isReal: true,
      organizationListPath: ORGANIZATIONS_PATH,
      select,
      retry,
      fallback,
      dismissFallback,
    }),
    [status, projectStatus, teamStatus, tree, effective, select, retry, fallback, dismissFallback],
  );
}
