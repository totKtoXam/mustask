// What every aggregate view shares (feat-048): the selectors of the address, the grouping in force
// (remembered per user, never in the address), the grouped count read for its headers and the
// open groups. A view page keys itself by organization, view, level and selectors, so another
// start is a fresh page.
import { useCallback, useMemo, useState } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../../auth/AuthContext';
import { useWorkContext } from '../../../shared/lib/workContext';
import {
  getGroups,
  type AggregateFilter,
  type AggregateSelectors,
  type AggregateView,
  type GroupHeader,
  type Grouping,
} from '../api/aggregateApi';
import {
  groupingsOffered,
  rememberGrouping,
  resolveGrouping,
  viewLevelOf,
  type KeyValueStore,
} from '../lib/grouping';
import type { BoardLevelCode } from '../lib/levels';
import {
  groupIdOf,
  initialOpen,
  openGroupsKey,
  readOpenGroups,
  withOpened,
  writeOpenGroups,
} from '../lib/openGroups';

function browserStore(): KeyValueStore | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

/** The ids the address names: the prefix's object, the rest in the query (ADR-0032 § 1). */
export function useAddressSelectors(): AggregateSelectors {
  const { areaId, teamId } = useParams<{ areaId: string; teamId: string }>();
  const [search] = useSearchParams();
  return useMemo(() => {
    const queryTeam = search
      .getAll('teamId')
      .find((value) => value !== '' && value.toLowerCase() !== 'none');
    const department = areaId ?? (search.get('departmentId') || undefined);
    const team = teamId ?? queryTeam;
    const selectors: AggregateSelectors = {};
    if (department) selectors.departmentId = department;
    if (team) selectors.teamId = team;
    return selectors;
  }, [areaId, teamId, search]);
}

/** «сужено: команда MARS, департамент Логистика» (feat-043 § 4), or none when nothing narrows. */
export function useNarrowedTag(organizationId: string, selectors: AggregateSelectors) {
  const context = useWorkContext();
  const offers = context.offers;
  const parts: string[] = [];
  const level = viewLevelOf(selectors);
  if (level === 'department' && selectors.teamId) {
    const team = offers?.teams.find((t) => t.id === selectors.teamId);
    parts.push(`команда ${team?.name ?? ''}`.trim());
  }
  if (level === 'team' && selectors.departmentId) {
    const department = offers?.departments.find((d) => d.id === selectors.departmentId);
    parts.push(`департамент ${department?.name ?? ''}`.trim());
  }
  void organizationId;
  return parts.length === 0 ? undefined : `сужено: ${parts.join(', ')}`;
}

export function useAggregateView({
  organizationId,
  view,
  level,
  selectors,
  filter,
}: {
  organizationId: string;
  view: AggregateView;
  level: BoardLevelCode;
  selectors: AggregateSelectors;
  filter?: AggregateFilter;
}) {
  const { subject = '' } = useAuth();
  const client = useQueryClient();
  const [serial, setSerial] = useState(0);
  const store = useMemo(() => browserStore(), []);
  const selectorKey = JSON.stringify(selectors);
  const filterKey = JSON.stringify(filter ?? {});
  const offered = useMemo(() => groupingsOffered(selectors), [selectors]);
  const grouping = useMemo(
    () => resolveGrouping(store, subject, organizationId, view, selectors),
    // `serial` is the memory having been written.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [store, subject, organizationId, view, selectorKey, serial],
  );
  const changeGrouping = useCallback(
    (next: Grouping) => {
      rememberGrouping(store, subject, organizationId, view, selectors, next);
      setSerial((n) => n + 1);
    },
    [store, subject, organizationId, view, selectors],
  );

  const headersKey = useMemo(
    () => [
      'boards',
      'aggregate-groups',
      organizationId,
      view,
      level,
      grouping,
      selectorKey,
      filterKey,
    ],
    [organizationId, view, level, grouping, selectorKey, filterKey],
  );
  const headers = useQuery({
    queryKey: headersKey,
    // The flat list of «Рабочие элементы» is one paged read with its own total.
    enabled: !(view === 'workitems' && grouping === 'none'),
    queryFn: () => getGroups(organizationId, view, level, grouping, selectors, filter),
  });
  const readHeadersAgain = useCallback(
    () => void client.invalidateQueries({ queryKey: headersKey }),
    [client, headersKey],
  );

  // The open groups: the memory of this grouping, else the first group (BR-NV-20), taken once per
  // set of headers' identity (a grouping or selector change starts afresh).
  const memoryKey = openGroupsKey(subject, organizationId, view, viewLevelOf(selectors), grouping);
  const groups: readonly GroupHeader[] | undefined = headers.data?.groups;
  const [open, setOpen] = useState<{ key: string; ids: string[] } | null>(null);
  if (groups !== undefined && grouping !== 'none' && (open === null || open.key !== memoryKey)) {
    setOpen({ key: memoryKey, ids: initialOpen(readOpenGroups(store, memoryKey), groups) });
  }
  const openIds = useMemo(
    () => (open !== null && open.key === memoryKey ? open.ids : []),
    [open, memoryKey],
  );
  const toggle = (id: string, expanded: boolean) => {
    const next = expanded
      ? withOpened(openIds, id)
      : openIds.filter((candidate) => candidate !== id);
    setOpen({ key: memoryKey, ids: next });
    writeOpenGroups(store, memoryKey, next);
  };
  const collapseAll = () => {
    setOpen({ key: memoryKey, ids: [] });
    writeOpenGroups(store, memoryKey, []);
  };

  // Groups whose project turned out to be gone (OQ-4 A 3): hidden until the headers are read again.
  const [gone, setGone] = useState<readonly string[]>([]);
  const markGone = useCallback(
    (id: string) => {
      setGone((ids) => (ids.includes(id) ? ids : [...ids, id]));
      readHeadersAgain();
    },
    [readHeadersAgain],
  );
  const shown = groups?.filter((group) => !gone.includes(groupIdOf(group)));

  return {
    organizationId,
    view,
    level,
    selectors,
    filter,
    grouping,
    offered,
    changeGrouping,
    headers,
    groups: shown,
    readHeadersAgain,
    isOpen: (id: string) => openIds.includes(id),
    toggle,
    collapseAll,
    markGone,
  };
}

export type AggregateViewState = ReturnType<typeof useAggregateView>;
