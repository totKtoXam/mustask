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
import { getOrganizations, getProjects, getTeams } from '../api/organizationsApi';
import { organizationsKeys } from '../api/queryKeys';
import { ORGANIZATIONS_PATH } from '../paths';
import { addressContext } from './addressContext';
import {
  LOADING_LIST,
  addressSync,
  effectiveId,
  resolveSelection,
  sameSelection,
  teamLevelItems,
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

/** The address sync of the page on screen: decided once per pathname. */
interface SyncState {
  path: string;
  done: boolean;
}

/** The levels below a choice that the address carries, whose first entries the move needs. */
interface FirstEntriesWanted {
  /** An organization chosen on a project- or team-level page: its first project. */
  project: boolean;
  /** A project or organization chosen on a team-level page: that project's first team. */
  team: boolean;
}

/**
 * The selection a move asks the resolver with: the choice, its first project
 * and that project's default team (the one the project names or has alone, else
 * the first one the user is in, else the first; mod-020, mod-016 D-5) where the
 * address carries those levels, each read first
 * on the switcher's own keys (design § 2.2, § 3.5; plan-frontend § 4.5, R-8).
 * A level with no entry stays unset, and the resolver answers «Главная» for
 * it (mod-015 BR-18: the entry is not visible there).
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
  if (wanted.project) {
    const projects = await queryClient.fetchQuery({
      queryKey: organizationsKeys.projects(organizationId, 'active'),
      queryFn: () => getProjects(organizationId, 'active'),
    });
    projectId = projects[0]?.id;
  }
  if (!wanted.team || projectId === undefined) {
    return { organizationId, projectId };
  }
  const teamsOf = projectId;
  const teams = await queryClient.fetchQuery({
    queryKey: organizationsKeys.teams(organizationId, teamsOf),
    queryFn: () => getTeams(organizationId, teamsOf),
  });
  return { organizationId, projectId, teamId: defaultEntry(teamLevelItems(teams))?.id };
}

/**
 * The switcher's data source on real lists (US-8; D-b, D-n, D-o; design § 3).
 * It reads one level at a time — the active organizations, the effective
 * organization's active projects, the effective project's active teams —
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

  // The address's organization's projects, while it is not yet the
  // effective one: a project the address carries is found before anything
  // is set (plan-frontend § 4.4).
  const address = addressContext(pathname);
  const addressOrganizationId =
    !syncDone &&
    address.projectId !== undefined &&
    address.organizationId !== organizationId &&
    organizations.items.some((item) => item.id === address.organizationId)
      ? address.organizationId
      : undefined;
  const addressProjectsQuery = useQuery({
    queryKey: organizationsKeys.projects(addressOrganizationId ?? '', 'active'),
    queryFn: () => getProjects(addressOrganizationId ?? '', 'active'),
    enabled: enabled && addressOrganizationId !== undefined,
    ...LEVEL_QUERY_OPTIONS,
  });

  const teamsQuery = useQuery({
    queryKey: organizationsKeys.teams(organizationId ?? '', projectId ?? ''),
    queryFn: () => getTeams(organizationId ?? '', projectId ?? ''),
    // The pages read the same key as the list; this level reads it as entries with the default mark.
    select: teamLevelItems,
    enabled: enabled && organizationId !== undefined && projectId !== undefined,
    ...LEVEL_QUERY_OPTIONS,
  });
  const teams = organizationId && projectId ? listOf(teamsQuery) : LOADING_LIST;

  // A team-level address (mod-006): its project's teams, once that project is
  // found, so the team it carries is found before anything is set. On the
  // effective project this is the same key, so the same request.
  const addressProjects =
    address.organizationId === organizationId ? projects : listOf(addressProjectsQuery);
  const addressTeamsWanted =
    !syncDone &&
    address.teamId !== undefined &&
    addressProjects.status === 'ready' &&
    addressProjects.items.some((item) => item.id === address.projectId);
  const addressTeamsQuery = useQuery({
    queryKey: organizationsKeys.teams(address.organizationId ?? '', address.projectId ?? ''),
    queryFn: () => getTeams(address.organizationId ?? '', address.projectId ?? ''),
    enabled: enabled && addressTeamsWanted,
    ...LEVEL_QUERY_OPTIONS,
  });

  // 1. The address, once per pathname, once its lists are read.
  if (enabled && !syncDone) {
    const decision = addressSync({
      address,
      chosen,
      organizations,
      addressProjects,
      addressTeams: addressTeamsWanted ? listOf(addressTeamsQuery) : LOADING_LIST,
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
    teamsOf: (orgId, id) => (orgId === organizationId && id === projectId ? teams : LOADING_LIST),
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
  // read, so a reload shows the same context (design § 3.4).
  const { settled } = resolution;
  useEffect(() => {
    if (enabled && writes > 0 && settled) {
      writeRemembered(subject, effective);
    }
  }, [enabled, subject, writes, settled, effective]);

  // The move of a choice that waits for a read. Only the latest choice moves
  // the page, and only from the page it was made on (design § 3.6: only a
  // choice moves pages): a later choice or another page drops it.
  const pendingMove = useRef<object | null>(null);
  useEffect(() => {
    pendingMove.current = null;
  }, [pathname]);

  const select = useCallback(
    (level: WorkContextLevel, id: string) => {
      const next: WorkContextSelection =
        level === 'organization'
          ? { organizationId: id }
          : level === 'project'
            ? { organizationId: effective.organizationId, projectId: id }
            : { ...effective, teamId: id };
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
      // team-level page to the project's first team, its default (design
      // § 3.5; mod-015 BR-18 for mod-006's «Бэклоги» and «Доски»). The page
      // stays while the read is pending; a failed read opens «Главная»
      // (plan-frontend § 4.5).
      const wanted: FirstEntriesWanted = {
        project: level === 'organization' && carried.projectId !== undefined,
        team: level !== 'team' && carried.teamId !== undefined,
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
    [effective, pathname, navigate, queryClient, resolveSwitchTarget],
  );

  const refetchOrganizations = organizationsQuery.refetch;
  const refetchProjects = projectsQuery.refetch;
  const refetchTeams = teamsQuery.refetch;
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
            ? projectItems.map((project) => ({
                id: project.id,
                name: project.name,
                teams:
                  project.id === effectiveProjectId
                    ? teamItems.map((team) => ({
                        id: team.id,
                        name: team.name,
                        isMember: team.isMember,
                        isDefault: team.isDefault,
                      }))
                    : [],
              }))
            : [],
      })),
    [organizationItems, projectItems, teamItems, effectiveOrganizationId, effectiveProjectId],
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
