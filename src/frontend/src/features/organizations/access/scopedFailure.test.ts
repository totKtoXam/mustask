import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { scopedFailureOf } from './scopedFailure';

const NOT_FOUND = { errorCode: 'organizations.organization.not_found' };
const FORBIDDEN = { errorCode: 'organizations.access.forbidden' };
const ORG = 'a0000000-0000-4000-8000-000000000001';

describe('scopedFailureOf (mod-016 plan P-4)', () => {
  it('reads the organization of a 403 under it', () => {
    expect(
      scopedFailureOf(new ApiError(403, FORBIDDEN, `/api/organizations/${ORG}/members`)),
    ).toEqual({
      organizationId: ORG,
      status: 403,
      organizationNotFound: false,
    });
  });

  it('knows the 404 «Организация не найдена» from another 404', () => {
    expect(
      scopedFailureOf(new ApiError(404, NOT_FOUND, `/api/organizations/${ORG}/projects`))
        ?.organizationNotFound,
    ).toBe(true);
    expect(
      scopedFailureOf(
        new ApiError(
          404,
          { errorCode: 'organizations.project.not_found' },
          `/api/organizations/${ORG}/projects/p1`,
        ),
      ),
    ).toEqual({ organizationId: ORG, status: 404, organizationNotFound: false });
  });

  it('reads the organization itself and a path with a query string', () => {
    expect(
      scopedFailureOf(new ApiError(404, NOT_FOUND, `/api/organizations/${ORG}`))?.organizationId,
    ).toBe(ORG);
    expect(
      scopedFailureOf(
        new ApiError(403, FORBIDDEN, `/api/organizations/${ORG}/member-candidates?text=ан`),
      )?.organizationId,
    ).toBe(ORG);
  });

  it('decodes an id that arrives encoded', () => {
    expect(
      scopedFailureOf(new ApiError(403, FORBIDDEN, '/api/organizations/a%20b/members'))
        ?.organizationId,
    ).toBe('a b');
  });

  it.each([
    ['the list', '/api/organizations'],
    ['the list with a query', '/api/organizations?x=1'],
    ['the deactivated list', '/api/organizations/deactivated'],
    ['another prefix', '/api/dictionaries/languages'],
    ['the access read itself', `/api/organizations/${ORG}/access`],
    ['the access read with a query', `/api/organizations/${ORG}/access?x=1`],
  ])('is nothing for %s', (_name, path) => {
    expect(scopedFailureOf(new ApiError(404, NOT_FOUND, path))).toBeNull();
    expect(scopedFailureOf(new ApiError(403, FORBIDDEN, path))).toBeNull();
  });

  it('is nothing for another status, an error with no path or one that is not an ApiError', () => {
    const path = `/api/organizations/${ORG}/members`;
    expect(scopedFailureOf(new ApiError(500, null, path))).toBeNull();
    expect(scopedFailureOf(new ApiError(401, null, path))).toBeNull();
    expect(scopedFailureOf(new ApiError(403, FORBIDDEN))).toBeNull();
    expect(scopedFailureOf(new Error('boom'))).toBeNull();
    expect(scopedFailureOf(undefined)).toBeNull();
  });

  it('does not take a malformed encoding for an organization', () => {
    expect(
      scopedFailureOf(new ApiError(403, FORBIDDEN, '/api/organizations/%E0%A4%A/members')),
    ).toBeNull();
  });
});
