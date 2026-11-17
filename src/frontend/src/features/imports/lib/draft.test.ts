import { describe, expect, it } from 'vitest';
import type { FileRead } from '../api/importsApi';
import { catalogue, template } from '../testFixtures';
import {
  applyFileRead,
  draftFromTemplate,
  emptyDraft,
  hasErrors,
  identifierMapping,
  setIdentifierColumn,
  tabsWithErrors,
  toInput,
  validate,
} from './draft';

function fileRead(headers: { header: string; proposedField: string | null }[]): FileRead {
  return {
    format: 'xlsx',
    sheets: ['Лист1'],
    sheet: 'Лист1',
    headerRow: 1,
    firstDataRow: 2,
    encoding: null,
    delimiter: null,
    encodingDetected: false,
    delimiterDetected: false,
    dateSystem: '1900',
    dataRowCount: 3,
    columns: headers.map((column, index) => ({
      index,
      header: column.header,
      address: `${String.fromCharCode(65 + index)}1`,
      proposedField: column.proposedField,
    })),
    sampleRows: [],
  };
}

describe('the template draft', () => {
  it('proposes a field for a column that names one, once, on a template with no fields', () => {
    const draft = applyFileRead(
      emptyDraft(),
      fileRead([
        { header: 'Название', proposedField: 'title' },
        { header: 'Заголовок', proposedField: 'title' },
        { header: 'Заметки', proposedField: null },
      ]),
    );
    expect(draft.mappings.map((mapping) => mapping.field)).toEqual(['title', '', '']);
    expect(draft.source.headers).toEqual(['Название', 'Заголовок', 'Заметки']);
  });

  it('keeps the saved mappings when a new file is read and adds the new columns unmapped', () => {
    const saved = draftFromTemplate(template());
    const draft = applyFileRead(
      saved,
      fileRead([
        { header: 'Название', proposedField: 'title' },
        { header: 'Срок', proposedField: null },
      ]),
    );
    expect(draft.mappings.filter((mapping) => mapping.field !== '')).toHaveLength(1);
    expect(draft.mappings.map((mapping) => mapping.source.column)).toContain('Срок');
  });

  it('turns the unmapped rows into ignored columns and omits them from the mappings', () => {
    const input = toInput(draftFromTemplate(template()));
    expect(input.settings.mappings.map((mapping) => mapping.field)).toEqual(['title']);
    expect(input.settings.ignoredColumns).toEqual(['Комментарий']);
  });

  it('moves the identifier mapping with the chosen column and drops it on «Нет»', () => {
    const draft = draftFromTemplate(template());
    const chosen = setIdentifierColumn(draft, 'Комментарий');
    expect(identifierMapping(chosen)?.source.column).toBe('Комментарий');
    const cleared = setIdentifierColumn(chosen, null);
    expect(identifierMapping(cleared)).toBeUndefined();
  });

  it('refuses a draft with no name and no required field and counts the errors per tab', () => {
    const found = validate({ ...draftFromTemplate(template()), name: ' ' }, catalogue);
    expect(hasErrors(found)).toBe(true);
    expect(found.name).toBeTruthy();
    expect(tabsWithErrors(found).source).toBe(0);
  });

  it('accepts a complete draft', () => {
    expect(hasErrors(validate(draftFromTemplate(template()), catalogue))).toBe(false);
  });
});
