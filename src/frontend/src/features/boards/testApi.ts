// MSW handlers over an in-memory copy of the stories' dataset, answering per
// mod-006's api-contract: not-found from the outside in (§ 0.3), membership by
// type level and resolved area owner (§ 1), placed then unplaced by number
// (§ 1), the reorder with its stale refusal and its places (§ 3.2), the board's
// columns from the seeded process (§ 3.3); plus mod-003's create with `teamId`
// (§ 7) and its state save (§ 6), and mod-001's process read. feat-001's
// api-contract § 4 adds each backlog row's live `parent` and its `children` on
// the same team's backlog one level below, recursively, and the parent write
// (§ 3.2, its checks in their order) and search (§ 3.4) the backlog maps a
// row with. It keeps the contract's answers, not every rule of the backend.
import { HttpResponse, http, type HttpHandler } from 'msw';
import { server } from '../../test/mocks/server';
import type { WorkItemType } from '../process';
import type { BacklogTreeItem } from './api/boardsApi';
import {
  PARENT_CANDIDATES_URL,
  PROCESS_URL,
  WORK_ITEMS_URL,
  WORK_ITEM_PARENT_URL,
  WORK_ITEM_URL,
  problemWithParams,
  process,
} from '../workitems/testFixtures';
import type { WorkItemRecord } from '../workitems/testFixtures';
import {
  BACKLOG_URL,
  BOARD_URL,
  PROBLEM_HEADERS,
  REORDER_URL,
  boardsDataset,
  problem,
  type BoardsDataset,
  type ProblemBody,
  type TeamRecord,
} from './testFixtures';

/** A request the API received, for "nothing was sent" and "this was sent" assertions. */
export interface RecordedRequest {
  method: string;
  /** The path as sent, still encoded, with its query. */
  path: string;
  body: unknown;
}

class Refusal {
  readonly body: ProblemBody;

  constructor(body: ProblemBody) {
    this.body = body;
  }
}

function refuse(code: string): never {
  throw new Refusal(problem(code));
}

/** § 0.2: the level segment → the process level's name, which the seeded types point at. */
const LEVEL_NAMES: Readonly<Record<string, string>> = {
  epics: 'Эпики',
  features: 'Фичи',
  stories: 'Истории',
};

/** feat-001 § 4: the backlog whose rows nest under a row of this one; «Истории» has none (OQ-5). */
const LEVEL_BELOW: Readonly<Record<string, string>> = {
  epics: 'features',
  features: 'stories',
};

/** BR-M4-2: categories in board order; Removed has no column. */
const CATEGORY_ORDER = ['Proposed', 'InProgress', 'Resolved', 'Completed'] as const;
const IDENTIFIER = /^([A-Z][A-Z0-9]{1,9})-([A-Z]+)-([1-9][0-9]*)$/;
const UUID = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
/** feat-001 § 3.4: the search answers at most 20. */
const CANDIDATE_LIMIT = 20;

type Params = Record<string, string | readonly string[] | undefined>;

function one(params: Params, name: string): string | undefined {
  const value = params[name];
  return typeof value === 'string' ? value : undefined;
}

/**
 * The in-memory API. `serveBoards()` installs its handlers; a test reads
 * `requests` and the dataset, changes the dataset as another user would, or
 * puts a one-off answer in front with `server.use` (MSW takes the newest
 * handler first).
 */
export class BoardsTestApi {
  readonly data: BoardsDataset;
  readonly requests: RecordedRequest[] = [];
  private clock = Date.UTC(2026, 9, 6);

  constructor(data: BoardsDataset = boardsDataset()) {
    this.data = data;
  }

  private now(): string {
    this.clock += 60_000;
    return new Date(this.clock).toISOString();
  }

  // ---- lookups, from the outside in (§ 0.3)

  private organization(organizationId: string | undefined) {
    const organization = this.data.organizations.find(
      (candidate) => candidate.active && candidate.id === organizationId?.toLowerCase(),
    );
    if (!organization) refuse('organizations.organization.not_found');
    return organization;
  }

  private project(organizationId: string | undefined, projectId: string | undefined) {
    const organization = this.organization(organizationId);
    const project = this.data.projects.find(
      (candidate) =>
        candidate.active &&
        candidate.organizationId === organization.id &&
        candidate.id === projectId?.toLowerCase(),
    );
    if (!project) refuse('organizations.project.not_found');
    return project;
  }

