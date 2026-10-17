import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { HttpResponse, http } from 'msw';
import { ApiError } from '../../../shared/api/httpClient';
import { server } from '../../../test/mocks/server';
import { API } from '../testFixtures';
import {
  addTeam,
  createArea,
  createOrganization,
  createProject,
  deactivateOrganization,
  deactivateProject,
  deleteArea,
  deleteTeam,
  getAreaTree,
  getOrganization,
  getOrganizations,
  getProject,
  getProjects,
  getTeam,
  getTeams,
  moveArea,
  renameArea,
  renameOrganization,
  renameProject,
  renameTeam,
  restoreOrganization,
  restoreProject,
  getOwnedItemsCount,
  setDefaultTeam,
} from './organizationsApi';

interface Sent {
  method: string;
  path: string;
  body: unknown;
}

const sent: Sent[] = [];

/** Answers every module request: 204 for the routes that have no body, else the echo. */
beforeEach(() => {
  sent.length = 0;
  server.use(
    http.all(`${API}/api/organizations*`, async ({ request }) => {
      const raw = request.method === 'GET' ? '' : await request.text();
      const path = new URL(request.url).pathname;
      sent.push({ method: request.method, path, body: raw ? JSON.parse(raw) : undefined });
      if (/\/(deactivate)$/.test(path) || request.method === 'DELETE') {
        return new HttpResponse(null, { status: 204 });
      }
      return HttpResponse.json({ ok: true });
    }),
  );
});

afterEach(() => {
  sent.length = 0;
});

describe('mod-002 api-contract § 5: one function per route, as the contract names it', () => {
  it('sends every read and write to its route, with exactly the contract’s body', async () => {
    await getOrganizations();
    await getOrganizations('deactivated');
    await createOrganization('Альфа');
    await getOrganization('o');
    await renameOrganization('o', 'Бета');
    await deactivateOrganization('o');
    await restoreOrganization('o');
    await getProjects('o');
    await getProjects('o', 'deactivated');
    await createProject('o', { name: 'Пример проекта', key: 'PRM' });
    await getProject('o', 'p');
    await renameProject('o', 'p', 'Новое');
    await deactivateProject('o', 'p');
    await restoreProject('o', 'p');
    await getTeams('o', 'p');
    await getTeam('o', 'p', 't');
    await addTeam('o', 'p', 'Платформа');
    await renameTeam('o', 'p', 't', 'Ядро');
    await setDefaultTeam('o', 'p', 't');
    await setDefaultTeam('o', 'p', null);
    await getOwnedItemsCount('o', 'p', 't');
    await deleteTeam('o', 'p', 't');
    await getAreaTree('o', 'p');
    await createArea('o', 'p', { parentId: 'r', name: 'API' });
    await renameArea('o', 'p', 'a', 'Шлюз');
    await moveArea('o', 'p', 'a', 'r');
    await deleteArea('o', 'p', 'a');

    const base = '/api/organizations';
    expect(sent).toEqual([
      { method: 'GET', path: base, body: undefined },
      { method: 'GET', path: `${base}/deactivated`, body: undefined },
      { method: 'POST', path: base, body: { name: 'Альфа' } },
      { method: 'GET', path: `${base}/o`, body: undefined },
      { method: 'PUT', path: `${base}/o/name`, body: { name: 'Бета' } },
      { method: 'POST', path: `${base}/o/deactivate`, body: undefined },
      { method: 'POST', path: `${base}/o/restore`, body: undefined },
      { method: 'GET', path: `${base}/o/projects`, body: undefined },
      { method: 'GET', path: `${base}/o/projects/deactivated`, body: undefined },
      { method: 'POST', path: `${base}/o/projects`, body: { name: 'Пример проекта', key: 'PRM' } },
      { method: 'GET', path: `${base}/o/projects/p`, body: undefined },
      { method: 'PUT', path: `${base}/o/projects/p/name`, body: { name: 'Новое' } },
      { method: 'POST', path: `${base}/o/projects/p/deactivate`, body: undefined },
      { method: 'POST', path: `${base}/o/projects/p/restore`, body: undefined },
      { method: 'GET', path: `${base}/o/projects/p/teams`, body: undefined },
      { method: 'GET', path: `${base}/o/projects/p/teams/t`, body: undefined },
      { method: 'POST', path: `${base}/o/projects/p/teams`, body: { name: 'Платформа' } },
      { method: 'PUT', path: `${base}/o/projects/p/teams/t/name`, body: { name: 'Ядро' } },
      { method: 'PUT', path: `${base}/o/projects/p/default-team`, body: { teamId: 't' } },
      { method: 'PUT', path: `${base}/o/projects/p/default-team`, body: { teamId: null } },
      { method: 'GET', path: `${base}/o/projects/p/teams/t/owned-items/count`, body: undefined },
      { method: 'DELETE', path: `${base}/o/projects/p/teams/t`, body: undefined },
      { method: 'GET', path: `${base}/o/projects/p/areas`, body: undefined },
      {
        method: 'POST',
        path: `${base}/o/projects/p/areas`,
        body: { parentId: 'r', name: 'API' },
      },
      { method: 'PUT', path: `${base}/o/projects/p/areas/a/name`, body: { name: 'Шлюз' } },
      { method: 'PUT', path: `${base}/o/projects/p/areas/a/parent`, body: { parentId: 'r' } },
      { method: 'DELETE', path: `${base}/o/projects/p/areas/a`, body: undefined },
    ]);
  });

  it('answers a 204 with undefined', async () => {
    await expect(deactivateOrganization('o')).resolves.toBeUndefined();
    await expect(deleteTeam('o', 'p', 't')).resolves.toBeUndefined();
  });
});

describe('mod-002 api-contract § 0.2: ids are sent as received, re-encoded and never validated', () => {
  it('re-encodes `not-a-uuid`, `a/b` and a space in every path segment', async () => {
    await getOrganization('not-a-uuid');
    await getProject('a/b', 'p q');
    await getTeam('o', 'p', 't/1');
    await renameArea('o', 'p', 'a b', 'x');

    expect(sent.map((request) => request.path)).toEqual([
      '/api/organizations/not-a-uuid',
      '/api/organizations/a%2Fb/projects/p%20q',
      '/api/organizations/o/projects/p/teams/t%2F1',
      '/api/organizations/o/projects/p/areas/a%20b/name',
    ]);
  });

  it('turns a problem answer into an ApiError carrying its errorCode', async () => {
    server.use(
      http.get(`${API}/api/organizations/:id`, () =>
        HttpResponse.json(
          { status: 404, errorCode: 'organizations.organization.not_found' },
          { status: 404 },
        ),
      ),
    );

    const failure = await getOrganization('x').catch((error: unknown) => error);

    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).errorCode).toBe('organizations.organization.not_found');
  });
});
