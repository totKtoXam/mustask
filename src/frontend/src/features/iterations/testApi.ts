// The iterations module's API double for the slice's tests (mod-007 api-contract § 1–§ 2): a sprint
// row builder and the paths of the team the tests stand in. The handlers are the kit's own
// (`recordRequests`, `serveProblem`, `hold`), the answers are these.
import { HttpResponse, http } from 'msw';
import { ids } from '../organizations/testFixtures';
import { apiUrl } from '../../test/api';
import { server } from '../../test/mocks/server';
import type { Dynamics, DynamicsRow, Figure, SprintRow } from './api/iterationsApi';

export const ORG = ids.alfa;
export const TEAM = ids.platformTeam;
export const SPRINTS_ROUTE = `/api/organizations/${ORG}/teams/${TEAM}/sprints`;
export const LIST_PATH = `/organizations/${ORG}/teams/${TEAM}/sprints`;

/** The id of the `n`-th sprint. */
export const sprintId = (n: number) => `e0000000-0000-4000-8000-${String(n).padStart(12, '0')}`;

/** A Planned sprint numbered `n`, as the list answers it. */
export function sprintRow(n: number, patch: Partial<SprintRow> = {}): SprintRow {
  return {
    id: sprintId(n),
    number: n,
    name: `Спринт ${n}`,
    state: 'Planned',
    startDate: null,
    endDate: null,
    itemCount: 0,
    storyPoints: 0,
    closedItems: null,
    closedStoryPoints: null,
    ...patch,
  };
}

/** Serves the list: highest number first, `open=true` keeps Planned and Active. */
export function serveSprintList(rows: readonly SprintRow[]): void {
  server.use(
    http.get(apiUrl(SPRINTS_ROUTE), ({ request }) => {
      const open = new URL(request.url).searchParams.get('open') === 'true';
      const items = rows
        .filter((row) => !open || row.state !== 'Closed')
        .sort((a, b) => b.number - a.number);
      return HttpResponse.json({ items, nextBefore: null });
    }),
  );
}

const noFigure: Figure = { min: null, average: null, max: null };

/** A dynamics row of sprint `n`. */
export function dynamicsRow(n: number, patch: Partial<DynamicsRow> = {}): DynamicsRow {
  return {
    id: sprintId(n),
    number: n,
    name: `Спринт ${n}`,
    endDate: '2026-03-14',
    items: 10,
    storyPoints: 20,
    closedItems: 6,
    closedItemsPercent: 60,
    closedStoryPoints: 13,
    closedStoryPointsPercent: 65,
    ...patch,
  };
}

/** The dynamics answer with the current row and the others. */
export function dynamicsOf(current: DynamicsRow, sprints: DynamicsRow[] = []): Dynamics {
  return {
    level: 'stories',
    from: '2025-12-01',
    to: '2026-03-31',
    rangeSprints: sprints.length,
    shownSprints: sprints.length,
    truncated: false,
    current,
    sprints,
    summary: {
      closedItems: { min: 4, average: 6.3, max: 9 },
      closedItemsPercent: { min: 40, average: 62.5, max: 90 },
      closedStoryPoints: noFigure,
      closedStoryPointsPercent: noFigure,
    },
  };
}
