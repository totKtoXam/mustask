// Маршруты справочника языков (`/api/dictionaries/languages*`). Типы —
// camelCase, как весь Mustask.Api. Ни один из этих запросов не читает
// заголовки ответа (`apiFetch` отдаёт только тело) и ни один не отправляет
// `Accept-Language` — оба ограничения закрыты структурно: здесь просто нет
// кода, который мог бы это сделать.
import { apiFetch } from '../../../shared/api/httpClient';
import {
  ConditionalOperator,
  createRegistryQueryBuilder,
  gridifyQueryToSearchParams,
  type OrderBy,
  type Paging,
} from '../../../shared/api/paging';

export type { OrderBy, Paging };

// ---------------------------------------------------------------------------
// Общие фрагменты
// ---------------------------------------------------------------------------

export interface AuditStamp {
  updatedAt: string;
  updatedBy: string;
}

// ---------------------------------------------------------------------------
// Языки
// ---------------------------------------------------------------------------

export interface LanguageResponse extends AuditStamp {
  code: string;
  endonym: string;
  isDefault: boolean;
  isActive: boolean;
  sortOrder: number;
  version: string;
}

export interface CreateLanguageInput {
  code: string;
  endonym?: string;
  sortOrder?: number;
}

export interface ReplaceLanguageInput {
  endonym: string;
  sortOrder: number;
  isActive: boolean;
}

// ---------------------------------------------------------------------------
// Построители Gridify-запросов (ADR-0016) — типизированы только над списком
// разрешённых полей через `RegistryQueryBuilder<TField>`
// (`shared/api/paging.ts`), чтобы имя вне белого списка не могло даже
// попасть в сигнатуру (закрывает `dictionaries.unknown_query_field`
// структурно, не проверкой в рантайме); `page`/`pageSize` — всегда конкретные
// значения (пустой `?pageSize=` есть 400, а не молчаливый дефолт); значения
// фильтров экранирует сам пакет (`addCondition`, `escapeValue` по умолчанию
// `true`) — `,`, `|`, `(`, `)` и буквальное `/i` в значении фильтра не ломают
// выражение.
// ---------------------------------------------------------------------------

const LANGUAGES_PAGE_DEFAULTS = { page: 1, pageSize: 10 } as const;

export type LanguagesSortField = 'code' | 'endonym' | 'isDefault' | 'isActive' | 'sortOrder';

export interface LanguagesQuery {
  page?: number;
  pageSize?: number;
  orderBy?: OrderBy<LanguagesSortField>;
  code?: string;
  endonym?: string;
  isDefault?: 'true' | 'false';
  isActive?: 'true' | 'false';
  sortOrder?: string;
}

/** Строит query-строку `GET /languages` строго по белому списку полей. */
export function buildLanguagesQuery(query: LanguagesQuery = {}): string {
  const builder = createRegistryQueryBuilder<LanguagesSortField>({
    page: query.page ?? LANGUAGES_PAGE_DEFAULTS.page,
    pageSize: query.pageSize ?? LANGUAGES_PAGE_DEFAULTS.pageSize,
    orderBy: query.orderBy,
  });

  if (query.code) builder.and(true).addCondition('code', ConditionalOperator.Contains, query.code);
  if (query.endonym) builder.and(true).addCondition('endonym', ConditionalOperator.Contains, query.endonym);
  if (query.isDefault) builder.and(true).addCondition('isDefault', ConditionalOperator.Equal, query.isDefault);
  if (query.isActive) builder.and(true).addCondition('isActive', ConditionalOperator.Equal, query.isActive);
  if (query.sortOrder) builder.and(true).addCondition('sortOrder', ConditionalOperator.Equal, query.sortOrder);

  return gridifyQueryToSearchParams(builder.build());
}

/**
 * Строит `filter=code=<code>` для точечного перечитывания одного языка (нет
 * `GET /languages/{code}`), через тот же `RegistryQueryBuilder`, что и
 * фильтры реестра. Строка, собранная вручную как `code==<code>`, Gridify не
 * принимает вовсе (`GridifyFilteringException: Invalid expression format
 * 'ValueExpression'`, подтверждено против реального парсера): равенство в
 * Gridify — это одиночный `=`, ровно то, что строит
 * `ConditionalOperator.Equal`.
 */
export function buildLanguageByCodeQuery(code: string): string {
  const builder = createRegistryQueryBuilder<LanguagesSortField>({ page: 1, pageSize: 1 });
  builder.and(true).addCondition('code', ConditionalOperator.Equal, code);
  return gridifyQueryToSearchParams(builder.build());
}

// ---------------------------------------------------------------------------
// Запросы
// ---------------------------------------------------------------------------

export function getLanguages(query?: LanguagesQuery): Promise<Paging<LanguageResponse>> {
  return apiFetch<Paging<LanguageResponse>>(`/api/dictionaries/languages?${buildLanguagesQuery(query)}`);
}

/** Одиночный язык по коду — нет отдельного маршрута, используется реестровый `GET` с фильтром. */
export async function getLanguageByCode(code: string): Promise<LanguageResponse | null> {
  const page = await apiFetch<Paging<LanguageResponse>>(
    `/api/dictionaries/languages?${buildLanguageByCodeQuery(code)}`,
  );
  return page.data[0] ?? null;
}

export function createLanguage(input: CreateLanguageInput): Promise<LanguageResponse> {
  // POST /languages не несёт If-Match (по контракту API).
  return apiFetch<LanguageResponse>('/api/dictionaries/languages', { method: 'POST', body: input });
}

export function replaceLanguage(
  code: string,
  input: ReplaceLanguageInput,
  ifMatch: string,
): Promise<LanguageResponse> {
  return apiFetch<LanguageResponse>(`/api/dictionaries/languages/${encodeURIComponent(code)}`, {
    method: 'PUT',
    body: input,
    headers: { 'If-Match': ifMatch },
  });
}

export function setDefaultLanguage(code: string): Promise<LanguageResponse> {
  // PUT /languages/default тоже не несёт If-Match (по контракту API).
  return apiFetch<LanguageResponse>('/api/dictionaries/languages/default', { method: 'PUT', body: { code } });
}
