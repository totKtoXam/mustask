// A test kit for feat-048's aggregate views: a hand-served data set that answers the group, row
// and cell reads of api-contract § 2 (cursor = the offset), records every request, and a render
// of the three pages at the organization, department and team address.
import { Route } from 'react-router-dom';
import { HttpResponse, http, type JsonBodyType } from 'msw';
import { server } from '../../test/mocks/server';
import { authValue, renderShell } from '../../test/renderShell';
import { OrganizationGate } from '../organizations';
import { sampleProcessDefinition } from '../process/testFixtures';
import { processType, API, PROCESS_URL, stateId } from '../workitems/testFixtures';
import type { AggregateCard, AggregateRow, GroupHeader } from './api/aggregateApi';
import { AggregateBacklogPage, AggregateBoardPage } from './pages/AggregateViewPages';
import { AggregateWorkItemsPage } from './pages/AggregateWorkItemsPage';
import { ids } from './testFixtures';
import { teamContext } from './testRender';

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
        return { cards, nextCursor: null };
      }),
    ),
    http.get(`${ORG_API}/workitems`, ({ request }) =>
      answer('list', request.url, () => options.list ?? { count: 0, data: [] }),
    ),
    http.get(PROCESS_URL, () => HttpResponse.json(sampleProcessDefinition)),
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
      </Route>
      <Route path="*" element={<p>Другая страница</p>} />
    </>
  );
}

export function renderAggregate(path: string) {
  return renderShell({
    path,
    auth: { ...authValue([]), subject: 'user-1' },
    contextSource: teamContext({ projectId: undefined, teamId: undefined }),
    routes: aggregateRoutes(),
    access: 'admin',
  });
}
