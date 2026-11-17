// Made-up answers of the imports API for the slice's tests (api-contract of mod-024).
import { HttpResponse, http } from 'msw';
import { apiUrl } from '../../test/api';
import { server } from '../../test/mocks/server';
import { ids } from '../organizations/testFixtures';
import type {
  Catalogue,
  DryRun,
  DryRunRow,
  FileRead,
  TemplateDto,
  TemplateList,
  TemplateListItem,
  UploadedFile,
} from './api/importsApi';
import { heldSample } from './lib/heldSample';

export const ORG = ids.alfa;
export const TEMPLATE_ID = 'e0000000-0000-4000-8000-000000000001';
export const OTHER_TEMPLATE_ID = 'e0000000-0000-4000-8000-000000000002';
export const IMPORTS_URL = `/api/organizations/${ORG}/imports`;
export const BASE = `/organizations/${ORG}/settings/imports`;

export const catalogue: Catalogue = {
  limits: {
    maxFileBytes: 10_485_760,
    maxDataRows: 5000,
    maxColumns: 100,
    maxSheets: 20,
    maxHeaderRow: 50,
    maxTemplates: 100,
    maxChainLength: 10,
    maxRecodingEntries: 1000,
    maxPatternLength: 500,
    previewRows: 50,
    sampleRows: 5,
  },
  fields: [
    {
      id: 'title',
      name: 'Название',
      group: 'main',
      required: true,
      multiValued: false,
      recoding: 'none',
      allowsEmptyPolicy: false,
      supportsClear: false,
      typeKinds: null,
      valueKind: 'text',
    },
    {
      id: 'identifier',
      name: 'Идентификатор MUSTASK',
      group: 'main',
      required: false,
      multiValued: false,
      recoding: 'none',
      allowsEmptyPolicy: false,
      supportsClear: false,
      typeKinds: null,
      valueKind: 'identifier',
    },
  ],
  converters: [
    { id: 'trim', name: 'Обрезать пробелы', class: 'text', isSource: false, params: [] },
  ],
  dateFormats: [{ id: 'dd.MM.yyyy', name: 'дд.мм.гггг' }],
};

export function templateItem(overrides: Partial<TemplateListItem> = {}): TemplateListItem {
  return {
    id: TEMPLATE_ID,
    name: 'Выгрузка задач',
    version: 1,
    source: { format: 'xlsx', sheet: 'Лист1', encoding: null, delimiter: null },
    authorId: 'u1',
    authorName: 'Анна Пример',
    updatedAt: '2026-11-01T10:00:00Z',
    canEdit: true,
    canDelete: true,
    canCopy: true,
    canRun: true,
    ...overrides,
  };
}

export function templateList(
  items: TemplateListItem[],
  overrides: Partial<TemplateList> = {},
): TemplateList {
  return {
    items,
    total: items.length,
    page: 1,
    pageSize: 10,
    canCreate: true,
    canRun: true,
    ...overrides,
  };
}

export function template(overrides: Partial<TemplateDto> = {}): TemplateDto {
  return {
    id: TEMPLATE_ID,
    name: 'Выгрузка задач',
    version: 1,
    authorId: 'u1',
    authorName: 'Анна Пример',
    createdAt: '2026-11-01T10:00:00Z',
    updatedAt: '2026-11-01T10:00:00Z',
    canEdit: true,
    canDelete: true,
    canCopy: true,
    canRun: true,
    settings: {
      source: {
        format: 'xlsx',
        sheet: 'Лист1',
        headerRow: 1,
        firstDataRow: 2,
        encoding: null,
        delimiter: null,
        headers: ['Название', 'Комментарий'],
      },
      mappings: [
        {
          id: 'm0000000-0000-4000-8000-000000000001',
          field: 'title',
          source: { kind: 'column', column: 'Название' },
          chain: [],
          emptyCell: 'keep',
          fieldAbsent: 'error',
        },
      ],
      recoding: [],
      identifier: { emptyIdentifier: 'create' },
      ignoredColumns: ['Комментарий'],
    },
    ...overrides,
  };
}