  private team(projectId: string, teamId: string | undefined): TeamRecord {
    const team = this.data.teams.find(
      (candidate) =>
        candidate.active &&
        candidate.projectId === projectId &&
        candidate.id === teamId?.toLowerCase(),
    );
    if (!team) refuse('organizations.team.not_found');
    return team;
  }

  private levelTypes(level: string | undefined): WorkItemType[] {
    const name = LEVEL_NAMES[level ?? ''];
    if (!name) refuse('boards.level.not_found');
    return process.workItemTypes.filter((type) => type.backlogLevel.name === name);
  }

  private type(typeId: string): WorkItemType {
    const found = process.workItemTypes.find((candidate) => candidate.id === typeId);
    if (!found) throw new Error(`No type ${typeId}.`);
    return found;
  }

  private areaPath(areaId: string): string {
    const names: string[] = [];
    let current = this.data.areas.find((area) => area.id === areaId);
    while (current) {
      names.unshift(current.name);
      const parentId = current.parentId;
      current = parentId ? this.data.areas.find((area) => area.id === parentId) : undefined;
    }
    return names.length <= 1 ? (names[0] ?? '') : names.slice(1).join(' / ');
  }

  /** mod-002's `resolvedOwner`: the explicit owner, else the nearest owned ancestor's, the root's being the first team. */
  private resolvedOwner(areaId: string): string | undefined {
    let current = this.data.areas.find((area) => area.id === areaId);
    while (current) {
      const owner = this.data.owners[current.id];
      if (owner && this.data.teams.some((team) => team.id === owner && team.active)) {
        return owner;
      }
      if (current.parentId === null) {
        const projectId = current.projectId;
        return this.data.teams.find((team) => team.projectId === projectId && team.first)?.id;
      }
      const parentId = current.parentId;
      current = this.data.areas.find((area) => area.id === parentId);
    }
    return undefined;
  }

  /** § 1: the backlog's items in its order — placed by place, then unplaced by number. */
  members(team: TeamRecord, level: string): WorkItemRecord[] {
    const typeIds = new Set(this.levelTypes(level).map((type) => type.id));
    const members = this.data.items.filter((record) => {
      if (record.projectId !== team.projectId || !typeIds.has(record.typeId)) return false;
      const state = this.type(record.typeId).states.find((s) => s.id === record.stateId);
      return state?.category.code !== 'Removed' && this.resolvedOwner(record.areaId) === team.id;
    });
    const place = (record: WorkItemRecord) =>
      this.data.places.find(
        (candidate) =>
          candidate.itemId === record.id &&
          candidate.teamId === team.id &&
          candidate.level === level,
      )?.position;
    const placed = members
      .filter((record) => place(record) !== undefined)
      .sort((first, second) => (place(first) ?? 0) - (place(second) ?? 0));
    const unplaced = members
      .filter((record) => place(record) === undefined)
      .sort((first, second) => first.number - second.number);
    return [...placed, ...unplaced];
  }

  // ---- answers (§ 2)

  private backlogItem(record: WorkItemRecord, order: number) {
    return { order, ...this.itemFields(record) };
  }

  /** The fields a backlog item shares with mod-003's work item (§ 2.3). */
  private itemFields(record: WorkItemRecord) {
    const project = this.data.projects.find((candidate) => candidate.id === record.projectId);
    const type = this.type(record.typeId);
    const state = type.states.find((candidate) => candidate.id === record.stateId);
    const user = this.data.users.find((candidate) => candidate.id === record.assigneeId);
    return {
      id: record.id,
      identifier: `${project?.key}-${type.key}-${record.number}`,
      number: record.number,
      type: { id: type.id, key: type.key, name: type.name, appearance: type.appearance },
      title: record.title,
      assignee:
        record.assigneeId === null
          ? null
          : {
              id: record.assigneeId,
              displayName: user?.displayName ?? null,
              email: user ? user.email : null,
            },
      state: {
        id: state?.id ?? record.stateId,
        name: state?.name ?? '',
        category: state?.category ?? { code: 'Proposed', label: '' },
      },
    };
  }

