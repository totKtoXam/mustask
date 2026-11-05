// Which groups are open (BR-NV-20): kept per user, organization, view, level and grouping in the
// browser, 10 at most; on the first visit only the first group is open (the one of no department
// or no team comes first, BR-AV-4).
import type { AggregateView, GroupHeader, Grouping } from '../api/aggregateApi';
import type { KeyValueStore } from './grouping';

export const MAX_OPEN_GROUPS = 10;

/** The id a group is remembered by: its id, or `none` for the group of no department / no team. */
export function groupIdOf(header: Pick<GroupHeader, 'id'>): string {
  return header.id ?? 'none';
}

export function openGroupsKey(
  subject: string,
  organizationId: string,
  view: AggregateView,
  level: string,
  grouping: Grouping,
): string {
  return `mustask.open-groups.v1.${subject}.${organizationId}.${view}.${level}.${grouping}`;
}

export function readOpenGroups(store: KeyValueStore | null, key: string): string[] | null {
  try {
    const raw = store?.getItem(key);
    if (raw === null || raw === undefined) return null;
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed)
      ? parsed.filter((id): id is string => typeof id === 'string').slice(0, MAX_OPEN_GROUPS)
      : null;
  } catch {
    return null;
  }
}

export function writeOpenGroups(store: KeyValueStore | null, key: string, ids: readonly string[]) {
  try {
    store?.setItem(key, JSON.stringify(ids.slice(-MAX_OPEN_GROUPS)));
  } catch {
    // Only the memory is lost.
  }
}

/**
 * The open groups for the headers just read: the remembered ones that still have a header (an
 * entry of an emptied group is ignored), else the first group on a first visit.
 */
export function initialOpen(
  remembered: readonly string[] | null,
  headers: readonly GroupHeader[],
): string[] {
  const present = headers.map(groupIdOf);
  if (remembered === null) return present.slice(0, 1);
  return remembered.filter((id) => present.includes(id));
}

/** Opening a group past the limit drops the oldest open one. */
export function withOpened(open: readonly string[], id: string): string[] {
  if (open.includes(id)) return [...open];
  return [...open, id].slice(-MAX_OPEN_GROUPS);
}
