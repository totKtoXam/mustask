// Spec 22: Node-side access to the routes a work item spec sets up through the
// API instead of the panel — mod-002's area create (its api-contract § 4.2)
// and mod-003's create and save (its api-contract § 2.2, § 2.4). Every call
// goes through `organizationsApi.ts`'s `send`, so the token exchange, its
// rules (never logged, never handed to a page) and the prior `signIn(page, …)`
// it needs are that module's.
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
  method: 'GET' | 'POST' | 'PATCH',
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
