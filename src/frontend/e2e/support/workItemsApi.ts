// Spec 22: Node-side access to the routes a work item spec sets up through the
// API instead of the panel — mod-002's area create (its api-contract § 4.2)
// and mod-003's create and save (its api-contract § 2.2, § 2.4); spec 24 adds
// feat-001's parent write (its api-contract § 3.2), spec 25 a save of
// feat-022's fields per type (its api-contract § 3.2), spec 26 a create that
// names its team and its parent (feat-027's api-contract § 3) and the id of a
// state by its name (mod-001's process, for a save of `stateId`); spec 27 adds an
// area beneath a team's default area (mod-002's area create with another
// parent) and the id of a user of the directory (mod-003's assignee
// candidates, api-contract § 2.5). Every call goes through
// `organizationsApi.ts`'s `send`, so the token exchange, its rules (never
// logged, never handed to a page) and the prior `signIn(page, …)` it needs
// are that module's.
import { randomUUID } from 'node:crypto';
import type { APIRequestContext } from '@playwright/test';
import { send } from './organizationsApi';

export interface AreaDto {
  id: string;
  projectId: string;
  parentId: string;
  name: string;
}

export interface WorkItemDto {
  id: string;
  identifier: string;
  title: string;
  area: { id: string; path: string };
}

async function call<T>(
  request: APIRequestContext,
  method: 'GET' | 'POST' | 'PUT' | 'PATCH',
  path: string,
  body?: unknown,
): Promise<T> {
  const response = await send(request, method, path, body);
  if (!response.ok()) {
    throw new Error(`${method} ${path} failed: HTTP ${response.status()} ${await response.text()}`);
  }
  return (await response.json()) as T;
}

const projectPath = (organizationId: string, projectId: string) =>
  `/api/organizations/${encodeURIComponent(organizationId)}/projects/${encodeURIComponent(projectId)}`;

/** The project's root area: the first node of the tree, which comes in pre-order. */
export async function rootArea(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
): Promise<{ id: string; path: string }> {
  const tree = await call<{ id: string; parentId: string | null; path: string }[]>(
    request,
    'GET',
    `${projectPath(organizationId, projectId)}/areas`,
  );
  const root = tree.find((node) => node.parentId === null);
  if (!root) throw new Error('The project has no root area.');
  return root;
}

/** A child of the root, owned by inheritance. */
export async function createArea(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
  name: string,
): Promise<AreaDto> {
  const root = await rootArea(request, organizationId, projectId);
  return call(request, 'POST', `${projectPath(organizationId, projectId)}/areas`, {
    parentId: root.id,
    name,
    ownerTeamId: null,
  });
}

/**
 * An area beneath `parentId` — a team's default area, say, so that the area's
 * path reads «Платформа / API» — owned by inheritance (mod-002 api-contract
 * § 4.2); `createArea` above is the child of the root.
 */
export function createChildArea(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
  parentId: string,
  name: string,
): Promise<AreaDto> {
  return call(request, 'POST', `${projectPath(organizationId, projectId)}/areas`, {
    parentId,
    name,
    ownerTeamId: null,
  });
}

/**
 * The id of the one user of the organization's directory whose e-mail or
 * display name contains `text` (mod-003 api-contract § 2.5; two characters at
 * least), for a save of `assignee`. The fixture user is `e2e-fixture@…`.
 */
export async function assigneeId(
  request: APIRequestContext,
  organizationId: string,
  text: string,
): Promise<string> {
  const found = await call<{ users: { id: string; displayName: string | null; email: string }[] }>(
    request,
    'GET',
    `/api/organizations/${encodeURIComponent(organizationId)}/workitems/assignee-candidates?text=${encodeURIComponent(text)}`,
  );
  const [user] = found.users;
  if (!user) throw new Error(`The directory has no user matching «${text}».`);
  return user.id;
}

/** One type's id from the organization's process (mod-001 api-contract § 1). */
export async function typeId(
  request: APIRequestContext,
  organizationId: string,
  key: string,
): Promise<string> {
  const process = await call<{ workItemTypes: { id: string; key: string }[] }>(
    request,
    'GET',
    `/api/organizations/${encodeURIComponent(organizationId)}/process`,
  );
  const type = process.workItemTypes.find((candidate) => candidate.key === key);
  if (!type) throw new Error(`The process has no type ${key}.`);
  return type.id;
}

/**
 * One state's id from the organization's process (mod-001 api-contract § 1):
 * the state `name` of the type `key`, for a save of `stateId` (mod-003 § 2.4).
 */
export async function stateId(
  request: APIRequestContext,
  organizationId: string,
  key: string,
  name: string,
): Promise<string> {
  const process = await call<{
    workItemTypes: { key: string; states: { id: string; name: string }[] }[];
  }>(request, 'GET', `/api/organizations/${encodeURIComponent(organizationId)}/process`);
  const state = process.workItemTypes
    .find((candidate) => candidate.key === key)
    ?.states.find((candidate) => candidate.name === name);
  if (!state) throw new Error(`The process has no state «${name}» on the type ${key}.`);
  return state.id;
}

/** § 2.2: a fresh submission, so a fresh `requestId`. */
export function createWorkItem(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
  body: { typeId: string; title: string },
): Promise<WorkItemDto> {
  return call(request, 'POST', `${projectPath(organizationId, projectId)}/workitems`, {
    requestId: randomUUID(),
    ...body,
  });
}

/**
 * § 2.2 with mod-006's `teamId` and feat-027's `parentId` (its api-contract
 * § 3): a create that lands in the default area of `teamId`, or — beneath
 * `parentId` — in the parent's area, linked to it in the same request. A fresh
 * submission, so a fresh `requestId`. `createWorkItem` above stays the plain
 * create, with neither.
 */
export function createWorkItemIn(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
  body: { typeId: string; title: string; teamId?: string; parentId?: string },
): Promise<WorkItemDto> {
  return call(request, 'POST', `${projectPath(organizationId, projectId)}/workitems`, {
    requestId: randomUUID(),
    ...body,
  });
}

/** § 2.4: a save of the area alone. */
export function moveWorkItem(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
  identifier: string,
  areaId: string,
): Promise<WorkItemDto> {
  return call(
    request,
    'PATCH',
    `${projectPath(organizationId, projectId)}/workitems/${encodeURIComponent(identifier)}`,
    { areaId },
  );
}

/**
 * § 2.4 with feat-022 § 3.2's fields per type: a save of exactly `body` —
 * texts as strings, every other field in `{ value }`.
 */
export function saveWorkItem(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
  identifier: string,
  body: Record<string, unknown>,
): Promise<WorkItemDto> {
  return call(
    request,
    'PATCH',
    `${projectPath(organizationId, projectId)}/workitems/${encodeURIComponent(identifier)}`,
    body,
  );
}

/**
 * feat-001 § 3.2: `identifier`'s parent set to `parentId` (`null` clears it),
 * having seen `seenParentId` — none by default, which is what a freshly
 * created item has.
 */
export function setParent(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
  identifier: string,
  parentId: string | null,
  seenParentId: string | null = null,
): Promise<{ parent: { identifier: string } | null }> {
  return call(
    request,
    'PUT',
    `${projectPath(organizationId, projectId)}/workitems/${encodeURIComponent(identifier)}/parent`,
    { parentId, seenParentId },
  );
}
