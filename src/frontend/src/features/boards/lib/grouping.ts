// The grouping switch's rules (feat-048 spec BR-AV-1..BR-AV-6; design § 2, § 5), pure: which
// groupings a level offers, the default, the browser memory per user, organization, view and
// level, and which hint and which moves a grouping gives. The grouping is never in the address.
import type { AggregateSelectors, AggregateView, Grouping } from '../api/aggregateApi';

export const GROUPING_LABELS: Readonly<Record<Grouping, string>> = {
  none: 'Без группировки',
  department: 'По департаменту',
  project: 'По проекту',
  team: 'По команде',
};

export const GROUPING_SWITCH_LABEL = 'Группировка';

/** The menu level of BR-NV-1: the most specific chosen object, else the organization. */
export type ViewLevel = 'organization' | 'department' | 'team' | 'project';

export function viewLevelOf(selectors: AggregateSelectors): ViewLevel {
  if (selectors.projectId) return 'project';
  if (selectors.teamId) return 'team';
  if (selectors.departmentId) return 'department';
  return 'organization';
}

/**
 * BR-AV-2: «Без группировки» always, and every dimension no selector fixes. A project fixes its
 * department as well; a single option means the control is absent (the caller checks).
 */
export function groupingsOffered(selectors: AggregateSelectors): readonly Grouping[] {
  const offered: Grouping[] = ['none'];
  if (!selectors.departmentId && !selectors.projectId) offered.push('department');
  if (!selectors.projectId) offered.push('project');
  if (!selectors.teamId) offered.push('team');
  return offered;
}

/** BR-AV-3: the default of a view at a level. */
export function defaultGrouping(view: AggregateView, selectors: AggregateSelectors): Grouping {
  if (view === 'workitems') return 'none';
  const level = viewLevelOf(selectors);
  if (level === 'organization' || level === 'department') return 'project';
  // Team level: by project only with a department chosen (BR-NV-17 clause 2, OQ-7 A).
  if (level === 'team' && selectors.departmentId) return 'project';
  return 'none';
}

const STORAGE_ROOT = 'mustask.grouping.v1';

export function groupingStorageKey(
  subject: string,
  organizationId: string,
  view: AggregateView,
  level: ViewLevel,
): string {
  return `${STORAGE_ROOT}.${subject}.${organizationId}.${view}.${level}`;
}

export interface KeyValueStore {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

function isGrouping(value: unknown): value is Grouping {
  return value === 'none' || value === 'department' || value === 'project' || value === 'team';
}

/**
 * The grouping in force: the remembered one if the level still offers it, else the default,
 * silently (BR-AV-3). An unreadable store is no memory.
 */
export function resolveGrouping(
  store: KeyValueStore | null,
  subject: string,
  organizationId: string,
  view: AggregateView,
  selectors: AggregateSelectors,
): Grouping {
  const offered = groupingsOffered(selectors);
  try {
    const raw = store?.getItem(
      groupingStorageKey(subject, organizationId, view, viewLevelOf(selectors)),
    );
    if (isGrouping(raw) && offered.includes(raw)) return raw;
  } catch {
    // A blocked store only costs the memory.
  }
  const fallback = defaultGrouping(view, selectors);
  return offered.includes(fallback) ? fallback : 'none';
}

export function rememberGrouping(
  store: KeyValueStore | null,
  subject: string,
  organizationId: string,
  view: AggregateView,
  selectors: AggregateSelectors,
  grouping: Grouping,
): void {
  try {
    store?.setItem(
      groupingStorageKey(subject, organizationId, view, viewLevelOf(selectors)),
      grouping,
    );
  } catch {
    // A full or blocked store only costs the memory.
  }
}

/** The announcement of a choice (design § 2). */
export function groupingAnnouncement(grouping: Grouping): string {
  return `Группировка: ${GROUPING_LABELS[grouping]}`;
}

export const groupingTexts = {
  narrowingHint: 'Пока выбор сужает список, порядок не меняется. Выберите «все», чтобы менять его.',
  changeTimeHint:
    'При этой группировке строки идут по времени изменения, порядок не меняется. Выберите «По проекту», чтобы менять его.',
} as const;

/**
 * The one hint under the page header (design § 5, D-1 A), or none. Only «Бэклог» has one: by
 * project it is the narrowing hint when a team or a department narrows a team's view; every
 * other grouping, and «Без группировки» at organization and department level, and by team at
 * project level, the change-time hint.
 */
export function backlogHint(
  grouping: Grouping,
  selectors: AggregateSelectors,
  filtered: boolean,
): string | null {
  const level = viewLevelOf(selectors);
  if (grouping === 'project') {
    const narrowed = Boolean(selectors.teamId) || filtered;
    return narrowed ? groupingTexts.narrowingHint : null;
  }
  if (grouping === 'none') {
    return level === 'organization' || level === 'department' ? groupingTexts.changeTimeHint : null;
  }
  return groupingTexts.changeTimeHint;
}

/** Rows of a backlog can be moved (BR-AV-6): by project and nothing narrowing. */
export function backlogCanMove(
  grouping: Grouping,
  selectors: AggregateSelectors,
  filtered: boolean,
): boolean {
  return grouping === 'project' && !selectors.teamId && !filtered;
}