  /** mod-003 § 1.1: what its create and save answer. */
  private workItem(record: WorkItemRecord) {
    return {
      ...this.itemFields(record),
      projectId: record.projectId,
      description: record.description,
      area: { id: record.areaId, path: this.areaPath(record.areaId) },
      createdBy: 'e2e-test-user',
      createdAt: record.createdAt,
      changedAt: record.changedAt,
    };
  }

  private surface(team: TeamRecord, level: string) {
    const types = this.levelTypes(level);
    return {
      team: {
        id: team.id,
        name: team.name,
        defaultArea: { id: team.defaultAreaId, path: this.areaPath(team.defaultAreaId) },
      },
      level: {
        code: level,
        name: LEVEL_NAMES[level] ?? '',
        types: types.map((type) => {
          const initial = type.states.find((state) => state.isInitial) ?? type.states[0];
          return {
            id: type.id,
            key: type.key,
            name: type.name,
            appearance: type.appearance,
            initialState: {
              id: initial?.id ?? '',
              name: initial?.name ?? '',
              category: initial?.category ?? { code: 'Proposed', label: '' },
            },
          };
        }),
      },
    };
  }

  backlog(team: TeamRecord, level: string) {
    return {
      ...this.surface(team, level),
      items: this.members(team, level).map((record, index) => ({
        order: index + 1,
        ...this.treeItem(team, level, record),
      })),
    };
  }

  // ---- the tree (feat-001 § 4)

  private byId(id: string | null | undefined): WorkItemRecord | undefined {
    return id ? this.data.items.find((candidate) => candidate.id === id) : undefined;
  }

  /** § 0.4: in «Удалена» — its state's category is `Removed`. */
  private removed(record: WorkItemRecord): boolean {
    const state = this.type(record.typeId).states.find(
      (candidate) => candidate.id === record.stateId,
    );
    return state?.category.code === 'Removed';
  }

  /** § 2.1: a parent, a child or a candidate. */
  ref(record: WorkItemRecord) {
    const { id, identifier, type, title, state } = this.itemFields(record);
    return { id, identifier, type, title, state };
  }

  /** A row without `order`: its fields, its live parent, its children one level below. */
  private treeItem(team: TeamRecord, level: string, record: WorkItemRecord): BacklogTreeItem {
    const parent = this.byId(record.parentId);
    const below = LEVEL_BELOW[level];
    return {
      ...this.itemFields(record),
      parent: parent && !this.removed(parent) ? this.ref(parent) : null,
      children: below
        ? this.members(team, below)
            .filter((child) => child.parentId === record.id)
            .map((child) => this.treeItem(team, below, child))
        : [],
    };
  }

  /** § 3.3: BR-M4-2's columns, each with its cards in the backlog's order. */
  board(team: TeamRecord, level: string) {
    const types = this.levelTypes(level);
    const items = this.members(team, level).map((record, index) =>
      this.backlogItem(record, index + 1),
    );
    const columns = [];
    for (const code of CATEGORY_ORDER) {
      const perType = types.map((type) =>
        type.states.filter((state) => state.category.code === code),
      );
      const width = Math.max(0, ...perType.map((states) => states.length));
      for (let k = 1; k <= width; k += 1) {
        const targets = types.map((type, index) => ({
          typeId: type.id,
          stateId: perType[index]?.[k - 1]?.id ?? null,
        }));
        const label = perType.flat()[0]?.category.label ?? '';
        const firstMapped = perType.find((states) => states[k - 1])?.[k - 1];
        columns.push({
          key: `${code}-${k}`,
          header: k === 1 ? label : (firstMapped?.name ?? ''),
          category: { code, label },
          targets,
          items: items.filter((card) =>
            targets.some(
              (target) => target.typeId === card.type.id && target.stateId === card.state.id,
            ),
          ),
        });
      }
    }
    return { ...this.surface(team, level), columns };
  }

