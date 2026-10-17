import { Route } from 'react-router-dom';
import { vi } from 'vitest';
import type { ServedAccess, TestRole } from '../../test/organizationRoles';
import { authValue, renderShell } from '../../test/renderShell';
import type { WorkContextSelection, WorkContextSource } from '../../shared/lib/workContext';
import { OrganizationGate } from '../organizations';
import { BacklogPage } from './pages/BacklogPage';
import { BoardPage } from './pages/BoardPage';
import { ProjectBacklogPage } from './pages/ProjectBacklogPage';
import { ProjectBoardPage } from './pages/ProjectBoardPage';
import { BACKLOG_ROUTE, BOARD_ROUTE, PROJECT_BACKLOG_ROUTE, PROJECT_BOARD_ROUTE } from './paths';
import { ids } from './testFixtures';

/** A team's address prefix in «Мастаск», «Платформа» by default. */
export function teamPrefix(teamId: string = ids.platformTeam): string {
  return `/organizations/${ids.example}/projects/${ids.msk}/teams/${teamId}`;
}

/** A team's backlog of one level, «Истории» of «Платформа» by default. */
export function backlogAddress(level = 'stories', teamId: string = ids.platformTeam): string {
  return `${teamPrefix(teamId)}/backlogs/${level}`;
}

/** A team's board of one level, «Истории» of «Платформа» by default. */
export function boardAddress(level = 'stories', teamId: string = ids.platformTeam): string {
  return `${teamPrefix(teamId)}/boards/${level}`;
}

/** The project's address prefix in «Мастаск» (feat-039): no team in it. */
export function projectPrefix(): string {
  return `/organizations/${ids.example}/projects/${ids.msk}`;
}

/** The project's backlog of one level (feat-039 BR-PB-1), «Истории» by default. */
export function projectBacklogAddress(level = 'stories'): string {
  return `${projectPrefix()}/backlogs/${level}`;
}

/** The project's board of one level (feat-039 BR-PB-1), «Истории» by default. */
export function projectBoardAddress(level = 'stories'): string {
  return `${projectPrefix()}/boards/${level}`;
}

/** An item's form in «Мастаск», where a title links. */
export function mskForm(identifier: string): string {
  return `/organizations/${ids.example}/projects/${ids.msk}/workitems/${identifier}`;
}

/**
 * A real context with «Пример организации» / «Мастаск» / «Платформа»
 * chosen: the shell shows the team-scoped entries.
 */
export function teamContext(selection: WorkContextSelection = {}): WorkContextSource {
  return {
    status: 'ready',
    organizations: [
      {
        id: ids.example,
        name: 'Пример организации',
        projects: [
          {
            id: ids.msk,
            name: 'Мастаск',
            teams: [
              { id: ids.mskTeam, name: 'Мастаск' },
              { id: ids.platformTeam, name: 'Платформа' },
              { id: ids.analyticsTeam, name: 'Аналитика' },
            ],
          },
        ],
      },
    ],
    selection: {
      organizationId: ids.example,
      projectId: ids.msk,
      teamId: ids.platformTeam,
      ...selection,
    },
    isReal: true,
    select: vi.fn(),
  };
}

/**
 * The slice's routes, as `app/routes.tsx` registers them, and a stub for every
 * other page. They sit under the organization gate, as there (mod-016): a page
 * asks `useAccessCheck()` for the organization in its address, and the shell
 * primes that organization's codes by the `role` of `renderBoards`. The pages
 * are mounted without `OrganizationPage`, so no test meets the gate's first rows.
 */
export function boardsRoutes() {
  return (
    <>
      <Route element={<OrganizationGate />}>
        <Route path={BOARD_ROUTE} element={<BoardPage />} />
        <Route path={BACKLOG_ROUTE} element={<BacklogPage />} />
        <Route path={PROJECT_BOARD_ROUTE} element={<ProjectBoardPage />} />
        <Route path={PROJECT_BACKLOG_ROUTE} element={<ProjectBacklogPage />} />
      </Route>
      <Route path="*" element={<p>Другая страница</p>} />
    </>
  );
}

/**
 * Opens `path` inside the real shell (`AppLayout`, its navigation model and
 * page chrome) with a stub context and session, as `app/routes.tsx` mounts the
 * slice.
 */
export function renderBoards(
  path: string,
  options: {
    context?: WorkContextSource;
    /**
     * The caller's role in the selected organization (mod-016): the administrator
     * by default; a `ServedAccess` is a bundle of exactly its codes.
     */
    role?: TestRole | ServedAccess;
  } = {},
) {
  return renderShell({
    path,
    auth: authValue([]),
    contextSource: options.context ?? teamContext(),
    routes: boardsRoutes(),
    access: options.role ?? 'admin',
  });
}
