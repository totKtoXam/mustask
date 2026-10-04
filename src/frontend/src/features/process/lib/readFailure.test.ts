import { describe, expect, it } from 'vitest';
import { organizationErrorCodes } from '../../../shared/api/errorCodes';
import { ApiError } from '../../../shared/api/httpClient';
import { UnreadableProcessDefinitionError } from '../api/processApi';
import { isOrganizationNotFound } from './readFailure';

describe('isOrganizationNotFound — the read branches on errorCode only (api-contract § 1.3)', () => {
  it('is true for an ApiError carrying organizations.organization.not_found', () => {
    expect(
      isOrganizationNotFound(
        new ApiError(404, { status: 404, errorCode: organizationErrorCodes.notFound }),
      ),
    ).toBe(true);
  });

  it.each([
    ['a 404 with another code', new ApiError(404, { status: 404, errorCode: 'http.not_found' })],
    ['a 404 with no body', new ApiError(404, null)],
    [
      'a 404 whose text alone says «Организация не найдена.»',
      new ApiError(404, { status: 404, detail: 'Организация не найдена.' }),
    ],
    ['a 500', new ApiError(500, { status: 500, errorCode: 'http.internal_error' })],
    ['a 403', new ApiError(403, null)],
    ['a network failure', new TypeError('Failed to fetch')],
    ['an answer with no types', new UnreadableProcessDefinitionError('no work item types')],
    ['an answer missing a part', new UnreadableProcessDefinitionError('a state has no category')],
    ['a value that is not an error', 'organizations.organization.not_found'],
  ])('is false for %s', (_case, error) => {
    expect(isOrganizationNotFound(error)).toBe(false);
  });
});
