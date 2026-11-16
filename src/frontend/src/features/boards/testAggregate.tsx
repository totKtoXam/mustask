// A test kit for feat-048's aggregate views: a hand-served data set that answers the group, row
// and cell reads of api-contract § 2 (cursor = the offset), records every request, and a render
// of the three pages at the organization, department and team address.
import { Route } from 'react-router-dom';
import { HttpResponse, http, type JsonBodyType } from 'msw';
import { server } from '../../test/mocks/server';
import { servedObjectCodes } from '../../test/organizationRoles';
import { authValue, renderShell } from '../../test/renderShell';
import { OrganizationGate } from '../organizations';
import { sampleProcessDefinition } from '../process/testFixtures';
import { processType, API, PROCESS_URL, stateId } from '../workitems/testFixtures';
import type {
  AggregateCard,
  AggregateColumnRead,
  AggregateRow,
  GroupHeader,
} from './api/aggregateApi';
import { AggregateBacklogPage, AggregateBoardPage } from './pages/AggregateViewPages';
import {
  ProjectBacklogRoute,
  ProjectBoardRoute,
  ProjectWorkItemsRoute,
} from './pages/GroupedViewRoutes';
import { AggregateWorkItemsPage } from './pages/AggregateWorkItemsPage';
import { ids } from './testFixtures';
import { teamContext } from './testRender';
import type { WorkContextSource } from '../../shared/lib/workContext';

export const ORG = ids.example;
const ORG_API = `${API}/api/organizations/${ORG}`;

export function orgAddress(view: 'backlogs' | 'boards' | 'workitems', level = 'stories'): string {
  return view === 'workitems'
    ? `/organizations/${ORG}/workitems`
    : `/organizations/${ORG}/${view}/${level}`;
}

export function mskRow(number: number, title = `Элемент ${number}`): AggregateRow {
  const type = processType('US');
  return {
    id: `row-${number}`,
    identifier: `MSK-US-${number}`,
    number,
    type: { id: type.id, key: type.key, name: type.name, appearance: 'story' },
    title,
    assignee: null,
    state: {
      id: stateId('US', 'Новая'),
      name: 'Новая',
      category: { code: 'Proposed', label: 'Предложено' },
    },
    priority: 2,
    team: null,
    project: { id: ids.msk, key: 'MSK', name: 'Мастаск' },
    parent: null,
    changedAt: '2026-10-01T10:00:00Z',
  };
}

export function header(
  kind: GroupHeader['kind'],
  id: string | null,
  name: string | null,
  count: number,
  extra: Partial<GroupHeader> = {},
): GroupHeader {
  return { kind, id, name, count, total: count, ...extra };
}

/** The board's columns of the stories level as the groups read gives them (api-contract § 2.1). */
export function storyColumns(): AggregateColumnRead[] {
  const type = processType('US');
  return [
    {
      key: 'Proposed-1',
      header: 'Новая',
      category: { code: 'Proposed', label: 'Предложено' },
      targets: [{ typeId: type.id, stateId: stateId('US', 'Новая') }],
      count: 2,
    },
    {
      key: 'InProgress-1',
      header: 'Активна',
      category: { code: 'InProgress', label: 'В работе' },
      targets: [{ typeId: type.id, stateId: stateId('US', 'Активна') }],
      count: 0,
    },
  ];
}

export function mskCard(number: number): AggregateCard {
  return { ...mskRow(number), checklist: [] };
}

export interface Write {
  method: string;
  path: string;
  body: unknown;
}

