// MSW handlers over an in-memory copy of the stories' dataset, answering per
// mod-006's api-contract: not-found from the outside in (§ 0.3), membership by
// type level and owning team (mod-017 api-contract § 2.5), placed then unplaced by
// number (§ 1), the reorder with its stale refusal and its places (§ 3.2), the
// board's columns from the seeded process (§ 3.3); plus mod-003's create with
// `team` (mod-017 § 2.2) and its state and team save (§ 6), the project's teams
// read and the team filter of the project's views (mod-017 § 2.6, § 3), and
// mod-001's process read. feat-001's
// api-contract § 4 adds each backlog row's live `parent`, and the parent write
// (§ 3.2, its checks in their order) and search (§ 3.4) the backlog maps a
// row with. feat-022's api-contract § 5 adds each row's and each card's
// `priority`, and § 2 the fields per type to mod-003's answer. feat-027's
// api-contract § 1–§ 2 makes each backlog answer the whole tree at full depth
// over one tree set — the page's level and every level below it, «Задачи»
// included, a same-level child only beneath its parent — with `order` on the
// top-level rows only and `effort`, `businessValue`, `valueArea` on every row;
// § 3 adds the create's optional `parentId`. It keeps the contract's answers,
// not every rule of the backend.
import { HttpResponse, http, type HttpHandler } from 'msw';
import { server } from '../../test/mocks/server';
import type { WorkItemType } from '../process';
import type { BacklogTreeItem } from './api/boardsApi';
import {
  AREAS_URL,
  PARENT_CANDIDATES_URL,
  PROCESS_URL,
  TEAMS_URL,
  WORK_ITEMS_URL,
  WORK_ITEM_PARENT_URL,
  WORK_ITEM_URL,
  defaultFields,
  linkedProjectIds,
  problemWithParams,
  process,
} from '../workitems/testFixtures';
import type { WorkItemRecord } from '../workitems/testFixtures';
import {
  BACKLOG_URL,
  BOARD_URL,
  PROBLEM_HEADERS,
  PROJECT_BACKLOG_URL,
  ORG_PROJECTS_URL,
  PROJECT_BOARD_URL,
  PROJECT_REORDER_URL,
  REORDER_URL,
  TEAM_PROJECTS_URL,
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

/**
 * What a backlog or a board is of: a team's (`team`, over every project it works on since feat-040;
 * `projectId: null`), or the whole project's (feat-039, `team: null`) — the same reads over another
 * membership and order.
 */
interface Scope {
  /** The project's views: the project. A team's views have none (feat-040 BR-TS-1). */
  projectId: string | null;
  team: TeamRecord | null;
  /** The project's views only (mod-017 § 3): the chosen team ids and `none`; absent or empty is no filter. */
  teamIds?: readonly string[];
  /** A team's views only (feat-040 BR-TS-8): the chosen project ids; absent or empty is no filter. */
  projectIds?: readonly string[];
}

function teamOf(team: TeamRecord): Scope {
  return { projectId: null, team };
}

/** § 0.2: the level segment → the process level's name, which the seeded types point at. */
const LEVEL_NAMES: Readonly<Record<string, string>> = {
  epics: 'Эпики',
  features: 'Фичи',
  stories: 'Истории',
};

/** BR-M4-2: categories in board order; Removed has no column. */
const CATEGORY_ORDER = ['Proposed', 'InProgress', 'Resolved', 'Completed'] as const;
const IDENTIFIER = /^([A-Z][A-Z0-9]{1,9})-([A-Z]+)-([1-9][0-9]*)$/;
const UUID = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
/** feat-032 api-contract § 1: the appearances a checklist holds. */
const CHECKLIST_APPEARANCES: ReadonlySet<string> = new Set(['task', 'bug', 'testcase']);
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
  /** The paths (by their ending) whose next POST is carried out, but whose answer is lost. */
  private readonly lostAnswers: string[] = [];

  constructor(data: BoardsDataset = boardsDataset()) {
    this.data = data;
  }

  /**
   * A lost answer, as a dropped connection makes it: the next POST to a path ending
   * in `suffix` is recorded and carried out — the item exists — and the page gets a
   * network error. A resend is then the case the `requestId` exists for.
   */
  loseNextAnswer(suffix: string): void {
    this.lostAnswers.push(suffix);
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

  /** feat-040 § 0.3: a team of the organization, found by (organization, id); a deleted or foreign one is not. */
  private team(organizationId: string | undefined, teamId: string | undefined): TeamRecord {
    const organization = this.organization(organizationId);
    const team = this.data.teams.find(
      (candidate) =>
        candidate.active &&
        candidate.id === teamId?.toLowerCase() &&
        this.data.projects.find((project) => project.id === candidate.projectId)?.organizationId ===
          organization.id,
    );
    if (!team) refuse('organizations.team.not_found');
    return team;
  }

  /** The projects a team is linked to that are active (feat-040 api-contract § 2.2), by name. */
  private activeProjectsOf(team: TeamRecord) {
    return linkedProjectIds(team)
      .flatMap((id) => {
        const project = this.data.projects.find((candidate) => candidate.id === id);
        return project?.active ? [project] : [];
      })
      .sort((first, second) =>
        first.name.localeCompare(second.name, 'ru', { sensitivity: 'base' }),
      );
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

  /** api-contract § 3: any chosen value matches; a value naming no team of the project selects nothing. */
  private inFilter(record: WorkItemRecord, scope: Scope): boolean {
    const chosen = scope.teamIds ?? [];
    if (chosen.length === 0) return true;
    const known = new Set(
      this.data.teams
        .filter((t) => t.active && linkedProjectIds(t).includes(scope.projectId ?? ''))
        .map((t) => t.id),
    );
    return chosen.some((value) =>
      value === 'none'
        ? !record.teamId || !known.has(record.teamId)
        : known.has(value.toLowerCase()) && record.teamId === value.toLowerCase(),
    );
  }

  /**
   * feat-040 api-contract § 3.1: any chosen project matches; a value naming no project the team
   * works on (unknown, malformed, another organization's, unlinked or deactivated) selects nothing,
   * and beside a valid value adds nothing.
   */
  private inProjectFilter(record: WorkItemRecord, scope: Scope): boolean {
    const chosen = scope.projectIds ?? [];
    if (chosen.length === 0 || scope.team === null) return true;
    const known = new Set(this.activeProjectsOf(scope.team).map((project) => project.id));
    return chosen.some(
      (value) => known.has(value.toLowerCase()) && record.projectId === value.toLowerCase(),
    );
  }

  /** The items a scope can hold at all: the project's, or — a team's — those of the team's active projects. */
  private inScope(record: WorkItemRecord, scope: Scope): boolean {
    if (scope.team === null) {
      return record.projectId === scope.projectId;
    }
    return (
      record.teamId === scope.team.id &&
      this.activeProjectsOf(scope.team).some((project) => project.id === record.projectId) &&
      this.inProjectFilter(record, scope)
    );
  }

  private teamRef(record: WorkItemRecord): { id: string; name: string } | null {
    const team = record.teamId
      ? this.data.teams.find((candidate) => candidate.id === record.teamId && candidate.active)
      : undefined;
    return team ? { id: team.id, name: team.name } : null;
  }

  /**
   * § 1: the backlog's items in its order — placed by place, then unplaced by number. The
   * project's (feat-039 api-contract § 1): every item of the project at the level outside
   * Removed, whatever its area or team; placed by the project's places, then unplaced by
   * creation time, then number.
   */
  members(scope: Scope, level: string): WorkItemRecord[] {
    const { team } = scope;
    const typeIds = new Set(this.levelTypes(level).map((type) => type.id));
    const members = this.data.items.filter((record) => {
      if (!this.inScope(record, scope) || !typeIds.has(record.typeId)) return false;
      const state = this.type(record.typeId).states.find((s) => s.id === record.stateId);
      return state?.category.code !== 'Removed' && (team !== null || this.inFilter(record, scope));
    });
    const place = (record: WorkItemRecord) =>
      team === null
        ? this.data.projectPlaces.find(
            (candidate) => candidate.itemId === record.id && candidate.level === level,
          )?.position
        : this.data.places.find(
            (candidate) =>
              candidate.itemId === record.id &&
              candidate.teamId === team.id &&
              candidate.level === level,
          )?.position;
    // feat-040 BR-TS-6: creation time, then the project's key by character code, then the number
    // as a number — the tie of unplaced items and of placed items of equal position, on both kinds
    // of view (a project's items share a key, so it decides nothing there).
    const key = (record: WorkItemRecord) =>
      this.data.projects.find((project) => project.id === record.projectId)?.key ?? '';
    const tie = (first: WorkItemRecord, second: WorkItemRecord) =>
      Date.parse(first.createdAt) - Date.parse(second.createdAt) ||
      (key(first) < key(second) ? -1 : key(first) > key(second) ? 1 : 0) ||
      first.number - second.number;
    const placed = members
      .filter((record) => place(record) !== undefined)
      .sort((first, second) => (place(first) ?? 0) - (place(second) ?? 0) || tie(first, second));
    const unplaced = members.filter((record) => place(record) === undefined).sort(tie);
    return [...placed, ...unplaced];
  }

  /** mod-017 api-contract § 1.6: the area tree in pre-order, which the creation dialog reads for the root's path. */
  private areaTree(projectId: string) {
    const nodes: {
      id: string;
      type: 'project' | 'area';
      parentId: string | null;
      name: string;
      path: string;
      depth: number;
    }[] = [];
    const visit = (parentId: string | null, depth: number) => {
      const children = this.data.areas
        .filter((area) => area.projectId === projectId && area.parentId === parentId)
        .sort((first, second) => first.name.localeCompare(second.name, 'ru'));
      for (const area of children) {
        nodes.push({
          id: area.id,
          type: area.parentId === null ? 'project' : 'area',
          parentId: area.parentId,
          name: area.name,
          path: this.areaPath(area.id),
          depth,
        });
        visit(area.id, depth + 1);
      }
    };
    visit(null, 0);
    return nodes;
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
              // mod-016 api-contract § 3.3: the membership alone.
              isMember: user !== undefined && user.member !== false,
            },
      state: {
        id: state?.id ?? record.stateId,
        name: state?.name ?? '',
        category: state?.category ?? { code: 'Proposed', label: '' },
      },
      // feat-022 § 5: «Приоритет», after the state.
      priority: record.fields.priority,
      // mod-017 § 1.5: the owning team as it stands now, `null` for «Без команды».
      team: this.teamRef(record),
      // feat-040 api-contract § 2.1: the item's project as it stands now.
      project: { id: record.projectId, name: project?.name ?? '' },
    };
  }

  /** mod-003 § 1.1 with feat-022 § 2: what its create and save answer. */
  private workItem(record: WorkItemRecord) {
    return {
      ...this.itemFields(record),
      ...record.fields,
      projectId: record.projectId,
      description: record.description,
      area: { id: record.areaId, path: this.areaPath(record.areaId) },
      createdBy: 'e2e-test-user',
      createdAt: record.createdAt,
      changedAt: record.changedAt,
    };
  }

  /** The item with its parent and children, as a read or a save of the card answers (feat-029). */
  private fullItem(record: WorkItemRecord) {
    const parent = this.byId(record.parentId);
    // mod-005 § 1.1: every answer of a `WorkItem` carries `deletion`, `null` for a live item.
    return {
      ...this.workItem(record),
      parent: parent ? this.ref(parent) : null,
      children: [],
      deletion: null,
    };
  }

  /** The answer's subject — `team`, or on the project's views (feat-039 § 2.1) `project` — and its level. */
  private surface(scope: Scope, level: string) {
    const types = this.levelTypes(level);
    const { team } = scope;
    const project = this.data.projects.find((candidate) => candidate.id === scope.projectId);
    return {
      ...(team === null
        ? { project: { id: scope.projectId ?? '', name: project?.name ?? '' } }
        : {
            team: { id: team.id, name: team.name },
            // feat-040 api-contract § 2.2: the team's linked active projects by name, whatever the filter.
            projects: this.activeProjectsOf(team).map(({ id, name }) => ({ id, name })),
          }),
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

  /** feat-027 § 2: the top-level rows, «Порядок» 1..n over them, the whole tree beneath each. */
  backlog(scope: Scope, level: string) {
    const pageRank = this.rankOfLevel(level);
    const tree = this.treeSet(scope, pageRank);
    const nested = this.nestedUnder(scope, tree);
    const topLevel = this.members(scope, level).filter((record) => !nested.parentOf.has(record.id));
    return {
      ...this.surface(scope, level),
      items: topLevel.map((record, index) => ({
        order: index + 1,
        ...this.treeItem(nested.childrenOf, record, new Set()),
      })),
    };
  }

  // ---- the tree (feat-027 § 1; feat-001 § 4 for `parent`)

  /** The rank of a page's level (mod-001's `backlogLevels[].rank`, a higher rank is a higher level). */
  private rankOfLevel(level: string): number {
    const name = LEVEL_NAMES[level];
    return process.backlogLevels.find((candidate) => candidate.name === name)?.rank ?? 0;
  }

  /**
   * § 1's tree set: the project's live items of the page's level or any level
   * ranked below it that the team owns — the «Задачи» level included, which has
   * no route of its own. The project's has every item, narrowed by the team
   * filter alone (mod-017 api-contract § 2.6).
   */
  private treeSet(scope: Scope, pageRank: number): WorkItemRecord[] {
    const { team } = scope;
    return this.data.items.filter(
      (record) =>
        this.inScope(record, scope) &&
        this.rank(record) <= pageRank &&
        !this.removed(record) &&
        (team !== null || this.inFilter(record, scope)),
    );
  }

  /**
   * One level's backlog order on the team (§ 1): the three levels with a route
   * as `members` gives it; «Задачи», which has no places before feat-023, by number.
   */
  private levelOrder(scope: Scope, rank: number): WorkItemRecord[] {
    const name = process.backlogLevels.find((candidate) => candidate.rank === rank)?.name;
    const code = Object.keys(LEVEL_NAMES).find((candidate) => LEVEL_NAMES[candidate] === name);
    if (code) {
      return this.members(scope, code);
    }
    return this.treeSet(scope, rank)
      .filter((record) => this.rank(record) === rank)
      .sort((first, second) => first.number - second.number);
  }

  /**
   * § 1's nesting over the tree set: an item stands beneath its stored parent
   * exactly when that parent is in the set and ranks the same or higher
   * (BR-H-2). The children of a row stand by level rank, highest first, then
   * in their level's backlog order (BR-T-6).
   */
  private nestedUnder(scope: Scope, set: readonly WorkItemRecord[]) {
    const inSet = new Set(set.map((record) => record.id));
    const position = new Map<string, number>();
    for (const rank of new Set(set.map((record) => this.rank(record)))) {
      this.levelOrder(scope, rank).forEach((record, index) => position.set(record.id, index));
    }
    const parentOf = new Map<string, WorkItemRecord>();
    const childrenOf = new Map<string, WorkItemRecord[]>();
    for (const record of set) {
      const parent = this.byId(record.parentId);
      if (parent && inSet.has(parent.id) && this.rank(parent) >= this.rank(record)) {
        parentOf.set(record.id, parent);
        childrenOf.set(parent.id, [...(childrenOf.get(parent.id) ?? []), record]);
      }
    }
    for (const children of childrenOf.values()) {
      children.sort(
        (first, second) =>
          this.rank(second) - this.rank(first) ||
          (position.get(first.id) ?? 0) - (position.get(second.id) ?? 0),
      );
    }
    return { parentOf, childrenOf };
  }

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

  /**
   * A row without `order` (§ 2.2): its fields, the three planning values of
   * § 2.1, its live parent and its children, recursively — a visited set keeps a
   * stored cycle, which no write makes, from looping.
   */
  private treeItem(
    childrenOf: ReadonlyMap<string, readonly WorkItemRecord[]>,
    record: WorkItemRecord,
    visited: ReadonlySet<string>,
  ): BacklogTreeItem {
    const parent = this.byId(record.parentId);
    const path = new Set(visited).add(record.id);
    return {
      ...this.itemFields(record),
      effort: record.fields.effort,
      businessValue: record.fields.businessValue,
      valueArea: record.fields.valueArea,
      parent: parent && !this.removed(parent) ? this.ref(parent) : null,
      children: (childrenOf.get(record.id) ?? [])
        .filter((child) => !path.has(child.id))
        .map((child) => this.treeItem(childrenOf, child, path)),
    };
  }

  /**
   * feat-032 api-contract § 1: a card's `checklist` — the direct children in its project outside the
   * Removed category whose type has the appearance task, bug or testcase, whatever their area, by
   * number; a child carries no `order` and no `priority`.
   */
  private checklist(parent: WorkItemRecord) {
    return this.data.items
      .filter(
        (record) =>
          record.parentId === parent.id &&
          record.projectId === parent.projectId &&
          !this.removed(record) &&
          CHECKLIST_APPEARANCES.has(this.type(record.typeId).appearance),
      )
      .sort((first, second) => first.number - second.number)
      .map((record) => {
        const { id, identifier, number, type, title, assignee, state } = this.itemFields(record);
        return { id, identifier, number, type, title, assignee, state };
      });
  }

  /**
   * § 3.3: BR-M4-2's columns, each with its cards in the backlog's order. A column is headed by the
   * name of the state it maps for the first type, in display order, that maps one (bug-004 D-3) — the
   * category's label is no header, it stays in the column's `category`.
   */
  board(scope: Scope, level: string) {
    const types = this.levelTypes(level);
    const items = this.members(scope, level).map((record, index) => ({
      ...this.backlogItem(record, index + 1),
      checklist: this.checklist(record),
    }));
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
          header: firstMapped?.name ?? '',
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
    return { ...this.surface(scope, level), columns };
  }

  /** § 3.2 steps 3–4; the project's (feat-039 § 3.2) writes the project's places only. */
  private reorder(scope: Scope, level: string, body: Record<string, unknown>) {
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
    const order = this.members(scope, level);
    const moved = order.find((record) => record.id === body.itemId);
    const anchor =
      position === 'after' ? order.find((record) => record.id === body.afterItemId) : undefined;
    if (!moved || (position === 'after' && !anchor)) refuse('boards.backlog.reorder_stale');
    if (anchor?.id === moved.id) return;

    const rest = order.filter((record) => record.id !== moved.id);
    const at = anchor ? rest.indexOf(anchor) + 1 : position === 'top' ? 0 : rest.length;
    rest.splice(at, 0, moved);
    const memberIds = new Set(rest.map((record) => record.id));
    const { team } = scope;
    if (team === null) {
      // The project's places: an item has one, and a team's are neither read nor written.
      const inProject = (itemId: string) => this.byId(itemId)?.projectId === scope.projectId;
      this.data.projectPlaces = this.data.projectPlaces.filter(
        (place) =>
          !memberIds.has(place.itemId) && !(place.level === level && inProject(place.itemId)),
      );
      rest.forEach((record, index) =>
        this.data.projectPlaces.push({ itemId: record.id, level, position: index + 1 }),
      );
      return;
    }
    // feat-040 BR-TS-7: every item of the backlog gets a place; the places of a deactivated
    // project's items (not members now) are left as they are.
    const inActiveProject = (itemId: string) =>
      this.data.projects.find((project) => project.id === this.byId(itemId)?.projectId)?.active ===
      true;
    this.data.places = this.data.places.filter(
      (place) =>
        !memberIds.has(place.itemId) &&
        !(place.teamId === team.id && place.level === level && inActiveProject(place.itemId)),
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
    // mod-017 § 0.4: the replay, then the team (not read beside a parent), then the parent's
    // not-found (`parentId` absent or null is no parent), «Удалена», then the level.
    const hasParent = body.parentId !== null && body.parentId !== undefined;
    const named = hasParent || !('team' in body) ? undefined : this.teamNamed(projectId, body.team);
    const parent = this.newParent(projectId, body.parentId);
    if (parent && this.removed(parent)) refuse('workitems.link.item_deleted');
    if (parent && this.rank(parent) < this.rankOfType(type.id)) {
      refuse('workitems.link.parent_level_not_allowed');
    }
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
      // feat-027 § 3: the parent's area as stored now, else the root (mod-017 BR-OT-10).
      areaId: parent?.areaId ?? root?.id ?? '',
      // BR-OT-3: a child the parent's team, else the project's default; no parent, the team sent,
      // else the default.
      teamId: hasParent
        ? (parent?.teamId ?? this.defaultTeamId(projectId))
        : 'team' in body
          ? (named?.id ?? null)
          : this.defaultTeamId(projectId),
      createdAt: now,
      changedAt: now,
      requestId,
      parentId: parent?.id ?? null,
      fields: defaultFields(type.appearance),
    };
    this.data.items.push(record);
    if (parent) {
      // BR-H-5: the parent's «Изменён» moves to the creation time.
      parent.changedAt = now;
    }
    return { status: 201, record };
  }

  /** mod-017 api-contract § 0.2: the team a write names; a wrapper `{ teamId }`, `null` meaning none. */
  private teamNamed(projectId: string, wrapper: unknown): TeamRecord | null {
    const teamId = (wrapper as { teamId?: unknown } | null | undefined)?.teamId;
    if (teamId === null || teamId === undefined) return null;
    const found =
      typeof teamId === 'string'
        ? this.data.teams.find((candidate) => candidate.id === teamId.toLowerCase())
        : undefined;
    if (!found || !found.active) refuse('organizations.team.not_found');
    if (!linkedProjectIds(found).includes(projectId)) refuse('organizations.team.not_in_project');
    return found;
  }

  /** BR-OT-4: the team the project's settings name, else its only active team, else none. */
  private defaultTeamId(projectId: string): string | null {
    const active = this.data.teams.filter(
      (team) => linkedProjectIds(team).includes(projectId) && team.active,
    );
    const named = this.data.projects.find((p) => p.id === projectId)?.defaultTeamId;
    return (
      active.find((team) => team.id === named)?.id ??
      (active.length === 1 ? (active[0]?.id ?? null) : null)
    );
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
    // mod-017 § 2.4: a changed team drops the item's team places (BR-OT-6) and moves «Изменён».
    if (body.team !== undefined) {
      const next = this.teamNamed(record.projectId, body.team);
      if ((next?.id ?? null) !== (record.teamId ?? null)) {
        this.data.places = this.data.places.filter((place) => place.itemId !== record.id);
        record.teamId = next?.id ?? null;
        record.changedAt = this.now();
      }
    }
    // feat-029: the card saves a title too, and the backlog behind it must show it.
    if (typeof body.title === 'string' && record.title !== body.title) {
      record.title = body.title;
      record.changedAt = this.now();
    }
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
    return this.rankOfType(record.typeId);
  }

  private rankOfType(typeId: string): number {
    const levelId = this.type(typeId).backlogLevel.id;
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

  handlers(options: { projectList?: boolean } = {}): HttpHandler[] {
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
          const lost =
            request.method === 'POST'
              ? this.lostAnswers.findIndex((suffix) => url.pathname.endsWith(suffix))
              : -1;
          if (lost >= 0) {
            // The request was carried out and recorded; the answer never arrives.
            this.lostAnswers.splice(lost, 1);
            return HttpResponse.error();
          }
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
    /** feat-040 api-contract § 0.3: the organization, then the team; there is no project level, and the filter is `projectId`. */
    const team = (p: Params, url?: URL): Scope => ({
      ...teamOf(this.team(one(p, 'organizationId'), one(p, 'teamId'))),
      projectIds: url?.searchParams.getAll('projectId'),
    });
    /** feat-039 § 0.4: the organization, then the project; there is no team level. */
    const wholeProject = (p: Params, url?: URL): Scope => {
      const project = this.project(one(p, 'organizationId'), one(p, 'projectId'));
      return { projectId: project.id, team: null, teamIds: url?.searchParams.getAll('teamId') };
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
        answer((p, _body, url) => {
          const found = team(p, url);
          return this.backlog(found, level(p));
        }),
      ),
      http.post(
        REORDER_URL,
        answer((p, body) => {
          // A reorder runs against the full list: the filter is never sent (api-contract § 3.2).
          const found = team(p);
          this.reorder(found, level(p), body);
          return undefined;
        }),
      ),
      http.get(
        BOARD_URL,
        answer((p, _body, url) => {
          const found = team(p, url);
          return this.board(found, level(p));
        }),
      ),
      // feat-028 api-contract § 2.2: a team's linked active projects, by name, which the dialog's «Проект» reads.
      http.get(
        TEAM_PROJECTS_URL,
        answer((p) => {
          const found = this.team(one(p, 'organizationId'), one(p, 'teamId'));
          return this.activeProjectsOf(found).map(({ id, name, key }) => ({ id, name, key }));
        }),
      ),
      // mod-002 api-contract § 2.1: the organization's active projects with their keys, by name —
      // the list a card opened by address on a team's view finds its project in (feat-040 BR-TS-4).
      // A test that serves the organizations API as well leaves it to that one (`projectList: false`).
      ...(options.projectList === false
        ? []
        : [
            http.get(
              ORG_PROJECTS_URL,
              answer((p) => {
                const organization = this.organization(one(p, 'organizationId'));
                return this.data.projects
                  .filter((project) => project.active && project.organizationId === organization.id)
                  .sort((a, b) => a.name.localeCompare(b.name, 'ru', { sensitivity: 'base' }))
                  .map(({ id, name, key }) => ({ id, name, key }));
              }),
            ),
          ]),
      // feat-039 api-contract § 3: the project's own backlog, reorder and board.
      http.get(
        PROJECT_BACKLOG_URL,
        answer((p, _body, url) => {
          const found = wholeProject(p, url);
          return this.backlog(found, level(p));
        }),
      ),
      http.post(
        PROJECT_REORDER_URL,
        answer((p, body) => {
          const found = wholeProject(p);
          this.reorder(found, level(p), body);
          return undefined;
        }),
      ),
      http.get(
        PROJECT_BOARD_URL,
        answer((p, _body, url) => {
          const found = wholeProject(p, url);
          return this.board(found, level(p));
        }),
      ),
      http.get(
        AREAS_URL,
        answer((p) => {
          const project = this.project(one(p, 'organizationId'), one(p, 'projectId'));
          return this.areaTree(project.id);
        }),
      ),
      // mod-017 api-contract § 2.7: the project's teams, which the filter and the dialog read.
      http.get(
        TEAMS_URL,
        answer((p) => {
          const project = this.project(one(p, 'organizationId'), one(p, 'projectId'));
          const active = this.data.teams
            .filter((team) => linkedProjectIds(team).includes(project.id) && team.active)
            .sort((a, b) => a.name.localeCompare(b.name, 'ru', { sensitivity: 'base' }));
          const named = active.find((team) => team.id === project.defaultTeamId);
          return active.map((team) => ({
            id: team.id,
            projectId: project.id,
            name: team.name,
            defaultMark: named
              ? named.id === team.id
                ? 'named'
                : null
              : active.length === 1
                ? 'onlyTeam'
                : null,
            isMember: team.member === true,
          }));
        }),
      ),
      http.post(
        WORK_ITEMS_URL,
        answer((p, body) => {
          const project = this.project(one(p, 'organizationId'), one(p, 'projectId'));
          const { status, record } = this.create(project.id, body);
          const parent = this.byId(record.parentId);
          // feat-027 § 3: 201 `WorkItem` with `parent` the parent's `WorkItemRef`.
          return HttpResponse.json(
            { ...this.workItem(record), parent: parent ? this.ref(parent) : null, children: [] },
            { status },
          );
        }),
      ),
      // feat-029: the card's dialog reads the item over the backlog and the board.
      http.get(
        WORK_ITEM_URL,
        answer((p) => {
          const project = this.project(one(p, 'organizationId'), one(p, 'projectId'));
          return this.fullItem(this.record(project.id, one(p, 'identifier')));
        }),
      ),
      http.patch(
        WORK_ITEM_URL,
        answer((p, body) => {
          const project = this.project(one(p, 'organizationId'), one(p, 'projectId'));
          const record = this.record(project.id, one(p, 'identifier'));
          this.saveState(record, body);
          return this.fullItem(record);
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

  /**
   * The identifiers of a team's backlog of one level, in its order: the whole
   * backlog the reorder orders (mod-006 § 3.2), items nested beneath a row of
   * their own level included — not the page's top-level rows, which `backlog`
   * answers.
   */
  order(teamId: string, levelCode = 'stories'): string[] {
    const found = this.data.teams.find((candidate) => candidate.id === teamId);
    if (!found) throw new Error(`No team ${teamId}.`);
    return this.members(teamOf(found), levelCode).map((record) => this.identifierOf(record));
  }

  /**
   * The identifiers of a project's backlog of one level, in the project's order (feat-039
   * api-contract § 1), nested items included, as `order` gives a team's.
   */
  projectOrder(projectId: string, levelCode = 'stories'): string[] {
    return this.members({ projectId, team: null }, levelCode).map((record) =>
      this.identifierOf(record),
    );
  }

  /** The stored record of a number in a project. */
  item(projectId: string, number: number): WorkItemRecord {
    const found = this.data.items.find(
      (candidate) => candidate.projectId === projectId && candidate.number === number,
    );
    if (!found) throw new Error(`No item ${number} in ${projectId}.`);
    return found;
  }

  /**
   * Another screen's save that changes an item's owning team (mod-017 § 2.4), taken as the server
   * would: the item's team places go with the change (BR-OT-6) and «Изменён» moves.
   */
  giveTeam(projectId: string, number: number, teamId: string | null): void {
    const record = this.item(projectId, number);
    this.data.places = this.data.places.filter((place) => place.itemId !== record.id);
    record.teamId = teamId;
    record.changedAt = this.now();
  }

  /**
   * A team's deletion by another user (mod-017 § 2.7), taken as the server would: the team is
   * gone, its items have no team, its places go, and a default naming it is cleared.
   */
  deleteTeam(teamId: string): void {
    for (const team of this.data.teams) {
      if (team.id === teamId) team.active = false;
    }
    for (const record of this.data.items) {
      if (record.teamId === teamId) {
        record.teamId = null;
        record.changedAt = this.now();
      }
    }
    this.data.places = this.data.places.filter((place) => place.teamId !== teamId);
    for (const project of this.data.projects) {
      if (project.defaultTeamId === teamId) project.defaultTeamId = null;
    }
  }

  /** Another user's reorder (US-2 "Two users", US-7), taken as the server would. */
  reorderAsAnotherUser(teamId: string, levelCode: string, body: Record<string, unknown>): void {
    const found = this.data.teams.find((candidate) => candidate.id === teamId);
    if (!found) throw new Error(`No team ${teamId}.`);
    this.reorder(teamOf(found), levelCode, body);
  }

  /** Another user's reorder of the project's backlog (feat-039 US-2 "Two users"), taken as the server would. */
  reorderProjectAsAnotherUser(
    projectId: string,
    levelCode: string,
    body: Record<string, unknown>,
  ): void {
    this.reorder({ projectId, team: null }, levelCode, body);
  }
}

/** Serves the dataset (US-1's by default) for the rest of the test. */
export function serveBoards(
  data?: BoardsDataset,
  options: { projectList?: boolean } = {},
): BoardsTestApi {
  const api = new BoardsTestApi(data);
  server.use(...api.handlers(options));
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
