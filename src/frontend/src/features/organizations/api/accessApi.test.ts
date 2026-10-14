import { describe, expect, it } from 'vitest';
import { HttpResponse, http } from 'msw';
import { ApiError } from '../../../shared/api/httpClient';
import { server } from '../../../test/mocks/server';
import { servedFor } from '../../../test/organizationRoles';
import { API, PROBLEM_HEADERS, problem } from '../testFixtures';
import { getAccess } from './accessApi';

const ORG = 'a0000000-0000-4000-8000-000000000001';

// api-contract § 1.1: `GET /api/organizations/{id}/access`.
describe('getAccess (mod-016 api-contract § 1.1)', () => {
  it('reads the role and the codes as served, and asks the organization’s own address', async () => {
    let requested = '';
    server.use(
      http.get(`${API}/api/organizations/:organizationId/access`, ({ request }) => {
        requested = new URL(request.url).pathname;
        return HttpResponse.json(servedFor('member'));
      }),
    );

    const answer = await getAccess(ORG);

    expect(requested).toBe(`/api/organizations/${ORG}/access`);
    expect(answer).toEqual({
      kind: 'member',
      role: 'member',
      permissions: servedFor('member').permissions,
    });
  });

  it('keeps the codes as given, in the served order, without adding or dropping any', async () => {
    server.use(
      http.get(`${API}/api/organizations/:organizationId/access`, () =>
        HttpResponse.json({ role: 'viewer', permissions: ['b.read', 'a.read', 'unknown.code'] }),
      ),
    );

    const answer = await getAccess(ORG);

    expect(answer).toEqual({
      kind: 'member',
      role: 'viewer',
      permissions: ['b.read', 'a.read', 'unknown.code'],
    });
  });

  it('answers «not a member» to the 404 «Организация не найдена» instead of throwing', async () => {
    server.use(
      http.get(`${API}/api/organizations/:organizationId/access`, () =>
        HttpResponse.json(problem('organizations.organization.not_found'), {
          status: 404,
          headers: PROBLEM_HEADERS,
        }),
      ),
    );

    await expect(getAccess(ORG)).resolves.toEqual({ kind: 'not-member' });
  });

  it.each([
    ['another 404', 404, 'organizations.project.not_found'],
    ['the platform’s 403', 403, 'http.forbidden'],
    ['a server error', 500, undefined],
    ['an unavailable service', 503, 'http.service_unavailable'],
  ])('throws %s: the tab keeps what it held', async (_name, status, errorCode) => {
    server.use(
      http.get(`${API}/api/organizations/:organizationId/access`, () =>
        HttpResponse.json({ status, errorCode }, { status, headers: PROBLEM_HEADERS }),
      ),
    );

    const error = await getAccess(ORG).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(status);
  });

  it('puts the organization’s id in the path as one segment', async () => {
    let requested = '';
    server.use(
      http.get(`${API}/api/organizations/:organizationId/access`, ({ request }) => {
        requested = new URL(request.url).pathname;
        return HttpResponse.json(servedFor('viewer'));
      }),
    );

    await getAccess('a b/c');

    expect(requested).toBe('/api/organizations/a%20b%2Fc/access');
  });
});