export function serveWrites(): Write[] {
  const writes: Write[] = [];
  server.use(
    http.patch(
      `${ORG_API}/projects/:projectId/workitems/:identifier`,
      async ({ request, params }) => {
        writes.push({
          method: 'PATCH',
          path: new URL(request.url).pathname,
          body: await request.json(),
        });
        const row = mskRow(Number(String(params.identifier).split('-').at(-1)));
        return HttpResponse.json({ ...row, projectId: params.projectId });
      },
    ),
    http.post(`${ORG_API}/projects/:projectId/backlogs/:level/reorder`, async ({ request }) => {
      writes.push({
        method: 'POST',
        path: new URL(request.url).pathname,
        body: await request.json(),
      });
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return writes;
}

export interface SeenRequest {
  kind: 'groups' | 'rows' | 'cells' | 'list';
  params: URLSearchParams;
}

/**
 * Serves the three views from `groups` (per grouping) and `rowsOf(group, offset)`; `fail` may
 * answer a rows read with a response instead.
 */
export function serveAggregate(options: {
  groups: Partial<Record<'none' | 'department' | 'project' | 'team', GroupHeader[]>>;
  rowsOf?(group: string | null, offset: number): AggregateRow[];
  cardsOf?(group: string | null, column: string, offset: number): AggregateCard[];
  fail?(request: SeenRequest, attempt: number): Response | undefined;
  list?: { count: number; data: unknown[] };
}) {
  const seen: SeenRequest[] = [];
  const attempts = new Map<string, number>();
  const answer = (
    kind: SeenRequest['kind'],
    url: string,
    build: (p: URLSearchParams) => JsonBodyType,
  ) => {
    const params = new URL(url).searchParams;
    const request = { kind, params };
    seen.push(request);
    const key = `${kind}?${params}`;
    const attempt = (attempts.get(key) ?? 0) + 1;
    attempts.set(key, attempt);
    return options.fail?.(request, attempt) ?? HttpResponse.json(build(params));
  };
  server.use(
    http.get(`${ORG_API}/:view/:level/groups`, ({ request }) =>
      answer('groups', request.url, (p) => ({
        grouping: p.get('grouping'),
        groups: options.groups[(p.get('grouping') ?? 'none') as 'none'] ?? [],
      })),
    ),
    http.get(`${ORG_API}/workitems/groups`, ({ request }) =>
      answer('groups', request.url, (p) => ({
        grouping: p.get('grouping'),
        groups: options.groups[(p.get('grouping') ?? 'none') as 'none'] ?? [],
      })),
    ),
    http.get(`${ORG_API}/:view/:level/rows`, ({ request }) =>
      answer('rows', request.url, (p) => {
        const offset = Number(p.get('cursor') ?? 0);
        const rows = options.rowsOf?.(p.get('group'), offset) ?? [];
        return { rows, nextCursor: rows.length === 2 ? String(offset + 2) : null };
      }),
    ),
    http.get(`${ORG_API}/workitems/rows`, ({ request }) =>
      answer('rows', request.url, (p) => {
        const offset = Number(p.get('cursor') ?? 0);
        const rows = options.rowsOf?.(p.get('group'), offset) ?? [];
        return { rows, nextCursor: rows.length === 2 ? String(offset + 2) : null };
      }),
    ),
    http.get(`${ORG_API}/boards/:level/cells`, ({ request }) =>
      answer('cells', request.url, (p) => {
        const offset = Number(p.get('cursor') ?? 0);
        const cards = options.cardsOf?.(p.get('group'), p.get('column') ?? '', offset) ?? [];
        // A full page of two has a next one, as the rows read serves it.
        return { cards, nextCursor: cards.length === 2 ? String(offset + 2) : null };
      }),
    ),
    http.get(`${ORG_API}/workitems/assignee-options`, () => HttpResponse.json([])),
    http.get(`${ORG_API}/workitems`, ({ request }) =>
      answer('list', request.url, () => options.list ?? { count: 0, data: [] }),
    ),
    http.get(PROCESS_URL, () => HttpResponse.json(sampleProcessDefinition)),
    // The project rows carry the caller's codes (mod-019 api-contract § 5): the cards are judged by their own.
    http.get(`${ORG_API}/projects`, () =>
      HttpResponse.json(
        [
          { id: ids.msk, name: 'Мастаск', key: 'MSK' },
          // The group headers of these tests name the project 'p-msk' (the real header id is the project's).
          { id: 'p-msk', name: 'Мастаск', key: 'MSK' },
          { id: ids.ops, name: 'Операции', key: 'OPS' },
        ].map((project) => ({ ...project, codes: [...servedObjectCodes.current] })),
      ),
    ),
  );
  return { seen, of: (kind: SeenRequest['kind']) => seen.filter((r) => r.kind === kind) };
}

function aggregateRoutes() {
  return (
    <>
      <Route element={<OrganizationGate />}>
        <Route
          path="/organizations/:organizationId/backlogs/:level"
          element={<AggregateBacklogPage />}
        />
        <Route
          path="/organizations/:organizationId/boards/:level"
          element={<AggregateBoardPage />}
        />
        <Route
          path="/organizations/:organizationId/workitems"
          element={<AggregateWorkItemsPage />}
        />
        <Route
          path="/organizations/:organizationId/areas/:areaId/backlogs/:level"
          element={<AggregateBacklogPage />}
        />
        <Route
          path="/organizations/:organizationId/teams/:teamId/workitems"
          element={<AggregateWorkItemsPage />}
        />
        <Route
          path="/organizations/:organizationId/projects/:projectId/backlogs/:level"
          element={<ProjectBacklogRoute />}
        />
        <Route
          path="/organizations/:organizationId/projects/:projectId/boards/:level"
          element={<ProjectBoardRoute />}
        />
        <Route
          path="/organizations/:organizationId/projects/:projectId/workitems"
          element={<ProjectWorkItemsRoute />}
        />
      </Route>
      <Route path="*" element={<p>Другая страница</p>} />
    </>
  );
}

export function renderAggregate(
  path: string,
  options: { context?: WorkContextSource; role?: 'admin' | 'member' | 'viewer' } = {},
) {
  return renderShell({
    path,
    auth: { ...authValue([]), subject: 'user-1' },
    contextSource: options.context ?? teamContext({ projectId: undefined, teamId: undefined }),
    routes: aggregateRoutes(),
    access: options.role ?? 'admin',
  });
}
