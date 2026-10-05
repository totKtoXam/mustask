// Specs 19-21: Node-side access to the real `/api/organizations/*` routes
// (mod-002 api-contract § 1-§ 4), used to set up and restore organizations,
// projects and teams around a test instead of driving the panel's own UI for
// that — and to make a request the UI never sends (21.6's direct create).
//
// The same token exchange as `dictionariesApi.ts`, kept as this module's own
// copy so specs 17-18's helper stays untouched: `POST /api/session/token` on
// the browser context's own session cookie, through Playwright's
// `APIRequestContext` (`page.request`), which shares that cookie jar. A prior
// `signIn(page, ...)` is what makes every call here succeed; nothing here
// talks to Keycloak or reads a password. The token never touches a page, is
// never logged and is never returned to a caller; a fresh one is fetched per
// call (the stand's access-token lifespan is 120 s).
//
// Names are unique instance-wide and nothing is ever purged (BR-M1-1), so a
// spec names what it creates with `runName(...)` and deactivates it after.
import { randomUUID } from 'node:crypto';
import type { APIRequestContext, APIResponse } from '@playwright/test';
import { env } from './env';

export interface OrganizationDto {
  id: string;
  name: string;
}

export interface ProjectDto {
  id: string;
  organizationId: string;
  name: string;
  key: string;
}

export interface TeamDto {
  id: string;
  projectId: string;
  name: string;
  isFirstTeam: boolean;
  defaultArea: { id: string; path: string };
}

export interface AreaNodeDto {
  id: string;
  parentId: string | null;
  name: string;
  path: string;
  ownerTeamId: string | null;
}

/** A name no other run has used: «<prefix> <8 hex>». */
export function runName(prefix: string): string {
  return `${prefix} ${randomUUID().slice(0, 8)}`;
}

/** A project key for a fresh organization: a capital letter, then 5 hex digits upper-cased. */
export function runKey(prefix = 'E'): string {
  return `${prefix}${randomUUID().replace(/-/g, '').slice(0, 5).toUpperCase()}`;
}

async function accessToken(request: APIRequestContext): Promise<string> {
  const response = await request.post(`${env().apiUrl}/api/session/token`);
  if (!response.ok()) {
    throw new Error(
      `POST /api/session/token failed: HTTP ${response.status()} — is the caller signed in (signIn(page, ...)) before calling into organizationsApi.ts?`,
    );
  }
  const body = (await response.json()) as { accessToken: string };
  return body.accessToken;
}

/** One request with a fresh token; the raw response, for a spec that reads a refusal. */
export async function send(
  request: APIRequestContext,
  method: 'GET' | 'POST' | 'PUT' | 'DELETE',
  path: string,
  body?: unknown,
): Promise<APIResponse> {
  const token = await accessToken(request);
  return request.fetch(`${env().apiUrl}${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}` },
    data: body,
  });
}

async function call<T>(
  request: APIRequestContext,
  method: 'GET' | 'POST' | 'PUT' | 'DELETE',
  path: string,
  body?: unknown,
): Promise<T> {
  const response = await send(request, method, path, body);
  if (!response.ok()) {
    throw new Error(`${method} ${path} failed: HTTP ${response.status()} ${await response.text()}`);
  }
  return response.status() === 204 ? (undefined as T) : ((await response.json()) as T);
}

const organizationPath = (organizationId: string) =>
  `/api/organizations/${encodeURIComponent(organizationId)}`;
const projectPath = (organizationId: string, projectId: string) =>
  `${organizationPath(organizationId)}/projects/${encodeURIComponent(projectId)}`;

export function activeOrganizations(request: APIRequestContext): Promise<OrganizationDto[]> {
  return call(request, 'GET', '/api/organizations');
}

export function createOrganization(
  request: APIRequestContext,
  name: string,
): Promise<OrganizationDto> {
  return call(request, 'POST', '/api/organizations', { name });
}

export function deactivateOrganization(
  request: APIRequestContext,
  organizationId: string,
): Promise<void> {
  return call(request, 'POST', `${organizationPath(organizationId)}/deactivate`);
}

export function restoreOrganization(
  request: APIRequestContext,
  organizationId: string,
): Promise<OrganizationDto> {
  return call(request, 'POST', `${organizationPath(organizationId)}/restore`);
}

export function createProject(
  request: APIRequestContext,
  organizationId: string,
  body: { name: string; key: string },
): Promise<ProjectDto> {
  return call(request, 'POST', `${organizationPath(organizationId)}/projects`, body);
}

export function teams(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
): Promise<TeamDto[]> {
  return call(request, 'GET', `${projectPath(organizationId, projectId)}/teams`);
}

export function addTeam(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
  name: string,
): Promise<TeamDto> {
  return call(request, 'POST', `${projectPath(organizationId, projectId)}/teams`, { name });
}

export function areaTree(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
): Promise<AreaNodeDto[]> {
  return call(request, 'GET', `${projectPath(organizationId, projectId)}/areas`);
}

/** An organization with one project, whose first team is named after it, and the teams added. */
export interface ContextFixture {
  organization: OrganizationDto;
  project: ProjectDto;
  teams: TeamDto[];
}

/**
 * Creates an organization, a project in it and the extra teams named, through
 * the API; the caller deactivates the organization when done
 * (`deactivateOrganization`), which hides everything beneath it.
 */
export async function createContext(
  request: APIRequestContext,
  names: { organization: string; project: string; teams?: readonly string[] },
): Promise<ContextFixture> {
  const organization = await createOrganization(request, names.organization);
  const project = await createProject(request, organization.id, {
    name: names.project,
    key: runKey('C'),
  });
  for (const team of names.teams ?? []) {
    await addTeam(request, organization.id, project.id, team);
  }
  return { organization, project, teams: await teams(request, organization.id, project.id) };
}

/**
 * Deactivates every active organization and returns their ids, so that a
 * spec can see the shell with no organization context — «Настройки» opening
 * «Языки» — and restore them after (`restoreOrganizations`).
 */
export async function deactivateAllOrganizations(request: APIRequestContext): Promise<string[]> {
  const active = await activeOrganizations(request);
  for (const organization of active) {
    await deactivateOrganization(request, organization.id);
  }
  return active.map((organization) => organization.id);
}

export async function restoreOrganizations(
  request: APIRequestContext,
  organizationIds: readonly string[],
): Promise<void> {
  for (const id of organizationIds) {
    await restoreOrganization(request, id);
  }
}