  /** § 3.2 steps 3–4. */
  private reorder(team: TeamRecord, level: string, body: Record<string, unknown>) {
    const position = body.position;
    if (position !== 'top' && position !== 'bottom' && position !== 'after') {
      throw new Refusal({
        ...problem('boards.validation_failed'),
        errors: {
          position: [
            {
              code: 'boards.backlog.position_invalid',
              detail: problem('boards.backlog.position_invalid').detail,
            },
          ],
        },
      });
    }
    const order = this.members(team, level);
    const moved = order.find((record) => record.id === body.itemId);
    const anchor =
      position === 'after' ? order.find((record) => record.id === body.afterItemId) : undefined;
    if (!moved || (position === 'after' && !anchor)) refuse('boards.backlog.reorder_stale');
    if (anchor?.id === moved.id) return;

    const rest = order.filter((record) => record.id !== moved.id);
    const at = anchor ? rest.indexOf(anchor) + 1 : position === 'top' ? 0 : rest.length;
    rest.splice(at, 0, moved);
    const memberIds = new Set(rest.map((record) => record.id));
    this.data.places = this.data.places.filter(
      (place) =>
        !memberIds.has(place.itemId) && !(place.teamId === team.id && place.level === level),
    );
    rest.forEach((record, index) =>
      this.data.places.push({ itemId: record.id, teamId: team.id, level, position: index + 1 }),
    );
  }

  private create(projectId: string, body: Record<string, unknown>) {
    const requestId = typeof body.requestId === 'string' ? body.requestId.toLowerCase() : '';
    const title = typeof body.title === 'string' ? body.title.trim() : '';
    if (!UUID.test(requestId) || title.length === 0) {
      throw new Error('The dialog sent an invalid create; mod-003 refuses it before this test.');
    }
    const type = process.workItemTypes.find((candidate) => candidate.id === body.typeId);
    if (!type) refuse('workitems.workitem.type_not_found');
    const replay = this.data.items.find(
      (record) => record.projectId === projectId && record.requestId === requestId,
    );
    if (replay) return { status: 200, record: replay };
    const team =
      typeof body.teamId === 'string' && body.teamId !== ''
        ? this.team(projectId, body.teamId)
        : undefined;
    const root = this.data.areas.find(
      (area) => area.projectId === projectId && area.parentId === null,
    );
    const number =
      Math.max(
        0,
        ...this.data.items
          .filter((record) => record.projectId === projectId)
          .map((record) => record.number),
      ) + 1;
    const now = this.now();
    const record: WorkItemRecord = {
      id: `c3000000-0000-4000-8000-${String(this.data.items.length + 1).padStart(12, '0')}`,
      projectId,
      number,
      typeId: type.id,
      title,
      description: '',
      stateId: (type.states.find((state) => state.isInitial) ?? type.states[0])?.id ?? '',
      assigneeId: null,
      areaId: team?.defaultAreaId ?? root?.id ?? '',
      createdAt: now,
      changedAt: now,
      requestId,
    };
    this.data.items.push(record);
    return { status: 201, record };
  }

  private record(projectId: string, identifier: string | undefined): WorkItemRecord {
    const project = this.data.projects.find((candidate) => candidate.id === projectId);
    const match = IDENTIFIER.exec(identifier ?? '');
    const found =
      match && match[1] === project?.key
        ? this.data.items.find(
            (candidate) =>
              candidate.projectId === projectId && candidate.number === Number(match[3]),
          )
        : undefined;
    if (!found || this.type(found.typeId).key !== match?.[2]) {
      refuse('workitems.workitem.not_found');
    }
    return found;
  }

  private saveState(record: WorkItemRecord, body: Record<string, unknown>) {
    if (typeof body.stateId === 'string') {
      const state = this.type(record.typeId).states.find(
        (candidate) => candidate.id === body.stateId,
      );
      if (!state || state.category.code === 'Removed') {
        refuse('workitems.workitem.state_not_allowed');
      }
      if (record.stateId !== state.id) {
        record.stateId = state.id;
        record.changedAt = this.now();
      }
    }
  }

  // ---- the parent write and its search (feat-001 § 3.2, § 3.4)

  /** The level rank of the record's type (mod-001's `backlogLevels[].rank`). */
  private rank(record: WorkItemRecord): number {
    const levelId = this.type(record.typeId).backlogLevel.id;
    return process.backlogLevels.find((candidate) => candidate.id === levelId)?.rank ?? 0;
  }

  private identifierOf(record: WorkItemRecord): string {
    return this.itemFields(record).identifier;
  }

  /** § 0.3 step 2: `parentId` names an item of this project, or the write is refused. */
  private newParent(projectId: string, parentId: unknown): WorkItemRecord | null {
    if (parentId === null || parentId === undefined) {
      return null;
    }
    const found =
      typeof parentId === 'string' && UUID.test(parentId)
        ? this.data.items.find(
            (candidate) =>
              candidate.projectId === projectId && candidate.id === parentId.toLowerCase(),
          )
        : undefined;
    if (!found) refuse('workitems.link.parent_not_found');
    return found;
  }

