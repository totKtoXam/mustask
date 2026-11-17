// The import module's requests, strictly per docs/features/mod-024-import-constructor/api-contract.md.
// camelCase, as all of Mustask.Api. Every route lives under `/api/organizations/{organizationId}/imports`.
import { apiFetch, apiFetchBlob } from '../../../shared/api/httpClient';

// ---------------------------------------------------------------------------
// Catalogue (§ 1)
// ---------------------------------------------------------------------------

export type FieldGroup = 'main' | 'typed';
export type RecodingKind = 'none' | 'project' | 'type' | 'state' | 'priority' | 'assignee' | 'team';
export type ValueKind = 'text' | 'integer' | 'decimal' | 'date' | 'reference' | 'identifier';

export interface CatalogueLimits {
  maxFileBytes: number;
  maxDataRows: number;
  maxColumns: number;
  maxSheets: number;
  maxHeaderRow: number;
  maxTemplates: number;
  maxChainLength: number;
  maxRecodingEntries: number;
  maxPatternLength: number;
  previewRows: number;
  sampleRows: number;
}

export interface CatalogueField {
  id: string;
  name: string;
  group: FieldGroup;
  required: boolean;
  multiValued: boolean;
  recoding: RecodingKind;
  allowsEmptyPolicy: boolean;
  supportsClear: boolean;
  typeKinds: string[] | null;
  valueKind: ValueKind;
}

export type ConverterId =
  | 'trim'
  | 'truncate'
  | 'number'
  | 'excelDate'
  | 'dateFormat'
  | 'isoDate'
  | 'split'
  | 'hyperlink'
  | 'lookup'
  | 'extract'
  | 'merge'
  | 'constant';

export interface CatalogueConverter {
  id: ConverterId;
  name: string;
  /** The catalogue's class: «text», «numbers», «dates», «lists», «links», «tables», «templates», «constants». */
  class: string;
  isSource: boolean;
  params: { name: string; kind: string; required: boolean }[];
}

export interface Catalogue {
  limits: CatalogueLimits;
  fields: CatalogueField[];
  converters: CatalogueConverter[];
  dateFormats: { id: string; name: string }[];
}

// ---------------------------------------------------------------------------
// Template model (§ 2.1)
// ---------------------------------------------------------------------------

export type SourceFormat = 'xlsx' | 'csv';

export interface TemplateSource {
  format: SourceFormat;
  sheet: string | null;
  headerRow: number;
  firstDataRow: number;
  encoding: string | null;
  delimiter: string | null;
  headers: string[];
}

export type MappingSourceKind = 'column' | 'constant' | 'merge';

export interface MappingSource {
  kind: MappingSourceKind;
  column?: string | null;
  value?: string | null;
  pattern?: string | null;
}

export interface ChainStep {
  converter: ConverterId;
  params: Record<string, unknown>;
}

export type EmptyCellPolicy = 'keep' | 'clear';
export type FieldAbsentPolicy = 'error' | 'drop';

export interface Mapping {
  id: string;
  field: string;
  source: MappingSource;
  chain: ChainStep[];
  emptyCell: EmptyCellPolicy;
  fieldAbsent: FieldAbsentPolicy;
}

export type RecodingPolicy = 'error' | 'default' | 'empty';

export interface RecodingEntry {
  fileValue: string;
  typeId: string | null;
  target: string;
  onExport: boolean;
}

export interface Recoding {
  mappingId: string;
  policy: RecodingPolicy;
  defaultValue: string | null;
  entries: RecodingEntry[];
}

export type EmptyIdentifier = 'create' | 'skip';

export interface TemplateSettings {
  source: TemplateSource;
  mappings: Mapping[];
  recoding: Recoding[];
  identifier: { emptyIdentifier: EmptyIdentifier };
  ignoredColumns: string[];
}

export interface TemplateDto {
  id: string;
  name: string;
  version: number;
  authorId: string;
  authorName: string | null;
  createdAt: string;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
  canCopy: boolean;
  canRun: boolean;
  settings: TemplateSettings;
}

export interface TemplateInput {
  name: string;
  settings: TemplateSettings;
}

// ---------------------------------------------------------------------------
// Template list (§ 2.2)
// ---------------------------------------------------------------------------

export type TemplatesSort = 'name' | '-name' | 'author' | '-author' | 'updatedAt' | '-updatedAt';

