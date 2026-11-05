import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { problem } from '../testFixtures';
import { deletionRefusal, isDeletedRefusal, isStateNotAllowed } from './deletionRefusals';

// mod-005 design § 8, the matrix of the three dialogs, cell by cell, by the `errorCode` alone.

function answered(code: string): ApiError {
  // The fixture's problem body without `params`, whose shape the panel's client types otherwise.
  const { params, ...body } = problem(code);
  void params;
  return new ApiError(body.status, body);
}

describe('deletionRefusal (design § 8)', () => {
  it.each([
    ['workitems.workitem.not_found', 'workItem'],
    ['organizations.project.not_found', 'project'],
    ['organizations.organization.not_found', 'organization'],
  ])('%s: the card gives way to the frame of %s', (code, level) => {
    expect(deletionRefusal(answered(code))).toEqual({ kind: 'gone', level });
  });

  it('409 `…deleted` and `…not_deleted`: stale, with the server’s own text', () => {
    expect(deletionRefusal(answered('workitems.workitem.deleted'))).toEqual({
      kind: 'stale',
      text: 'Рабочий элемент удалён и доступен только для чтения.',
    });
    expect(deletionRefusal(answered('workitems.workitem.not_deleted'))).toEqual({
      kind: 'stale',
      text: 'Рабочий элемент не удалён.',
    });
  });

  it('403 stays in the dialog with «Недостаточно прав»', () => {
    expect(deletionRefusal(answered('organizations.access.forbidden'))).toEqual({
      kind: 'stays',
      text: 'Недостаточно прав',
    });
  });

  it('no answer and a server error stay in the dialog', () => {
    expect(deletionRefusal(new TypeError('Failed to fetch')).kind).toBe('stays');
    expect(deletionRefusal(new ApiError(500, null))).toEqual({
      kind: 'stays',
      text: 'Ошибка API (HTTP 500).',
    });
  });
});

describe('the two single-code checks', () => {
  it('`…deleted` is the save’s refusal alone', () => {
    expect(isDeletedRefusal(answered('workitems.workitem.deleted'))).toBe(true);
    expect(isDeletedRefusal(answered('workitems.workitem.not_deleted'))).toBe(false);
    expect(isDeletedRefusal(new Error('x'))).toBe(false);
  });

  it('`state_not_allowed` is the restore field’s', () => {
    expect(isStateNotAllowed(answered('workitems.workitem.state_not_allowed'))).toBe(true);
    expect(isStateNotAllowed(answered('workitems.workitem.deleted'))).toBe(false);
  });
});