export function dryRunRow(overrides: Partial<DryRunRow> = {}): DryRunRow {
  return {
    row: 2,
    result: 'created',
    item: { title: 'Первая задача', identifier: null },
    project: { id: 'p1', name: 'Проект Альфа' },
    fields: [{ field: 'title', name: 'Название', value: 'Первая задача', was: null }],
    reasons: [],
    ...overrides,
  };
}

export function dryRun(overrides: Partial<DryRun> = {}): DryRun {
  return {
    id: 'f0000000-0000-4000-8000-000000000001',
    templateId: TEMPLATE_ID,
    templateVersion: 1,
    stale: false,
    fileName: 'tasks.xlsx',
    createdAt: '2026-11-01T10:00:00Z',
    expiresAt: '2026-11-02T10:00:00Z',
    totals: { created: 1, updated: 0, unchanged: 0, skipped: 0, error: 1, all: 2 },
    notes: [],
    importable: true,
    result: 'all',
    rows: [
      dryRunRow(),
      dryRunRow({
        row: 3,
        result: 'error',
        item: null,
        fields: [],
        reasons: [
          {
            severity: 'error',
            code: 'x',
            column: 'Название',
            cell: 'A3',
            value: null,
            text: 'Название не заполнено.',
          },
        ],
      }),
    ],
    rowsShown: 2,
    ...overrides,
  };
}

export const FILE_ID = 'a0000000-0000-4000-8000-000000000001';

/** A made-up read of a sample workbook: two columns, two sheets, one data row. */
export function fileRead(overrides: Partial<FileRead> = {}): FileRead {
  return {
    format: 'xlsx',
    sheets: ['Лист1', 'Лист2'],
    sheet: 'Лист1',
    headerRow: 1,
    firstDataRow: 2,
    encoding: null,
    delimiter: null,
    encodingDetected: false,
    delimiterDetected: false,
    dateSystem: '1900',
    dataRowCount: 2,
    columns: [
      { index: 0, header: 'Название', address: 'A1', proposedField: 'title' },
      { index: 1, header: 'Статус', address: 'B1', proposedField: null },
    ],
    sampleRows: [
      {
        row: 2,
        cells: [
          { text: 'Первая задача', kind: 'text', address: 'A2' },
          { text: 'Новая', kind: 'text', address: 'B2' },
        ],
      },
    ],
    ...overrides,
  };
}

export function uploaded(read: FileRead = fileRead()): UploadedFile {
  return {
    fileId: FILE_ID,
    fileName: 'tasks.xlsx',
    size: 20480,
    expiresAt: '2026-11-14T10:00:00Z',
    read,
  };
}

/** Serves the upload and the re-read of the sample file with `read`. */
export function serveFile(read: FileRead = fileRead()) {
  server.use(
    http.post(apiUrl(`${IMPORTS_URL}/files`), () => HttpResponse.json(uploaded(read))),
    http.get(apiUrl(`${IMPORTS_URL}/files/:id/columns`), () => HttpResponse.json(read)),
  );
}

/** Serves the read side of the API the pages share: the catalogue, the list and one template. */
export function serveImports(
  options: {
    list?: TemplateList;
    template?: TemplateDto;
  } = {},
) {
  heldSample.clear(); // a test starts with no sample held by an earlier one
  server.use(
    http.get(apiUrl(`${IMPORTS_URL}/catalogue`), () => HttpResponse.json(catalogue)),
    http.get(apiUrl(`${IMPORTS_URL}/templates`), () =>
      HttpResponse.json(options.list ?? templateList([templateItem()])),
    ),
    http.get(apiUrl(`${IMPORTS_URL}/templates/:id`), () =>
      HttpResponse.json(options.template ?? template()),
    ),
  );
}