export interface TemplatesQuery {
  name?: string;
  author?: 'all' | 'mine';
  sort?: TemplatesSort;
  page?: number;
  pageSize?: number;
}

export interface TemplateListItem {
  id: string;
  name: string;
  version: number;
  source: {
    format: SourceFormat;
    sheet: string | null;
    encoding: string | null;
    delimiter: string | null;
  };
  authorId: string;
  authorName: string | null;
  updatedAt: string;
  canEdit: boolean;
  canDelete: boolean;
  canCopy: boolean;
  canRun: boolean;
}

export interface TemplateList {
  items: TemplateListItem[];
  total: number;
  page: number;
  pageSize: number;
  canCreate: boolean;
  canRun: boolean;
}

export interface TemplateColumns {
  id: string;
  name: string;
  columns: string[];
}

// ---------------------------------------------------------------------------
// Uploaded files (§ 3)
// ---------------------------------------------------------------------------

export interface FileSourceOptions {
  sheet?: string | null;
  headerRow?: number;
  firstDataRow?: number;
  encoding?: string | null;
  delimiter?: string | null;
}

export type CellKind = 'text' | 'number' | 'date' | 'boolean' | 'empty';

export interface FileColumn {
  index: number;
  header: string;
  address: string;
  proposedField: string | null;
}

export interface SampleRow {
  row: number;
  cells: { text: string; kind: CellKind; address: string }[];
}

export interface FileRead {
  format: SourceFormat;
  sheets: string[];
  sheet: string | null;
  headerRow: number;
  firstDataRow: number;
  encoding: string | null;
  delimiter: string | null;
  encodingDetected: boolean;
  delimiterDetected: boolean;
  dateSystem: string | null;
  dataRowCount: number;
  columns: FileColumn[];
  sampleRows: SampleRow[];
}

export interface UploadedFile {
  fileId: string;
  fileName: string;
  size: number;
  expiresAt: string;
  read: FileRead;
}

export interface FileValues {
  values: { value: string; rows: number }[];
  distinctCount: number;
  exceeded: boolean;
}

export interface ChainSampleRow {
  row: number;
  original: string;
  steps: { converter: string; value: string | null; error: string | null }[];
  result: string | null;
  error: { code: string; params?: Record<string, unknown> } | null;
}

export interface ChainSample {
  rows: ChainSampleRow[];
}

// ---------------------------------------------------------------------------
// Dry run (§ 4)
// ---------------------------------------------------------------------------

export type RowResult = 'created' | 'updated' | 'unchanged' | 'skipped' | 'error';
export type ResultFilter = 'all' | RowResult;

export interface DryRunReason {
  severity: 'error' | 'warning';
  code: string;
  params?: Record<string, unknown>;
  column: string | null;
  cell: string | null;
  value: string | null;
  text: string;
}

export interface DryRunRow {
  row: number;
  result: RowResult;
  item: { title: string | null; identifier: string | null } | null;
  project: { id: string; name: string } | null;
  fields: { field: string; name: string; value: string | null; was: string | null }[];
  reasons: DryRunReason[];
}

export interface DryRunTotals {
  created: number;
  updated: number;
  unchanged: number;
  skipped: number;
  error: number;
  all: number;
}

export interface DryRun {
  id: string;
  templateId: string;
  templateVersion: number;
  stale: boolean;
  fileName: string;
  createdAt: string;
  expiresAt: string;
  totals: DryRunTotals;
  /** Codes of the error catalogue (`imports.dry_run.*`): their text is the catalogue's. */
  notes: string[];
  importable: boolean;
  result: ResultFilter;
  rows: DryRunRow[];
  rowsShown: number;
}

// ---------------------------------------------------------------------------
// Requests
// ---------------------------------------------------------------------------

const base = (organizationId: string) =>
  `/api/organizations/${encodeURIComponent(organizationId)}/imports`;

const id = encodeURIComponent;

export function getCatalogue(organizationId: string): Promise<Catalogue> {
  return apiFetch<Catalogue>(`${base(organizationId)}/catalogue`);
}

export function buildTemplatesQuery(query: TemplatesQuery = {}): string {
  const params = new URLSearchParams();
  if (query.name) params.set('name', query.name);
  if (query.author) params.set('author', query.author);
  params.set('sort', query.sort ?? 'name');
  params.set('page', String(query.page ?? 1));
  params.set('pageSize', String(query.pageSize ?? 10));
  return params.toString();
}

