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
// (§ 3.2). It keeps the contract's answers, not every rule of the backend.
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
  WORK_ITEMS_URL,
  WORK_ITEM_PARENT_URL,
  WORK_ITEM_URL,
  defaultFields,
  exampleDataset,
  problem,
  problemWithParams,
  process,
  validationProblem,
  type Dataset,
  type ProblemBody,
  type ProjectRecord,
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
            },
      area: { id: record.areaId, path: this.areaPath(record.areaId) },
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
    return {
      ...this.listItem(record),
      projectId: record.projectId,
      description: record.description,
      createdBy: 'e2e-test-user',
      createdAt: record.createdAt,
      parent: parent ? this.ref(parent) : null,
      children: children.map((child) => this.ref(child)),
      ...record.fields,
    };
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
          ownerTeamId: null,
          resolvedOwner: { id: 'team', name: project.name },
          ownerInherited: depth > 0,
          defaultOfTeam: null,
        });
        visit(area.id, depth + 1);
      }
    };
    visit(null, 0);
    return nodes;
  }

  private list(project: ProjectRecord, pageParameter: string | null) {
    const page = pageParameter === null ? 1 : Number(pageParameter);
    if (page < 1) {
      throw new Refusal(validationProblem({ page: 'workitems.invalid_page' }));
    }
    const items = this.data.items
      .filter((record) => record.projectId === project.id)
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
      areaId: root?.id ?? '',
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
        kindOf(name) === 'text' ? member : ((member as { value?: unknown }).value ?? null);
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
      if ((!user || !user.active) && assigneeId !== record.assigneeId) {
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
        WORK_ITEMS_URL,
        answer((p, url) => this.list(pr(p), url.searchParams.get('page'))),
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