  /**
   * § 3.2 in order: the parent's not-found, deleted, the stale view (a view
   * that saw none matches a deleted parent), self, the level, the cycle with
   * its path; then the link, «Изменён» moving on the child and each live
   * parent it adds or removes (BR-H-5).
   */
  private writeParent(projectId: string, record: WorkItemRecord, body: Record<string, unknown>) {
    const parent = this.newParent(projectId, body.parentId);
    if (this.removed(record) || (parent && this.removed(parent))) {
      refuse('workitems.link.item_deleted');
    }
    const stored = this.byId(record.parentId);
    const seen = typeof body.seenParentId === 'string' ? body.seenParentId.toLowerCase() : null;
    const matches =
      stored === undefined
        ? seen === null
        : stored.id === seen || (seen === null && this.removed(stored));
    if (!matches) refuse('workitems.link.parent_changed');
    if (parent) {
      if (parent.id === record.id) refuse('workitems.link.self_parent');
      if (this.rank(parent) < this.rank(record)) {
        refuse('workitems.link.parent_level_not_allowed');
      }
      const chain: WorkItemRecord[] = [];
      for (let current: WorkItemRecord | undefined = parent; current;) {
        chain.push(current);
        if (current.id === record.id) {
          throw new Refusal(
            problemWithParams('workitems.link.cycle', {
              parent: this.identifierOf(parent),
              item: this.identifierOf(record),
              path: chain
                .reverse()
                .map((link) => this.identifierOf(link))
                .join(' → '),
            }),
          );
        }
        current = this.byId(current.parentId);
      }
    }
    const before = record.parentId ?? null;
    const after = parent?.id ?? null;
    if (after !== before) {
      record.parentId = after;
      const now = this.now();
      record.changedAt = now;
      for (const id of [before, after]) {
        const touched = this.byId(id);
        if (touched && !this.removed(touched)) touched.changedAt = now;
      }
    }
    return { parent: parent ? this.ref(parent) : null };
  }

  /** § 3.4: this project's live items of an allowed type — with `levelAbove`, the level directly above. */
  private parentCandidates(record: WorkItemRecord, text: string | null, levelAbove: boolean) {
    const needle = (text ?? '').trim().toLocaleLowerCase('ru');
    const own = this.rank(record);
    const above = Math.min(
      ...process.backlogLevels.map((candidate) => candidate.rank).filter((rank) => rank > own),
    );
    const matched = this.data.items
      .filter(
        (candidate) =>
          candidate.projectId === record.projectId &&
          candidate.id !== record.id &&
          !this.removed(candidate) &&
          (levelAbove ? this.rank(candidate) === above : this.rank(candidate) >= own) &&
          (this.identifierOf(candidate).toLocaleLowerCase('ru').includes(needle) ||
            candidate.title.toLocaleLowerCase('ru').includes(needle)),
      )
      .sort(
        (first, second) =>
          Date.parse(second.changedAt) - Date.parse(first.changedAt) ||
          second.number - first.number,
      );
    return {
      items: matched.slice(0, CANDIDATE_LIMIT).map((candidate) => this.ref(candidate)),
      moreMatched: matched.length > CANDIDATE_LIMIT,
    };
  }

  // ---- MSW

