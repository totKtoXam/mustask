import { describe, expect, it } from 'vitest';
import { iterationErrorCodes } from '../../../shared/api/errorCodes';
import { ApiError } from '../../../shared/api/httpClient';
import { problem } from '../../../test/api';
import { placeSprintRefusal } from './refusals';

const refused = (code: string, status?: number) => {
  const body = problem(code, {}, status);
  return new ApiError(body.status, body);
};

describe('mod-007 design § 5.1: where a sprint refusal stands', () => {
  it.each([
    [iterationErrorCodes.nameTaken, 'name'],
    [iterationErrorCodes.datesUnpaired, 'dates'],
    [iterationErrorCodes.datesReversed, 'dates'],
    [iterationErrorCodes.closeDestinationInvalid, 'destination'],
    [iterationErrorCodes.activeExists, null],
  ])('places %s under %s', (code, field) => {
    const placed = placeSprintRefusal(refused(code));
    expect(placed.field).toBe(field);
    expect(placed.text).toBe(problem(code).detail);
  });

  it('names the sprint and the team that are gone, and a lost code', () => {
    expect(placeSprintRefusal(refused(iterationErrorCodes.notFound)).sprintGone).toBe(true);
    expect(placeSprintRefusal(refused('organizations.team.not_found')).teamGone).toBe(true);
    expect(placeSprintRefusal(refused('organizations.access.forbidden')).forbidden).toBe(true);
  });

  it('puts a 400 on the field its errors name, with the field reason text', () => {
    const body = {
      ...problem(iterationErrorCodes.validationFailed),
      errors: { name: [{ code: 'x', detail: 'Название занято.' }] },
    };
    const placed = placeSprintRefusal(new ApiError(400, body as never));
    expect(placed).toMatchObject({ field: 'name', text: 'Название занято.' });
  });

  it('is plain for an error that is not the API’s', () => {
    expect(placeSprintRefusal(new Error('boom'))).toMatchObject({ field: null, forbidden: false });
  });
});
