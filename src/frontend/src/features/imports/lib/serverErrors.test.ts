import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { importErrorCodes } from '../../../shared/api/errorCodes';
import { problem } from '../../../test/api';
import { template } from '../testFixtures';
import { catalogText } from './catalogText';
import { draftFromTemplate, mappedRows } from './draft';
import { saveFailure } from './serverErrors';

function apiError(body: object, status: number): ApiError {
  return new ApiError(status, body as never);
}

describe('a refused save', () => {
  const draft = draftFromTemplate(template());

  it('puts a taken name on the name field', () => {
    const body = problem(importErrorCodes.templateNameTaken, { name: 'Выгрузка задач' });
    const failure = saveFailure(apiError(body, 409), draft);
    expect(failure.errors.name).toBe(body.detail);
    expect(failure.banner).toBeNull();
  });

  it('puts a mapping reason on its row and a source reason on the header row', () => {
    const row = mappedRows(draft)[0]!;
    const body = {
      ...problem(importErrorCodes.validationFailed),
      errors: {
        'settings.mappings[0].field': [{ code: 'imports.mapping.x', detail: 'Поле занято.' }],
        'settings.source.headerRow': [{ code: 'imports.source.x', detail: 'Неверная строка.' }],
      },
    };
    const failure = saveFailure(apiError(body, 400), draft);
    expect(failure.errors.mappings[row.id]).toBe('Поле занято.');
    expect(failure.errors.headerRow).toBe('Неверная строка.');
  });

  it('says the template limit in the banner', () => {
    const failure = saveFailure(
      apiError(problem(importErrorCodes.templateLimitReached), 409),
      draft,
    );
    expect(failure.limitReached).toBe(true);
    expect(failure.banner).toBeTruthy();
  });
});

describe('the catalogue text of a code', () => {
  it('reads the Russian text of a served code', () => {
    expect(catalogText('imports.dry_run.empty_file')).not.toBe('imports.dry_run.empty_file');
  });

  it('falls back to the code when it is unknown', () => {
    expect(catalogText('imports.nothing')).toBe('imports.nothing');
  });
});
