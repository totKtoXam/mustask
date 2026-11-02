import { matchPath } from 'react-router-dom';
import {
  KIND_ROUTE_PREFIX,
  LANDING_LEVEL_SEGMENT,
  SELECTOR_QUERY,
  type MenuKind,
  type MenuRow,
} from './addressModel';
import {
  NO_SELECTION,
  SELECTOR_KINDS,
  primaryKind,
  type FitData,
  type ObjectSelection,
  type SelectorKind,
} from './selectorFit';

/**
 * The work context as the address and the browser hold it (feat-043: ADR-0032 §§ 1, 5, 6). The
 * most specific chosen object stands in the address prefix, the others ride in the query
 * (`teamId`, `departmentId`); the remembered selection lives in `localStorage` per user and
 * organization. Pure: storage is injected, nothing here reads `window`.
 */

/** What an address says of the selection. */
export interface AddressSelection {
  /** `undefined`: the address is not an organization's. */
  organizationId?: string;
  selection: ObjectSelection;
}

const PREFIX_KINDS: readonly SelectorKind[] = ['project', 'team', 'department'];

function idOf(pattern: string, pathname: string, param: string): string | null {
  const match = matchPath({ path: pattern, end: false }, pathname);
  const value = match?.params[param];
  if (value === undefined || value === '') return null;
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

/**
 * Reads the selection an address carries: the object its prefix names, the others from the
 * query. Matching is the router's own (case and percent-encoding, bug-003 and tech-009 F4); an
 * empty or repeated query value is ignored (the first one wins).
 */
export function readAddressSelection(pathname: string, search: string): AddressSelection {
  // The list of deactivated organizations is a page of the list, not of an organization.
  const isList = matchPath({ path: '/organizations/deactivated', end: true }, pathname) !== null;
  const organizationId = isList
    ? undefined
    : (idOf(`${KIND_ROUTE_PREFIX.organization}/*`, pathname, 'organizationId') ?? undefined);
  const selection: ObjectSelection = { ...NO_SELECTION };
  if (organizationId === undefined) return { selection };

  const params = new URLSearchParams(search);
  for (const kind of ['team', 'department'] as const) {
    const value = params.get(SELECTOR_QUERY[kind]);
    if (value) selection[kind] = value;
  }
  const idParams: Record<SelectorKind, string> = {
    project: 'projectId',
    team: 'teamId',
    department: 'areaId',
  };
  const prefixes: Record<SelectorKind, MenuKind> = {
    project: 'project',
    team: 'team',
    department: 'department',
  };
  let prefixed = false;
  for (const kind of PREFIX_KINDS) {
    const id = idOf(`${KIND_ROUTE_PREFIX[prefixes[kind]]}/*`, pathname, idParams[kind]);
    if (id !== null) {
      selection[kind] = id;
      prefixed = true;
      break;
    }
  }
  // On the organization's settings pages, which have no object prefix, the project rides in the
  // query too (ADR-0032 § 1: read only there); under a prefix the prefix is the most specific
  // object and no project is chosen, and on any other page `projectId` is that page's own filter.
  const isSettings =
    matchPath({ path: `${KIND_ROUTE_PREFIX.organization}/settings/*`, end: true }, pathname) !==
    null;
  if (!prefixed && isSettings) {
    const value = params.get(SELECTOR_QUERY.project);
    if (value) selection.project = value;
  }
  return { organizationId, selection };
}

/** The menu kind of a selection: its most specific object, else the organization. */
export function menuKindOf(selection: ObjectSelection): MenuKind {
  return primaryKind(selection) ?? 'organization';
}

/** The prefix path of the most specific object of a selection (no query). */
export function prefixPath(organizationId: string, selection: ObjectSelection): string {
  const org = `/organizations/${encodeURIComponent(organizationId)}`;
  const primary = primaryKind(selection);
  if (primary === null) return org;
  const id = encodeURIComponent(selection[primary] as string);
  switch (primary) {
    case 'project':
      return `${org}/projects/${id}`;
    case 'team':
      return `${org}/teams/${id}`;
    case 'department':
      return `${org}/areas/${id}`;
  }
}

/** The query the non-primary selectors ride in, `''` when there is none. */
export function selectionQuery(selection: ObjectSelection): string {
  const primary = primaryKind(selection);
  const params = new URLSearchParams();
  for (const kind of ['team', 'department'] as const) {
    const id = selection[kind];
    if (kind !== primary && id !== null) params.set(SELECTOR_QUERY[kind], id);
  }
  const text = params.toString();
  return text === '' ? '' : `?${text}`;
}

/**
 * The query of an address with no object prefix, an organization's settings page: every chosen
 * object rides in it, the project too, so the address names the whole selection (QA F-3).
 */
export function fullSelectionQuery(selection: ObjectSelection): string {
  const params = new URLSearchParams();
  for (const kind of ['team', 'department', 'project'] as const) {
    const id = selection[kind];
    if (id !== null) params.set(SELECTOR_QUERY[kind], id);
  }
  const text = params.toString();
  return text === '' ? '' : `?${text}`;
}

/** The address of a menu row under the selection: prefix, segment, then the query of the rest. */
export function menuHref(
  organizationId: string,
  selection: ObjectSelection,
  row: Pick<MenuRow, 'segment'>,
): string {
  const segment = row.segment.replace(':level', LANDING_LEVEL_SEGMENT);
  return `${prefixPath(organizationId, selection)}/${segment}${selectionQuery(selection)}`;
}

/**
 * The id the selection may name, matched without regard to case (tech-009 F4: an address may
 * write an id in capitals): the known id spelled as the server spells it, else `null`.
 */
export function resolveKnownId(candidate: string, known: readonly string[]): string | null {
  const lower = candidate.toLowerCase();
  return known.find((id) => id.toLowerCase() === lower) ?? null;
}

const STORAGE_ROOT = 'mustask.context.v2';

export function selectionStorageKey(subject: string, organizationId: string): string {
  return `${STORAGE_ROOT}.${subject}.${organizationId}`;
}

export function organizationStorageKey(subject: string): string {
  return `${STORAGE_ROOT}.${subject}.organization`;
}

/** The part of `Storage` this module needs. */
export interface KeyValueStore {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

function idOrNull(value: unknown): string | null {
  return typeof value === 'string' && value !== '' ? value : null;
}

/** The remembered selection of an organization; an unreadable or malformed value is absent. */
export function readRemembered(
  store: KeyValueStore,
  subject: string,
  organizationId: string,
): ObjectSelection | null {
  try {
    const raw = store.getItem(selectionStorageKey(subject, organizationId));
    if (raw === null) return null;
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) return null;
    const record = parsed as Record<string, unknown>;
    return {
      department: idOrNull(record.department),
      team: idOrNull(record.team),
      project: idOrNull(record.project),
    };
  } catch {
    return null;
  }
}

export function writeRemembered(
  store: KeyValueStore,
  subject: string,
  organizationId: string,
  selection: ObjectSelection,
): void {
  try {
    store.setItem(selectionStorageKey(subject, organizationId), JSON.stringify(selection));
    store.setItem(organizationStorageKey(subject), organizationId);
  } catch {
    // A full or blocked store only costs the memory, never the page.
  }
}

/**
 * «Все организации» (ADR-0032 § 5): clears all four. The last organization is stored empty, so
 * a page with no organization of its own shows none, and the organization's own selection is
 * emptied.
 */
export function clearRemembered(
  store: KeyValueStore,
  subject: string,
  organizationId: string | null,
): void {
  try {
    if (organizationId !== null) {
      store.setItem(
        selectionStorageKey(subject, organizationId),
        JSON.stringify({ department: null, team: null, project: null }),
      );
    }
    store.setItem(organizationStorageKey(subject), '');
  } catch {
    // A full or blocked store only costs the memory, never the page.
  }
}

/** Whether the last organization was cleared on purpose («Все организации»): stored, but empty. */
export function isOrganizationCleared(store: KeyValueStore, subject: string): boolean {
  try {
    return store.getItem(organizationStorageKey(subject)) === '';
  } catch {
    return false;
  }
}

/** The last organization the user chose or opened, or `null`. */
export function readRememberedOrganization(store: KeyValueStore, subject: string): string | null {
  try {
    return idOrNull(store.getItem(organizationStorageKey(subject)));
  } catch {
    return null;
  }
}

export interface FallbackResult {
  selection: ObjectSelection;
  /** The kinds dropped, with the names they had when known; empty: the notice is not shown. */
  dropped: readonly SelectorKind[];
}

/**
 * ADR-0032 § 6: each remembered object that is gone or no longer offered drops to «все».
 * An id is judged against the offers as the server spells it (case ignored); a kept id is
 * returned as spelled there. The pairs that no longer fit are not judged here: the fit of a
 * remembered pair is the selectors' business once they are shown.
 */
export function fallbackSelection(remembered: ObjectSelection, data: FitData): FallbackResult {
  const known: Record<SelectorKind, readonly string[]> = {
    department: data.departments.map(({ id }) => id),
    team: data.teams.map(({ id }) => id),
    project: data.projects.map(({ id }) => id),
  };
  const selection: ObjectSelection = { ...NO_SELECTION };
  const dropped: SelectorKind[] = [];
  for (const kind of SELECTOR_KINDS) {
    const id = remembered[kind];
    if (id === null) continue;
    const resolved = resolveKnownId(id, known[kind]);
    if (resolved === null) dropped.push(kind);
    else selection[kind] = resolved;
  }
  return { selection, dropped };
}

/**
 * «Ранее выбранный контекст недоступен. Выбрано: Ромашка.» (design § 8); without the name
 * when nothing is left.
 */
export function fallbackNotice(names: readonly string[]): string {
  const base = 'Ранее выбранный контекст недоступен.';
  return names.length === 0 ? base : `${base} Выбрано: ${names.join(' / ')}.`;
}
