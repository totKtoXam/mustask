import { Route } from 'react-router-dom';
import { vi } from 'vitest';
import { authValue, renderShell } from '../../test/renderShell';
import type { WorkContextSelection, WorkContextSource } from '../../shared/lib/workContext';
import { BacklogPage } from './pages/BacklogPage';
import { BoardPage } from './pages/BoardPage';
import { BACKLOG_ROUTE, BOARD_ROUTE } from './paths';
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

/** The slice's routes, as `app/routes.tsx` registers them, and a stub for every other page. */
export function boardsRoutes() {
  return (
    <>
      <Route path={BOARD_ROUTE} element={<BoardPage />} />
      <Route path={BACKLOG_ROUTE} element={<BacklogPage />} />
      <Route path="*" element={<p>Другая страница</p>} />
    </>
  );
}

/**
 * Opens `path` inside the real shell (`AppLayout`, its navigation model and
 * page chrome) with a stub context and session, as `app/routes.tsx` mounts the
 * slice.
 */
export function renderBoards(path: string, options: { context?: WorkContextSource } = {}) {
  return renderShell({
    path,
    auth: authValue([]),
    contextSource: options.context ?? teamContext(),
    routes: boardsRoutes(),
  });
}
