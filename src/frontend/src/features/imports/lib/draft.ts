// The template draft the editor holds (design § 3): the template's settings plus the rows of the
// mapping table that transfer nothing («Не переносить»: `field === ''`). Pure functions, no render.
import type {
  Catalogue,
  ChainStep,
  FileRead,
  Mapping,
  MappingSource,
  Recoding,
  RecodingEntry,
  TemplateDto,
  TemplateInput,
  TemplateSource,
} from '../api/importsApi';
import { texts } from './texts';

/** A mapping row of the draft: `field` is `''` for a column that is not transferred. */
export interface DraftMapping extends Omit<Mapping, 'field'> {
  field: string;
}

export interface Draft {
  name: string;
  source: TemplateSource;
  mappings: DraftMapping[];
  recoding: Recoding[];
  emptyIdentifier: 'create' | 'skip';
}

/** The identifier column's header the export writes (BR-IE-10). */
const IDENTIFIER_HEADER = 'идентификатор';

/** A fresh id for a mapping: the stand is served over plain HTTP, where `crypto.randomUUID` is absent. */
export function newId(): string {
  const bytes = new Uint8Array(16);
  if (typeof crypto !== 'undefined' && typeof crypto.getRandomValues === 'function') {
    crypto.getRandomValues(bytes);
  } else {
    for (let i = 0; i < bytes.length; i += 1) bytes[i] = Math.floor(Math.random() * 256);
  }
  bytes[6] = ((bytes[6] ?? 0) & 0x0f) | 0x40;
  bytes[8] = ((bytes[8] ?? 0) & 0x3f) | 0x80;
  const hex = [...bytes].map((byte) => byte.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

export const EMPTY_SOURCE: TemplateSource = {
  format: 'xlsx',
  sheet: null,
  headerRow: 1,
  firstDataRow: 2,
  encoding: null,
  delimiter: null,
  headers: [],
};

export function emptyDraft(): Draft {
  return { name: '', source: EMPTY_SOURCE, mappings: [], recoding: [], emptyIdentifier: 'create' };
}

export function columnMapping(column: string, field = ''): DraftMapping {
  return {
    id: newId(),
    field,
    source: { kind: 'column', column },
    chain: [],
    emptyCell: 'keep',
    fieldAbsent: 'error',
  };
}

export function blankMapping(): DraftMapping {
  return { ...columnMapping(''), source: { kind: 'column', column: null } };
}

const sameHeader = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();

/** The columns no mapping row names yet, as «Не переносить» rows (the saved template's unlisted headers). */
function withUnmappedRows(
  mappings: DraftMapping[],
  headers: readonly string[],
  ignored: readonly string[],
): DraftMapping[] {
  const used = (header: string) =>
    mappings.some(
      (mapping) =>
        (mapping.source.kind === 'column' &&
          mapping.source.column !== null &&
          mapping.source.column !== undefined &&
          sameHeader(mapping.source.column, header)) ||
        false,
    );
  const rows = [...mappings];
  for (const header of headers) {
    if (used(header)) continue;
    // An unlisted «Идентификатор» that was not left out on purpose is the identifier column (BR-IE-10).
    const implicitIdentifier =
      sameHeader(header, IDENTIFIER_HEADER) &&
      !ignored.some((name) => sameHeader(name, header)) &&
      !rows.some((mapping) => mapping.field === 'identifier');
    rows.push(columnMapping(header, implicitIdentifier ? 'identifier' : ''));
  }
  return rows;
}

export function draftFromTemplate(template: TemplateDto): Draft {
  const { settings } = template;
  const mappings: DraftMapping[] = settings.mappings.map((mapping) => ({ ...mapping }));
  return {
    name: template.name,
    source: settings.source,
    mappings: withUnmappedRows(mappings, settings.source.headers, settings.ignoredColumns),
    recoding: settings.recoding.map((entry) => ({ ...entry, entries: [...entry.entries] })),
    emptyIdentifier: settings.identifier.emptyIdentifier,
  };
}

/**
 * A file read of the sample (a new upload, or a re-read with other options) becomes the draft's source.
 * A template with no mapped field yet gets BR-IE-4's proposals (a column whose header names a field);
 * a template that already has mappings keeps them and only gains the new columns as rows.
 */
export function applyFileRead(draft: Draft, read: FileRead): Draft {
  const headers = read.columns.map((column) => column.header);
  const source: TemplateSource = {
    format: read.format,
    sheet: read.sheet,
    headerRow: read.headerRow,
    firstDataRow: read.firstDataRow,
    encoding: read.encoding,
    delimiter: read.delimiter,
    headers,
  };
  const hasFields = draft.mappings.some((mapping) => mapping.field !== '');
  if (!hasFields) {
    const taken = new Set<string>();
    const mappings = read.columns.map((column) => {
      const field =
        column.proposedField && !taken.has(column.proposedField) ? column.proposedField : '';
      if (field) taken.add(field);
      return columnMapping(column.header, field);
    });
    return { ...draft, source, mappings, recoding: [] };
  }
  const kept = draft.mappings.filter((mapping) => mapping.field !== '');
  return { ...draft, source, mappings: withUnmappedRows(kept, headers, []) };
}

/** The mapping that feeds the identifier, if any (BR-IE-10): the model's one source of truth. */
export function identifierMapping(draft: Draft): DraftMapping | undefined {
  return draft.mappings.find((mapping) => mapping.field === 'identifier');
}

/** Chooses the identifier column (`null`: «Нет»): the mapping to «Идентификатор MUSTASK» is created, moved or dropped. */
export function setIdentifierColumn(draft: Draft, column: string | null): Draft {
  const without = draft.mappings.filter((mapping) => mapping.field !== 'identifier');
  const released = draft.mappings
    .filter((mapping) => mapping.field === 'identifier')
    .map((mapping) => ({ ...mapping, field: '' }));
  if (column === null) {
    // the column the identifier mapping released stays an unmapped row: left out on purpose
    return { ...draft, mappings: [...without, ...released] };
  }
  const existing = without.find(
    (mapping) =>
      mapping.field === '' &&
      mapping.source.kind === 'column' &&
      mapping.source.column !== null &&
      mapping.source.column !== undefined &&
      sameHeader(mapping.source.column, column),
  );
  if (existing) {
    return {
      ...draft,
      mappings: [
        ...without.filter((mapping) => mapping !== existing),
        ...released,
        { ...existing, field: 'identifier' },
      ],
    };
  }
  return { ...draft, mappings: [...without, ...released, columnMapping(column, 'identifier')] };
}

/** The columns the mapping reads: the one it names, or the `{name}`s of a merge pattern. */
export function columnsOf(source: MappingSource): string[] {
  if (source.kind === 'column') return source.column ? [source.column] : [];
  if (source.kind === 'merge') {
    return [...(source.pattern ?? '').matchAll(/\{([^{}]+)\}/g)].map((match) => match[1] ?? '');
  }
  return [];
}

/** The headers a mapping names that the file does not have (the saved file's, or the sample's). */
export function missingColumns(source: MappingSource, headers: readonly string[]): string[] {
  return columnsOf(source).filter((name) => !headers.some((header) => sameHeader(header, name)));
}

export function recodingOf(draft: Draft, mappingId: string): Recoding | undefined {
  return draft.recoding.find((entry) => entry.mappingId === mappingId);
}

export function emptyRecoding(mappingId: string): Recoding {
  return { mappingId, policy: 'error', defaultValue: null, entries: [] };
}

export function upsertRecoding(draft: Draft, recoding: Recoding): Draft {
  const rest = draft.recoding.filter((entry) => entry.mappingId !== recoding.mappingId);
  return { ...draft, recoding: [...rest, recoding] };
}

export function updateMapping(draft: Draft, id: string, change: Partial<DraftMapping>): Draft {
  return {
    ...draft,
    mappings: draft.mappings.map((mapping) =>
      mapping.id === id ? { ...mapping, ...change } : mapping,
    ),
  };
}

export function removeMapping(draft: Draft, id: string): Draft {
  return {
    ...draft,
    mappings: draft.mappings.filter((mapping) => mapping.id !== id),
    recoding: draft.recoding.filter((entry) => entry.mappingId !== id),
  };
}

export function setEntries(recoding: Recoding, entries: RecodingEntry[]): Recoding {
  return { ...recoding, entries };
}

/** The mapped rows of the draft (those that transfer a field). */
export function mappedRows(draft: Draft): DraftMapping[] {
  return draft.mappings.filter((mapping) => mapping.field !== '');
}

/** The body of `POST/PUT /templates` (api-contract § 2.5). */
export function toInput(draft: Draft): TemplateInput {
  const mappings: Mapping[] = mappedRows(draft).map((mapping) => ({
    ...mapping,
    field: mapping.field,
    chain: mapping.chain.map((step) => ({ ...step })),
  }));
  const ids = new Set(mappings.map((mapping) => mapping.id));
  const ignored = draft.mappings
    .filter(
      (mapping) =>
        mapping.field === '' && mapping.source.kind === 'column' && mapping.source.column,
    )
    .map((mapping) => mapping.source.column as string);
  return {
    name: draft.name,
    settings: {
      source: draft.source,
      mappings,
      // a table row with no target yet (a half-added type entry) says nothing: it is not stored
      recoding: draft.recoding
        .filter((entry) => ids.has(entry.mappingId))
        .map((entry) => ({ ...entry, entries: entry.entries.filter((row) => row.target !== '') })),
      identifier: { emptyIdentifier: draft.emptyIdentifier },
      ignoredColumns: ignored,
    },
  };
}

/** A tab of the editor (design § 3). */
export type EditorTab = 'source' | 'mapping' | 'values' | 'identifier';

export interface DraftErrors {
  name?: string;
  headerRow?: string;
  firstDataRow?: string;
  /** Error of a mapping row, by mapping id. */
  mappings: Record<string, string>;
  /** A required field with no source (shown above the table). */
  missingRequired: string[];
  /** Error of a recoding, by mapping id. */
  recoding: Record<string, string>;
}

const NAME_MAX = 100;
const HEADER_ROW_MAX = 50;

/** The client-side checks of design § 3 (the server repeats them and stays the authority). */
export function validate(draft: Draft, catalogue: Catalogue | undefined): DraftErrors {
  const errors: DraftErrors = { mappings: {}, missingRequired: [], recoding: {} };
  const name = draft.name.trim();
  if (name === '') errors.name = texts.nameRequired;
  else if (name.length > NAME_MAX) errors.name = texts.nameTooLong;
  const { headerRow, firstDataRow } = draft.source;
  if (!Number.isInteger(headerRow) || headerRow < 1 || headerRow > HEADER_ROW_MAX) {
    errors.headerRow = texts.headerRowInvalid;
  }
  if (!Number.isInteger(firstDataRow) || firstDataRow <= headerRow) {
    errors.firstDataRow = texts.firstDataRowInvalid;
  }
  if (!catalogue) return errors;

  const fieldOf = (id: string) => catalogue.fields.find((field) => field.id === id);
  for (const mapping of mappedRows(draft)) {
    const field = fieldOf(mapping.field);
    const emptySource =
      (mapping.source.kind === 'column' && !mapping.source.column) ||
      (mapping.source.kind === 'constant' && !mapping.source.value) ||
      (mapping.source.kind === 'merge' && !mapping.source.pattern);
    if (emptySource) {
      errors.mappings[mapping.id] = texts.sourceRequired;
      continue;
    }
    if (field && !field.multiValued && mapping.chain.some((step) => step.converter === 'split')) {
      errors.mappings[mapping.id] = texts.listIntoSingle(field.name);
      continue;
    }
    const unknown =
      mapping.source.kind === 'merge' ? missingColumns(mapping.source, draft.source.headers) : [];
    const first = unknown[0];
    if (first !== undefined) errors.mappings[mapping.id] = texts.columnUnknown(first);
  }
  for (const field of catalogue.fields.filter((candidate) => candidate.required)) {
    if (!draft.mappings.some((mapping) => mapping.field === field.id)) {
      errors.missingRequired.push(field.id);
    }
  }
  for (const recoding of draft.recoding) {
    const mapping = draft.mappings.find((candidate) => candidate.id === recoding.mappingId);
    if (mapping && recoding.policy === 'default' && !recoding.defaultValue) {
      errors.recoding[mapping.id] = texts.chooseValue;
    }
  }
  return errors;
}

/** The tab an error belongs to; the first one with an error opens after a failed save. */
export function tabsWithErrors(errors: DraftErrors): Record<EditorTab, number> {
  return {
    source: (errors.headerRow ? 1 : 0) + (errors.firstDataRow ? 1 : 0),
    mapping: Object.keys(errors.mappings).length + errors.missingRequired.length,
    values: Object.keys(errors.recoding).length,
    identifier: 0,
  };
}

export function hasErrors(errors: DraftErrors): boolean {
  return (
    errors.name !== undefined || Object.values(tabsWithErrors(errors)).some((count) => count > 0)
  );
}

/** A chain step with its parameters at their defaults (design § 3.2). */
export function newStep(converter: ChainStep['converter']): ChainStep {
  switch (converter) {
    case 'trim':
      return { converter, params: { collapse: false } };
    case 'truncate':
      return { converter, params: { length: 100 } };
    case 'excelDate':
      return { converter, params: { system: '1900' } };
    case 'dateFormat':
      return { converter, params: { format: 'dmy_dot' } };
    case 'split':
      return { converter, params: { delimiter: ';' } };
    case 'lookup':
      return { converter, params: { entries: [], policy: 'error', defaultValue: null } };
    case 'extract':
      return { converter, params: { pattern: '' } };
    default:
      return { converter, params: {} };
  }
}