export function getTemplates(
  organizationId: string,
  query?: TemplatesQuery,
): Promise<TemplateList> {
  return apiFetch<TemplateList>(`${base(organizationId)}/templates?${buildTemplatesQuery(query)}`);
}

export function getTemplate(organizationId: string, templateId: string): Promise<TemplateDto> {
  return apiFetch<TemplateDto>(`${base(organizationId)}/templates/${id(templateId)}`);
}

export function getTemplateColumns(
  organizationId: string,
  templateId: string,
): Promise<TemplateColumns> {
  return apiFetch<TemplateColumns>(`${base(organizationId)}/templates/${id(templateId)}/columns`);
}

export function createTemplate(organizationId: string, input: TemplateInput): Promise<TemplateDto> {
  return apiFetch<TemplateDto>(`${base(organizationId)}/templates`, {
    method: 'POST',
    body: input,
  });
}

export function replaceTemplate(
  organizationId: string,
  templateId: string,
  input: TemplateInput,
): Promise<TemplateDto> {
  return apiFetch<TemplateDto>(`${base(organizationId)}/templates/${id(templateId)}`, {
    method: 'PUT',
    body: input,
  });
}

export function copyTemplate(organizationId: string, templateId: string): Promise<TemplateDto> {
  return apiFetch<TemplateDto>(`${base(organizationId)}/templates/${id(templateId)}/copy`, {
    method: 'POST',
  });
}

export function deleteTemplate(organizationId: string, templateId: string): Promise<void> {
  return apiFetch<void>(`${base(organizationId)}/templates/${id(templateId)}`, {
    method: 'DELETE',
  });
}

function optionsForm(options: FileSourceOptions): [string, string][] {
  const fields: [string, string][] = [];
  if (options.sheet) fields.push(['sheet', options.sheet]);
  if (options.headerRow !== undefined) fields.push(['headerRow', String(options.headerRow)]);
  if (options.firstDataRow !== undefined)
    fields.push(['firstDataRow', String(options.firstDataRow)]);
  if (options.encoding) fields.push(['encoding', options.encoding]);
  if (options.delimiter) fields.push(['delimiter', options.delimiter]);
  return fields;
}

export function uploadFile(
  organizationId: string,
  file: File,
  options: FileSourceOptions = {},
): Promise<UploadedFile> {
  const form = new FormData();
  form.append('file', file);
  for (const [name, value] of optionsForm(options)) form.append(name, value);
  return apiFetch<UploadedFile>(`${base(organizationId)}/files`, { method: 'POST', body: form });
}

export function readFileColumns(
  organizationId: string,
  fileId: string,
  options: FileSourceOptions = {},
): Promise<FileRead> {
  const params = new URLSearchParams(optionsForm(options));
  const query = params.toString();
  return apiFetch<FileRead>(
    `${base(organizationId)}/files/${id(fileId)}/columns${query ? `?${query}` : ''}`,
  );
}

export interface MappingProbe {
  source: FileSourceOptions;
  mapping: Mapping;
}

export function readFileValues(
  organizationId: string,
  fileId: string,
  probe: MappingProbe,
): Promise<FileValues> {
  return apiFetch<FileValues>(`${base(organizationId)}/files/${id(fileId)}/values`, {
    method: 'POST',
    body: probe,
  });
}

export function readChainSample(
  organizationId: string,
  fileId: string,
  probe: MappingProbe,
): Promise<ChainSample> {
  return apiFetch<ChainSample>(`${base(organizationId)}/files/${id(fileId)}/chain-sample`, {
    method: 'POST',
    body: probe,
  });
}

export function startDryRun(
  organizationId: string,
  templateId: string,
  fileId: string,
  signal?: AbortSignal,
): Promise<DryRun> {
  return apiFetch<DryRun>(`${base(organizationId)}/templates/${id(templateId)}/dry-runs`, {
    method: 'POST',
    body: { fileId },
    signal,
  });
}

export function getDryRun(
  organizationId: string,
  dryRunId: string,
  result: ResultFilter = 'all',
): Promise<DryRun> {
  return apiFetch<DryRun>(
    `${base(organizationId)}/dry-runs/${id(dryRunId)}?${new URLSearchParams({ result }).toString()}`,
  );
}

export function downloadDryRunReport(
  organizationId: string,
  dryRunId: string,
): Promise<{ blob: Blob; fileName: string | null }> {
  return apiFetchBlob(`${base(organizationId)}/dry-runs/${id(dryRunId)}/report`);
}