  handlers(): HttpHandler[] {
    const answer =
      (run: (params: Params, body: Record<string, unknown>, url: URL) => unknown) =>
      async ({ request, params }: { request: Request; params: Params }) => {
        const raw = request.method === 'GET' ? '' : await request.text();
        const body = raw ? (JSON.parse(raw) as Record<string, unknown>) : {};
        const url = new URL(request.url);
        this.requests.push({
          method: request.method,
          path: `${url.pathname}${url.search}`,
          body: raw ? body : undefined,
        });
        try {
          const result = run(params, body, url);
          if (result instanceof HttpResponse) {
            return result;
          }
          return result === undefined
            ? new HttpResponse(null, { status: 204 })
            : HttpResponse.json(result as object, { status: 200 });
        } catch (error) {
          if (error instanceof Refusal) {
            return HttpResponse.json(error.body, {
              status: error.body.status,
              headers: PROBLEM_HEADERS,
            });
          }
          throw error;
        }
      };
    const team = (p: Params) => {
      const project = this.project(one(p, 'organizationId'), one(p, 'projectId'));
      return this.team(project.id, one(p, 'teamId'));
    };
    const level = (p: Params) => {
      const segment = one(p, 'level') ?? '';
      this.levelTypes(segment);
      return segment;
    };

    return [
      http.get(
        PROCESS_URL,
        answer((p) => {
          this.organization(one(p, 'organizationId'));
          return process;
        }),
      ),
      http.get(
        BACKLOG_URL,
        answer((p) => {
          const found = team(p);
          return this.backlog(found, level(p));
        }),
      ),
      http.post(
        REORDER_URL,
        answer((p, body) => {
          const found = team(p);
          this.reorder(found, level(p), body);
          return undefined;
        }),
      ),
      http.get(
        BOARD_URL,
        answer((p) => {
          const found = team(p);
          return this.board(found, level(p));
        }),
      ),
      http.post(
        WORK_ITEMS_URL,
        answer((p, body) => {
          const project = this.project(one(p, 'organizationId'), one(p, 'projectId'));
          const { status, record } = this.create(project.id, body);
          return HttpResponse.json(this.workItem(record), { status });
        }),
      ),
      http.patch(
        WORK_ITEM_URL,
        answer((p, body) => {
          const project = this.project(one(p, 'organizationId'), one(p, 'projectId'));
          const record = this.record(project.id, one(p, 'identifier'));
          this.saveState(record, body);
          return this.workItem(record);
        }),
      ),
      http.put(
        WORK_ITEM_PARENT_URL,
        answer((p, body) => {
          const project = this.project(one(p, 'organizationId'), one(p, 'projectId'));
          const record = this.record(project.id, one(p, 'identifier'));
          return this.writeParent(project.id, record, body);
        }),
      ),
      http.get(
        PARENT_CANDIDATES_URL,
        answer((p, _body, url) => {
          const project = this.project(one(p, 'organizationId'), one(p, 'projectId'));
          const record = this.record(project.id, one(p, 'identifier'));
          return this.parentCandidates(
            record,
            url.searchParams.get('text'),
            url.searchParams.get('levelAbove') === 'true',
          );
        }),
      ),
    ];
  }

  // ---- what a test reads or does as another user

  /** The writes received, reads left out. */
  writes(): RecordedRequest[] {
    return this.requests.filter((request) => request.method !== 'GET');
  }

  /** The reads whose path contains `fragment`, e.g. `'/backlogs/stories'`. */
  reads(fragment: string): RecordedRequest[] {
    return this.requests.filter(
      (request) => request.method === 'GET' && request.path.includes(fragment),
    );
  }

  /** The identifiers of a team's backlog of one level, in its order. */
  order(teamId: string, levelCode = 'stories'): string[] {
    const found = this.data.teams.find((candidate) => candidate.id === teamId);
    if (!found) throw new Error(`No team ${teamId}.`);
    return this.backlog(found, levelCode).items.map((row) => row.identifier);
  }

  /** The stored record of a number in a project. */
  item(projectId: string, number: number): WorkItemRecord {
    const found = this.data.items.find(
      (candidate) => candidate.projectId === projectId && candidate.number === number,
    );
    if (!found) throw new Error(`No item ${number} in ${projectId}.`);
    return found;
  }

  /** Another user's reorder (US-2 "Two users", US-7), taken as the server would. */
  reorderAsAnotherUser(teamId: string, levelCode: string, body: Record<string, unknown>): void {
    const found = this.data.teams.find((candidate) => candidate.id === teamId);
    if (!found) throw new Error(`No team ${teamId}.`);
    this.reorder(found, levelCode, body);
  }
}

/** Serves the dataset (US-1's by default) for the rest of the test. */
export function serveBoards(data?: BoardsDataset): BoardsTestApi {
  const api = new BoardsTestApi(data);
  server.use(...api.handlers());
  return api;
}

/** Answers requests to `url` (an MSW pattern) with the problem body, once or until the test ends. */
export function serveProblem(
  method: 'get' | 'post' | 'patch',
  url: string,
  body: ProblemBody,
  options: { once?: boolean } = {},
): void {
  server.use(
    http[method](
      url,
      () => HttpResponse.json(body, { status: body.status, headers: PROBLEM_HEADERS }),
      { once: options.once ?? false },
    ),
  );
}
