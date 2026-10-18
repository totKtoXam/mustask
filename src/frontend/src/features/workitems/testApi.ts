// MSW handlers over an in-memory copy of the stories' dataset, answering per
// mod-003's api-contract: not-found from the outside in (§ 0.3), the order of
// answers (§ 0.4), the list's order and paging (§ 2.1), replays by
// `requestId` (§ 2.2), saves of the sent fields only (§ 2.4), and the
// assignee search (§ 2.5); plus the process and area reads the screens reuse
// (§ 3). feat-001's api-contract adds the item's `parent` and `children`
// (§ 2.2), the save's `parent` with the parent write's checks in their order
// (§ 3.2, § 3.3), the parent write on its own (§ 3.2) and the parent search
// (§ 3.4). feat-022's api-contract adds the 17 fields per type to the answer
// (§ 2), their defaults on create (§ 3.1) and their save with its reasons
// (§ 3.2). mod-017's api-contract adds the owning team: `team` on every item and row, the
// team in the create and the save (§ 2.2, § 2.4) with its two refusals (§ 0.2), the list's
// `teamId` filter (§ 3) and the project's teams read the picker and the filter use.
// mod-005's api-contract adds the item's `deletion` (§ 1.1), the three writes (§ 2: delete,
// restore, purge), the list without deleted items (§ 3.2) and the save that refuses a deleted
// item first (§ 3.4).
// It keeps the contract's answers, not every rule of the backend.
import { HttpResponse, http, type HttpHandler } from 'msw';
import { server } from '../../test/mocks/server';
import type { AreaNode } from '../organizations';
import type {
  WorkItem,
  WorkItemFieldName,
  WorkItemFields,
  WorkItemListItem,
  WorkItemRef,
} from './api/workItemsApi';
import { LIST_FIELDS, WORK_ITEM_FIELD_NAMES, fieldsOf, kindOf } from './lib/fields';
import {
  AREAS_URL,
  CANDIDATES_URL,
  PARENT_CANDIDATES_URL,
  PROBLEM_HEADERS,
  PROCESS_URL,
  TEAMS_URL,
  WORK_ITEMS_URL,
  WORK_ITEM_DELETE_URL,
  WORK_ITEM_PARENT_URL,
  WORK_ITEM_PURGE_URL,
  WORK_ITEM_RESTORE_URL,
  WORK_ITEM_URL,
  defaultFields,
  exampleDataset,
  ids,
  linkedProjectIds,
  problem,
  removedStateId,
  problemWithParams,
  process,
  validationProblem,
  type Dataset,
  type ProblemBody,
  type ProjectRecord,
  type TeamRecord,
  type WorkItemRecord,
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

const UUID = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
const IDENTIFIER = /^([A-Z][A-Z0-9]{1,9})-([A-Z]+)-([1-9][0-9]*)$/;
const LINE_BREAK = /[\n\r\v\f\u0085\u2028\u2029]/;
const CANDIDATE_LIMIT = 20;
const PAGE_SIZE = 50;

/**
 * feat-022 § 3.2's value reason of a field the type has, sent `value` not
 * `null` — a list key, a number's range then its precision, a date's range, a
 * text's length — or `null`.
 */
function fieldReason(name: WorkItemFieldName, value: unknown): string | null {
  switch (kindOf(name)) {
    case 'criteria':
      if (!Array.isArray(value)) return 'workitems.workitem.criteria_invalid';
      for (const item of value) {
        if (!item || typeof item !== 'object') return 'workitems.workitem.criteria_invalid';
        if (
          typeof (item as { given?: unknown }).given !== 'string' ||
          typeof (item as { when?: unknown }).when !== 'string' ||
          typeof (item as { then?: unknown }).then !== 'string'
        ) {
          return 'workitems.workitem.criteria_invalid';
        }
      }
      return null;
    case 'text':
      return typeof value === 'string' && value.length > 100_000
        ? 'workitems.workitem.text_too_long'
        : null;
    case 'list': {
      const keys = LIST_FIELDS[name as keyof typeof LIST_FIELDS].options.map((option) =>
        LIST_FIELDS[name as keyof typeof LIST_FIELDS].numeric ? Number(option.key) : option.key,
      );
      return keys.includes(value as never) ? null : 'workitems.workitem.value_not_in_list';
    }
    case 'number': {
      const number = Number(value);
      if (number < 0 || number > 9_999) return 'workitems.workitem.number_out_of_range';
      if (name === 'businessValue') {
        return Number.isInteger(number) ? null : 'workitems.workitem.integer_required';
      }
      return Math.round(number * 100) / 100 === number
        ? null
        : 'workitems.workitem.number_too_precise';
    }
    case 'date':
      return typeof value === 'string' && value >= '2000-01-01' && value <= '2099-12-31'
        ? null
        : 'workitems.workitem.date_out_of_range';
  }
}

/** BR-M3-31: an always-set field emptied by a save takes its default. */
const EMPTIED_DEFAULTS: Partial<Record<WorkItemFieldName, unknown>> = {
  priority: 2,
  severity: 3,
  valueArea: 'Business',
};

function titleReason(title: unknown): string | null {
  const trimmed = typeof title === 'string' ? title.trim() : '';
  if (trimmed.length === 0) return 'workitems.workitem.title_required';
  if (trimmed.length > 255) return 'workitems.workitem.title_too_long';
  if (LINE_BREAK.test(trimmed)) return 'workitems.workitem.title_multiline';
  return null;
}

/**
 * The in-memory API. `serveWorkItems()` installs its handlers; a test reads
 * `requests` and the dataset, or puts a one-off answer in front with
 * `server.use` (MSW takes the newest handler first).
 */
export class WorkItemsTestApi {
  readonly data: Dataset;
  readonly requests: RecordedRequest[] = [];
  /** The signed-in user a Delete records as its deleter (mod-005 BR-DR-2): a user of the dataset. */
  actorId: string = ids.anna;
  /** Each accepted change is one minute after the latest time the dataset holds. */
  private clock: number;

  constructor(data: Dataset = exampleDataset()) {
    this.data = data;
    this.clock = Math.max(
      Date.UTC(2026, 9, 6),
      ...data.items.map((record) => Date.parse(record.changedAt)),
    );
  }

  private now(): string {
    this.clock += 60_000;
    return new Date(this.clock).toISOString();
  }

  // ---- lookups, from the outside in (§ 0.3)

  private organization(id: string | undefined) {
    const found = this.data.organizations.find(
      (candidate) => candidate.active && candidate.id === id?.toLowerCase(),
    );
    if (!found) refuse('organizations.organization.not_found');
    return found;
  }

  private project(organizationId: string | undefined, id: string | undefined): ProjectRecord {
    const organization = this.organization(organizationId);
    const found = this.data.projects.find(
      (candidate) =>
        candidate.active &&
        candidate.organizationId === organization.id &&
        candidate.id === id?.toLowerCase(),
    );
    if (!found) refuse('organizations.project.not_found');
    return found;
  }

  private item(project: ProjectRecord, identifier: string | undefined): WorkItemRecord {
    const match = IDENTIFIER.exec(identifier ?? '');
    const found =
      match && match[1] === project.key
        ? this.data.items.find(
            (candidate) =>
              candidate.projectId === project.id && candidate.number === Number(match[3]),
          )
        : undefined;
    if (!found || this.type(found.typeId).key !== match?.[2]) {
      refuse('workitems.workitem.not_found');
    }
    return found;
  }

  private type(typeId: string) {
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
    // The root by its own name; below it, the path from the root's child (DC-9).
    return names.length <= 1 ? (names[0] ?? '') : names.slice(1).join(' / ');
  }

  /** mod-017 api-contract § 0.2: the team a write names — the first row that applies. */
  private teamNamed(project: ProjectRecord, teamId: unknown): TeamRecord | null {
    if (teamId === null || teamId === undefined) {
      return null;
    }
    const found =
      typeof teamId === 'string'
        ? this.data.teams.find((candidate) => candidate.id === teamId.toLowerCase())
        : undefined;
    const owner = found && this.data.projects.find((candidate) => candidate.id === found.projectId);
    if (
      !found ||
      !found.active ||
      !owner ||
      !owner.active ||
      owner.organizationId !== project.organizationId
    ) {
      refuse('organizations.team.not_found');
    }
    if (!linkedProjectIds(found).includes(project.id)) {
      refuse('organizations.team.not_in_project');
    }
    return found;
  }

  private activeTeams(project: ProjectRecord): TeamRecord[] {
    return this.data.teams
      .filter((team) => linkedProjectIds(team).includes(project.id) && team.active)
      .sort(
        (first, second) =>
          first.name.localeCompare(second.name, 'ru', { sensitivity: 'base' }) ||
          first.id.localeCompare(second.id),
      );
  }

  /** BR-OT-4: the team the settings name, else the only active team, else none. */
  private defaultTeam(project: ProjectRecord): TeamRecord | null {
    const active = this.activeTeams(project);
    return (
      active.find((team) => team.id === project.defaultTeamId) ??
      (active.length === 1 ? (active[0] ?? null) : null)
    );
  }

  /** mod-017 api-contract § 1.3, for the picker and the filter. */
  private teamList(project: ProjectRecord) {
    const mark = this.defaultTeam(project);
    const named = this.activeTeams(project).some((team) => team.id === project.defaultTeamId);
    return this.activeTeams(project).map((team) => ({
      id: team.id,
      projectId: project.id,
      name: team.name,
      defaultMark: mark?.id === team.id ? (named ? 'named' : 'onlyTeam') : null,
      isMember: false,
    }));
  }

  private teamOf(record: WorkItemRecord): { id: string; name: string } | null {
    const team = record.teamId
      ? this.data.teams.find((candidate) => candidate.id === record.teamId && candidate.active)
      : undefined;
    return team ? { id: team.id, name: team.name } : null;
  }

  // ---- answers (§ 1)

  private listItem(record: WorkItemRecord): WorkItemListItem {
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
      state: {
        id: state?.id ?? record.stateId,
        name: state?.name ?? '',
        category: state?.category ?? { code: 'Proposed', label: '' },
      },
      assignee:
        record.assigneeId === null
          ? null
          : {
              id: record.assigneeId,
              displayName: user?.displayName ?? null,
              email: user ? user.email : null,
              isMember: user !== undefined && user.member !== false,
            },
      area: { id: record.areaId, path: this.areaPath(record.areaId) },
      team: this.teamOf(record),
      changedAt: record.changedAt,
    };
  }

  answer(record: WorkItemRecord): WorkItem {
    const parent = record.parentId ? this.byId(record.parentId) : undefined;
    const children = this.data.items
      .filter((candidate) => candidate.parentId === record.id && !this.removed(candidate))
      .sort(
        (first, second) => this.rank(second) - this.rank(first) || first.number - second.number,
      );
    const project = this.data.projects.find((candidate) => candidate.id === record.projectId);
    return {
      ...this.listItem(record),
      projectId: record.projectId,
      // feat-040 api-contract § 4.2: the project with its name, beside `projectId`.
      project: { id: record.projectId, name: project?.name ?? '' },
      description: record.description,
      createdBy: 'e2e-test-user',
      createdAt: record.createdAt,
      parent: parent ? this.ref(parent) : null,
      children: children.map((child) => this.ref(child)),
      deletion: this.deletionOf(record),
      ...record.fields,
    };
  }

  /** mod-005 § 1.1: the marks of the latest deletion, `null` for a live item or one with none. */
  private deletionOf(record: WorkItemRecord): WorkItem['deletion'] {
    const marks = record.deletion;
    if (!marks || !this.removed(record)) {
      return null;
    }
    const type = this.type(record.typeId);
    const held = type.states.find((candidate) => candidate.id === marks.fromStateId);
    const user = this.data.users.find((candidate) => candidate.id === marks.byId);
    return {
      fromState: {
        id: marks.fromStateId,
        name: held?.name ?? '',
        category: held?.category ?? { code: 'Proposed', label: '' },
      },
      by: {
        id: marks.byId,
        displayName: user?.displayName ?? null,
        isActive: user?.active ?? false,
        isMember: user !== undefined && user.member !== false,
      },
      at: marks.at,
    };
  }

  /** mod-005 § 2.1: the item moves to its type's «Удалена» and remembers the state, the actor and the time. */
  private deleteItem(record: WorkItemRecord): WorkItemRecord {
    if (this.removed(record)) refuse('workitems.workitem.deleted');
    const now = this.now();
    record.deletion = { fromStateId: record.stateId, byId: this.actorId, at: now };
    record.stateId = removedStateId(record);
    record.changedAt = now;
    return record;
  }

  /**
   * mod-005 § 2.2, in the order of the contract: not deleted, then the state (a state of the
   * type other than «Удалена»); absent, the state held, else the type's first.
   */
  private restoreItem(record: WorkItemRecord, body: Record<string, unknown>): WorkItemRecord {
    if (!this.removed(record)) refuse('workitems.workitem.not_deleted');
    const type = this.type(record.typeId);
    const chosen = typeof body.stateId === 'string' ? body.stateId : undefined;
    if (body.stateId !== undefined && body.stateId !== null) {
      const state = type.states.find((candidate) => candidate.id === chosen);
      if (!state || state.category.code === 'Removed')
        refuse('workitems.workitem.state_not_allowed');
    }
    const held = record.deletion?.fromStateId;
    record.stateId =
      chosen ??
      (held && type.states.some((candidate) => candidate.id === held)
        ? held
        : ((type.states.find((state) => state.isInitial) ?? type.states[0])?.id ?? record.stateId));
    record.deletion = null;
    record.changedAt = this.now();
    return record;
  }

  /** mod-005 § 2.3: the item is gone; its children survive, unparented, «Изменён» the purge's. */
  private purgeItem(record: WorkItemRecord): void {
    if (!this.removed(record)) refuse('workitems.workitem.not_deleted');
    const now = this.now();
    for (const child of this.data.items) {
      if (child.parentId === record.id) {
        child.parentId = null;
        child.changedAt = now;
      }
    }
    this.data.items.splice(this.data.items.indexOf(record), 1);
  }

  // ---- the hierarchy (feat-001 § 1, § 2.1)

  /** § 2.1: a parent, a child or a candidate. */
  ref(record: WorkItemRecord): WorkItemRef {
    const { id, identifier, type, title, state } = this.listItem(record);
    return { id, identifier, type, title, state };
  }

  private byId(id: string): WorkItemRecord | undefined {
    return this.data.items.find((candidate) => candidate.id === id);
  }

  /** The level rank of the record's type (mod-001's `backlogLevels[].rank`). */
  private rank(record: WorkItemRecord): number {
    const levelId = this.type(record.typeId).backlogLevel.id;
    return process.backlogLevels.find((level) => level.id === levelId)?.rank ?? 0;
  }

  /** § 0.4: in «Удалена» — its state's category is `Removed`. */
  private removed(record: WorkItemRecord): boolean {
    const state = this.type(record.typeId).states.find(
      (candidate) => candidate.id === record.stateId,
    );
    return state?.category.code === 'Removed';
  }

  private identifierOf(record: WorkItemRecord): string {
    return this.listItem(record).identifier;
  }

  /** § 0.3 step 2: `parentId` names an item of this project, or the write is refused. */
  private newParent(project: ProjectRecord, parentId: unknown): WorkItemRecord | null {
    if (parentId === null || parentId === undefined) {
      return null;
    }
    const found =
      typeof parentId === 'string' && UUID.test(parentId)
        ? this.data.items.find(
            (candidate) =>
              candidate.projectId === project.id && candidate.id === parentId.toLowerCase(),
          )
        : undefined;
    if (!found) refuse('workitems.link.parent_not_found');
    return found;
  }

  /**
   * § 3.2's checks 3–7 in their order: deleted, the stale view, self, the
   * level, the cycle with its path. The first that fails refuses.
   */
  private checkParent(
    record: WorkItemRecord,
    parent: WorkItemRecord | null,
    seenParentId: unknown,
  ): void {
    if (this.removed(record) || (parent && this.removed(parent))) {
      refuse('workitems.link.item_deleted');
    }
    const stored = record.parentId ? this.byId(record.parentId) : undefined;
    const seen = typeof seenParentId === 'string' ? seenParentId.toLowerCase() : null;
    const matches =
      stored === undefined
        ? seen === null
        : stored.id === seen || (seen === null && this.removed(stored));
    if (!matches) refuse('workitems.link.parent_changed');
    if (parent === null) {
      return;
    }
    if (parent.id === record.id) refuse('workitems.link.self_parent');
    if (this.rank(parent) < this.rank(record)) refuse('workitems.link.parent_level_not_allowed');
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
      current = current.parentId ? this.byId(current.parentId) : undefined;
    }
  }

  /**
   * § 3.2: the parent write on its own — the body's parent, the checks in
   * their order, then the link; «Изменён» moves on the child and each live
   * parent it adds or removes (BR-H-5). The answer is the stored parent.
   */
  private writeParent(
    project: ProjectRecord,
    record: WorkItemRecord,
    body: Record<string, unknown>,
  ): { parent: WorkItemRef | null } {
    const parent = this.newParent(project, body.parentId);
    this.checkParent(record, parent, body.seenParentId);
    const before = record.parentId ?? null;
    const after = parent?.id ?? null;
    if (after !== before) {
      record.parentId = after;
      const now = this.now();
      record.changedAt = now;
      for (const id of [before, after]) {
        const touched = id ? this.byId(id) : undefined;
        if (touched && !this.removed(touched)) touched.changedAt = now;
      }
    }
    return { parent: parent ? this.ref(parent) : null };
  }

  /** § 3.4: the item's own project, never itself or a deleted item, of an allowed type. */
  private parentCandidates(record: WorkItemRecord, text: string | null, levelAbove: boolean) {
    const needle = (text ?? '').trim().toLocaleLowerCase('ru');
    if (needle.length > 255) {
      return { items: [], moreMatched: false };
    }
    const own = this.rank(record);
    const above = Math.min(
      ...process.backlogLevels.map((level) => level.rank).filter((rank) => rank > own),
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

  // ---- routes (§ 2, § 3)

  private tree(project: ProjectRecord): AreaNode[] {
    const nodes: AreaNode[] = [];
    const visit = (parentId: string | null, depth: number) => {
      const children = this.data.areas
        .filter((area) => area.projectId === project.id && area.parentId === parentId)
        .sort((first, second) => first.name.localeCompare(second.name, 'ru'));
      for (const area of children) {
        nodes.push({
          id: area.id,
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

  /** api-contract § 3: any chosen value matches; a value naming no team of the project selects nothing. */
  private matchesFilter(
    record: WorkItemRecord,
    project: ProjectRecord,
    teamIds: string[],
  ): boolean {
    if (teamIds.length === 0) {
      return true;
    }
    const team = this.teamOf(record);
    const known = new Set(this.activeTeams(project).map((candidate) => candidate.id));
    return teamIds.some((value) =>
      value === 'none'
        ? team === null
        : known.has(value.toLowerCase()) && team?.id === value.toLowerCase(),
    );
  }

  private list(project: ProjectRecord, pageParameter: string | null, teamIds: string[]) {
    const page = pageParameter === null ? 1 : Number(pageParameter);
    if (page < 1) {
      throw new Refusal(validationProblem({ page: 'workitems.invalid_page' }));
    }
    // mod-005 § 3.2: the list and its count are the items outside «Удалена».
    const items = this.data.items
      .filter(
        (record) =>
          record.projectId === project.id &&
          !this.removed(record) &&
          this.matchesFilter(record, project, teamIds),
      )
      .sort(
        (first, second) =>
          Date.parse(second.changedAt) - Date.parse(first.changedAt) ||
          second.number - first.number,
      );
    return {
      count: items.length,
      data: items
        .slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)
        .map((record) => this.listItem(record)),
    };
  }

  private create(project: ProjectRecord, body: Record<string, unknown>) {
    const reasons: Record<string, string> = {};
    const requestId = typeof body.requestId === 'string' ? body.requestId : '';
    if (!UUID.test(requestId) || /^[0-]+$/.test(requestId)) {
      reasons.requestId = 'workitems.workitem.request_id_invalid';
    }
    const title = titleReason(body.title);
    if (title) reasons.title = title;
    if (Object.keys(reasons).length > 0) {
      throw new Refusal(validationProblem(reasons));
    }
    const type = process.workItemTypes.find((candidate) => candidate.id === body.typeId);
    if (!type) refuse('workitems.workitem.type_not_found');

    const replay = this.data.items.find(
      (record) => record.projectId === project.id && record.requestId === requestId.toLowerCase(),
    );
    if (replay) {
      return { status: 200, item: replay };
    }
    // api-contract § 0.4: the replay precedes the team; a child takes its parent's team and a team
    // sent beside a parent is not read (BR-OT-3).
    const parent =
      typeof body.parentId === 'string' ? this.newParent(project, body.parentId) : null;
    const team = parent
      ? ((parent.teamId ? this.teamNamed(project, parent.teamId) : null) ??
        this.defaultTeam(project))
      : 'team' in body
        ? this.teamNamed(project, (body.team as { teamId?: unknown } | null)?.teamId)
        : this.defaultTeam(project);
    const number =
      Math.max(
        0,
        ...this.data.items
          .filter((record) => record.projectId === project.id)
          .map((record) => record.number),
      ) + 1;
    const root = this.data.areas.find(
      (area) => area.projectId === project.id && area.parentId === null,
    );
    const now = this.now();
    const record: WorkItemRecord = {
      id: `c1000000-0000-4000-8000-${String(this.data.items.length + 1).padStart(12, '0')}`,
      projectId: project.id,
      number,
      typeId: type.id,
      title: (body.title as string).trim(),
      description: '',
      stateId: (type.states.find((state) => state.isInitial) ?? type.states[0])?.id ?? '',
      assigneeId: null,
      areaId: parent?.areaId ?? root?.id ?? '',
      teamId: team?.id ?? null,
      parentId: parent?.id ?? null,
      createdAt: now,
      changedAt: now,
      requestId: requestId.toLowerCase(),
      // feat-022 § 3.1: the body's fields are ignored; the type's defaults.
      fields: defaultFields(type.appearance),
    };
    this.data.items.push(record);
    return { status: 201, item: record };
  }

  /**
   * feat-022 § 3.2: the fields sent — absent or `null` leaves one alone, a
   * text as a string, any other in `{ value }` — with their reasons: not on
   * the type, then the value, then the target before the start against what
   * would be stored. Answers the values to store, or adds the reasons.
   */
  private fieldsSent(
    record: WorkItemRecord,
    body: Record<string, unknown>,
    reasons: Record<string, string>,
  ): Partial<WorkItemFields> {
    const has = new Set(fieldsOf(this.type(record.typeId).appearance));
    const sent: Record<string, unknown> = {};
    for (const name of WORK_ITEM_FIELD_NAMES) {
      const member = body[name];
      if (member === undefined || member === null) continue;
      if (!has.has(name)) {
        reasons[name] = 'workitems.workitem.field_not_on_type';
        continue;
      }
      const value =
        kindOf(name) === 'text' || kindOf(name) === 'criteria'
          ? member
          : ((member as { value?: unknown }).value ?? null);
      const reason = value === null ? null : fieldReason(name, value);
      if (reason) {
        reasons[name] = reason;
        continue;
      }
      sent[name] = value === null ? (EMPTIED_DEFAULTS[name] ?? null) : value;
    }
    const after = { ...record.fields, ...sent };
    if (
      !reasons.startDate &&
      !reasons.targetDate &&
      after.startDate !== null &&
      after.targetDate !== null &&
      after.targetDate < after.startDate
    ) {
      reasons.targetDate = 'workitems.workitem.target_before_start';
    }
    return sent as Partial<WorkItemFields>;
  }

  private save(project: ProjectRecord, record: WorkItemRecord, body: Record<string, unknown>) {
    // mod-005 § 3.4: the stored state is judged first — before the values, the references, the state.
    if (this.removed(record)) refuse('workitems.workitem.deleted');
    const reasons: Record<string, string> = {};
    if (body.title !== undefined && body.title !== null) {
      const title = titleReason(body.title);
      if (title) reasons.title = title;
    }
    if (typeof body.description === 'string' && body.description.length > 100_000) {
      reasons.description = 'workitems.workitem.description_too_long';
    }
    const fields = this.fieldsSent(record, body, reasons);
    if (Object.keys(reasons).length > 0) {
      throw new Refusal(validationProblem(reasons));
    }
    if (typeof body.areaId === 'string') {
      const area = this.data.areas.find(
        (candidate) => candidate.id === body.areaId && candidate.projectId === project.id,
      );
      if (!area) refuse('organizations.area.not_found');
    }
    // mod-017 § 2.4: the team after the area; absent leaves it, `{ teamId: null }` is «Без команды».
    const teamChange = body.team as { teamId?: unknown } | null | undefined;
    const team = teamChange ? this.teamNamed(project, teamChange.teamId) : undefined;
    // feat-001 § 3.3: `parent: null` or none leaves the parent as it is.
    const parentChange = body.parent as
      { parentId?: unknown; seenParentId?: unknown } | null | undefined;
    const parent = parentChange ? this.newParent(project, parentChange.parentId) : null;
    const type = this.type(record.typeId);
    if (typeof body.stateId === 'string') {
      const state = type.states.find((candidate) => candidate.id === body.stateId);
      if (!state || state.category.code === 'Removed') {
        refuse('workitems.workitem.state_not_allowed');
      }
    }
    const assignee = body.assignee as { userId?: string | null } | null | undefined;
    const assigneeId = assignee ? (assignee.userId ?? null) : undefined;
    if (assigneeId) {
      const user = this.data.users.find((candidate) => candidate.id === assigneeId);
      if ((!user || !user.active || user.member === false) && assigneeId !== record.assigneeId) {
        refuse('workitems.workitem.assignee_unavailable');
      }
    }
    if (parentChange) {
      this.checkParent(record, parent, parentChange.seenParentId);
    }

    const before = JSON.stringify(record);
    const parentBefore = record.parentId ?? null;
    if (typeof body.title === 'string') record.title = body.title.trim();
    if (typeof body.description === 'string') record.description = body.description;
    if (typeof body.stateId === 'string') record.stateId = body.stateId;
    if (assigneeId !== undefined) record.assigneeId = assigneeId;
    if (typeof body.areaId === 'string') record.areaId = body.areaId;
    if (team !== undefined) record.teamId = team?.id ?? null;
    const parentAfter = parentChange ? (parent?.id ?? null) : parentBefore;
    if (parentAfter !== parentBefore) record.parentId = parentAfter;
    record.fields = { ...record.fields, ...fields };
    if (JSON.stringify(record) !== before) {
      const now = this.now();
      record.changedAt = now;
      // BR-H-5: each live parent the save adds or removes moves «Изменён» too.
      if (parentAfter !== parentBefore) {
        for (const id of [parentBefore, parentAfter]) {
          const touched = id ? this.byId(id) : undefined;
          if (touched && !this.removed(touched)) touched.changedAt = now;
        }
      }
    }
    return record;
  }

  private candidates(text: string | null) {
    const needle = (text ?? '').trim().toLocaleLowerCase('ru');
    if (needle.length > 100) {
      throw new Refusal({
        ...problem('user_directory.validation_failed'),
        errors: {
          text: [
            {
              code: 'user_directory.value_too_long',
              detail: problem('user_directory.value_too_long').detail,
            },
          ],
        },
      });
    }
    if (needle.length < 2) {
      return { users: [], moreMatched: false };
    }
    const matched = this.data.users
      .filter(
        (user) =>
          user.active &&
          user.member !== false &&
          (user.displayName.toLocaleLowerCase('ru').includes(needle) ||
            user.email.toLocaleLowerCase('ru').includes(needle)),
      )
      .sort(
        (first, second) =>
          first.displayName.localeCompare(second.displayName, 'ru') ||
          first.id.localeCompare(second.id),
      );
    return {
      users: matched
        .slice(0, CANDIDATE_LIMIT)
        .map(({ id, displayName, email }) => ({ id, displayName, email })),
      moreMatched: matched.length > CANDIDATE_LIMIT,
    };
  }

  // ---- MSW

  handlers(): HttpHandler[] {
    type Params = Record<string, string | readonly string[] | undefined>;
    const one = (params: Params, name: string) => {
      const value = params[name];
      return typeof value === 'string' ? value : undefined;
    };
    const answer =
      (run: (params: Params, url: URL, body: Record<string, unknown>) => unknown) =>
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
          const result = run(params, url, body);
          if (result instanceof HttpResponse) {
            return result;
          }
          return HttpResponse.json(result as object, { status: 200 });
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
    const pr = (p: Params) => this.project(one(p, 'organizationId'), one(p, 'projectId'));

    return [
      http.get(
        PROCESS_URL,
        answer((p) => {
          this.organization(one(p, 'organizationId'));
          return process;
        }),
      ),
      http.get(
        AREAS_URL,
        answer((p) => this.tree(pr(p))),
      ),
      http.get(
        TEAMS_URL,
        answer((p) => this.teamList(pr(p))),
      ),
      http.get(
        WORK_ITEMS_URL,
        answer((p, url) =>
          this.list(pr(p), url.searchParams.get('page'), url.searchParams.getAll('teamId')),
        ),
      ),
      http.post(
        WORK_ITEMS_URL,
        answer((p, _url, body) => {
          const { status, item } = this.create(pr(p), body);
          return HttpResponse.json(this.answer(item), { status });
        }),
      ),
      http.get(
        WORK_ITEM_URL,
        answer((p) => this.answer(this.item(pr(p), one(p, 'identifier')))),
      ),
      http.patch(
        WORK_ITEM_URL,
        answer((p, _url, body) => {
          const project = pr(p);
          return this.answer(this.save(project, this.item(project, one(p, 'identifier')), body));
        }),
      ),
      http.post(
        WORK_ITEM_DELETE_URL,
        answer((p) => {
          const project = pr(p);
          return this.answer(this.deleteItem(this.item(project, one(p, 'identifier'))));
        }),
      ),
      http.post(
        WORK_ITEM_RESTORE_URL,
        answer((p, _url, body) => {
          const project = pr(p);
          return this.answer(this.restoreItem(this.item(project, one(p, 'identifier')), body));
        }),
      ),
      http.post(
        WORK_ITEM_PURGE_URL,
        answer((p) => {
          const project = pr(p);
          this.purgeItem(this.item(project, one(p, 'identifier')));
          return new HttpResponse(null, { status: 204 });
        }),
      ),
      http.get(
        CANDIDATES_URL,
        answer((p, url) => {
          this.organization(one(p, 'organizationId'));
          return this.candidates(url.searchParams.get('text'));
        }),
      ),
      http.put(
        WORK_ITEM_PARENT_URL,
        answer((p, _url, body) => {
          const project = pr(p);
          return this.writeParent(project, this.item(project, one(p, 'identifier')), body);
        }),
      ),
      http.get(
        PARENT_CANDIDATES_URL,
        answer((p, url) => {
          const record = this.item(pr(p), one(p, 'identifier'));
          return this.parentCandidates(
            record,
            url.searchParams.get('text'),
            url.searchParams.get('levelAbove') === 'true',
          );
        }),
      ),
    ];
  }

  /**
   * Takes a create as the server would, for a test whose answer never reaches
   * the panel (a lost answer): recorded and applied, nothing returned.
   */
  acceptCreate(organizationId: string, projectId: string, body: Record<string, unknown>): void {
    this.requests.push({
      method: 'POST',
      path: `/api/organizations/${organizationId}/projects/${projectId}/workitems`,
      body,
    });
    this.create(this.project(organizationId, projectId), body);
  }

  /** The writes received, reads left out. */
  writes(): RecordedRequest[] {
    return this.requests.filter((request) => request.method !== 'GET');
  }

  /** The reads of one route kind received, e.g. `'/workitems?'` for the list. */
  reads(fragment: string): RecordedRequest[] {
    return this.requests.filter(
      (request) => request.method === 'GET' && request.path.includes(fragment),
    );
  }

  /** The stored record of `identifier`'s number in a project. */
  record(projectId: string, number: number): WorkItemRecord {
    const found = this.data.items.find(
      (candidate) => candidate.projectId === projectId && candidate.number === number,
    );
    if (!found) throw new Error(`No item ${number} in ${projectId}.`);
    return found;
  }
}

/** Serves the dataset (the stories' example by default) for the rest of the test. */
export function serveWorkItems(data?: Dataset): WorkItemsTestApi {
  const api = new WorkItemsTestApi(data);
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
