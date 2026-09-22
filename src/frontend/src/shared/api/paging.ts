// ADR-0016: the single re-export
// point for `gridify-client`. A slice's api layer imports from here, never
// from the package directly — that is how a second registry stays honest to
// the same escaping and the same closed field whitelist without a review
// having to check it by hand every time.
//
// What the package buys for free (`GridifyQueryBuilder.addCondition`,
// `escapeValue` defaults to `true`): a value containing `,`, `|`, `(`, `)`
// or the literal `/i` is backslash-escaped before it reaches the `filter`
// expression, so it can no longer be read as Gridify syntax — the bug this
// re-adoption exists to close (a `,`/`|`/`(`/`)` typed into a filter box used
// to break the query and answer 400 instead of "Ничего не найдено"). The
// escape covers exactly those five sequences: a value that begins with an
// operator character (`>` measured) still reaches the parser as syntax and
// is rejected with a 400, and `\` is not escaped, so a trailing backslash is
// silently swallowed by the server.
//
// What it does not buy, and what `RegistryQueryBuilder` below adds back:
// `GridifyQueryBuilder`'s own `addCondition`/`addOrderBy` take a bare
// `string` field name — a typo there would type-check and only surface as a
// runtime `dictionaries.unknown_query_field` 400. `RegistryQueryBuilder<TField>`
// narrows every field-taking method to the slice's own sort/filter union, so
// an unlisted field is a compile error again, the same property phase 1
// bought with its closed per-slice sort-field unions (`LanguagesSortField`).
import {
  ConditionalOperator,
  GridifyQueryBuilder,
  type IGridifyQuery,
  type Paging,
} from 'gridify-client';

export { ConditionalOperator };
export type { IGridifyQuery, Paging };

/** One sort instruction: a field from the slice's own whitelist, plus direction. */
export interface OrderBy<TField extends string> {
  field: TField;
  desc?: boolean;
}

export interface RegistryQueryParams<TField extends string> {
  page: number;
  pageSize: number;
  orderBy?: OrderBy<TField>;
}

/**
 * `GridifyQueryBuilder`, narrowed to `TField`. Every method that takes a
 * field name is re-typed here; everything else (`and`/`or`'s optional-leading-
 * operator bookkeeping, `build()`) is a thin pass-through to the package.
 */
export class RegistryQueryBuilder<TField extends string> {
  private readonly inner: GridifyQueryBuilder;

  // Явное присваивание, не сокращённое parameter property — репозиторий
  // держит `erasableSyntaxOnly` (tsconfig.app.json), а сокращение генерирует
  // код, не стирающийся при простом снятии типов.
  private constructor(inner: GridifyQueryBuilder) {
    this.inner = inner;
  }

  /** @internal — construct through `createRegistryQueryBuilder`. */
  static wrap<TField extends string>(inner: GridifyQueryBuilder): RegistryQueryBuilder<TField> {
    return new RegistryQueryBuilder<TField>(inner);
  }

  /**
   * Chainable; call once before each condition. `optional: true` makes the
   * package swallow a leading operator with nothing before it, which is
   * exactly the "is this the first condition" bookkeeping a hand-rolled
   * `filters.join(',')` used to need — pass it every time and never think
   * about position again.
   */
  and(optional = false): this {
    this.inner.and(optional);
    return this;
  }

  or(optional = false): this {
    this.inner.or(optional);
    return this;
  }

  addCondition(field: TField, operator: ConditionalOperator, value: string | number | boolean): this {
    this.inner.addCondition(field, operator, value);
    return this;
  }

  build(): IGridifyQuery {
    return this.inner.build();
  }
}

/** A builder preloaded with `page`/`pageSize`/`orderBy` — the slice adds `filter` conditions itself, since it is the only one that knows its own fields (ADR-0016). */
export function createRegistryQueryBuilder<TField extends string>(
  params: RegistryQueryParams<TField>,
): RegistryQueryBuilder<TField> {
  const inner = new GridifyQueryBuilder().setPage(params.page).setPageSize(params.pageSize);
  if (params.orderBy) {
    inner.addOrderBy(params.orderBy.field, params.orderBy.desc);
  }
  return RegistryQueryBuilder.wrap<TField>(inner);
}

/** `build()`'s result → the endpoint's query string. Empty `orderBy`/`filter` are never written (a request must never carry an empty paging/query parameter). */
export function gridifyQueryToSearchParams(query: IGridifyQuery): string {
  const params = new URLSearchParams();
  params.set('page', String(query.page));
  params.set('pageSize', String(query.pageSize));
  if (query.orderBy) {
    params.set('orderBy', query.orderBy);
  }
  if (query.filter) {
    params.set('filter', query.filter);
  }
  return params.toString();
}
