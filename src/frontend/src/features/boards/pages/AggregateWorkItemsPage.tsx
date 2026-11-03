import { useState } from 'react';
import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { DataTable, FilterBar, InlineNotice } from '../../../shared/ui';
import { StateFrame } from '../../../shared/ui/StateFrame';
import {
  AssigneeFilter,
  KeywordFilter,
  StateFilter,
  WORK_ITEMS_PAGE_SIZE,
  filterContent,
  isFiltered,
  useOrganizationWorkItems,
  useWorkItemFilter,
  type OrganizationWorkItem,
  type WorkItemListFilter,
} from '../../workitems';
import { getRows, type AggregateRow, type GroupHeader } from '../api/aggregateApi';
import { AggregateHeader } from '../components/AggregateHeader';
import { AggregateRows } from '../components/AggregateRows';
import { rowColumns } from '../components/rowColumns';
import { GroupBody, HeadersSkeleton } from '../components/GroupBody';
import { StatusRegion } from '../components/StatusRegion';
import { ViewGroup } from '../components/ViewGroup';
import { useGroupRead } from '../hooks/useGroupRead';
import {
  useAddressSelectors,
  useAggregateView,
  useNarrowedTag,
  type AggregateViewState,
} from '../hooks/useAggregateView';
import { aggregateTexts, countText } from '../lib/aggregateTexts';
import { groupIdOf } from '../lib/openGroups';

const TITLE = 'Рабочие элементы';
const CLEAR_FILTERS = 'Сбросить фильтры';

/** One group of the grouped list: 50 rows a page by the cursor, no pager, no sorting (BR-AV-8). */
function GroupRows({
  v,
  header,
  announce,
}: {
  v: AggregateViewState;
  header: GroupHeader;
  announce(text: string): void;
}) {
  const id = groupIdOf(header);
  const read = useGroupRead<AggregateRow>({
    enabled: v.isOpen(id),
    fetchPage: async (cursor) => {
      const page = await getRows(
        v.organizationId,
        'workitems',
        v.level,
        { grouping: v.grouping, group: id, cursor },
        v.selectors,
        v.filter,
      );
      return { items: page.rows, nextCursor: page.nextCursor };
    },
    onGone: () => v.grouping === 'project' && v.markGone(id),
    onCursorReload: v.readHeadersAgain,
  });
  return (
    <GroupBody read={read} stream={false} announce={announce}>
      {(rows) => (
        <AggregateRows
          organizationId={v.organizationId}
          label={header.name ?? TITLE}
          rows={rows}
          hideProject={v.grouping === 'project'}
          withChanged
        />
      )}
    </GroupBody>
  );
}

/** «Без группировки»: the page-enveloped list with its pager and total (BR-NV-12). */
function FlatList({
  organizationId,
  selectors,
  filter,
  filtered,
  onClear,
}: {
  organizationId: string;
  selectors: ReturnType<typeof useAddressSelectors>;
  filter: WorkItemListFilter;
  filtered: boolean;
  onClear(): void;
}) {
  const [page, setPage] = useState(0);
  const query = useOrganizationWorkItems(organizationId, page + 1, selectors, filter);
  const rows: readonly OrganizationWorkItem[] = query.data?.data ?? [];
  const empty = filtered
    ? {
        icon: FilterListOffIcon,
        heading: aggregateTexts.nothingFound,
        action: { label: CLEAR_FILTERS, icon: <FilterListOffIcon />, onClick: onClear },
      }
    : { icon: InboxOutlinedIcon, heading: aggregateTexts.emptyList };
  return (
    <DataTable
      aria-label={TITLE}
      loading={query.isPending}
      error={query.isError && rows.length === 0 ? aggregateTexts.errorStream : undefined}
      onRetry={() => void query.refetch()}
      empty={empty}
      columns={rowColumns({ organizationId, rows, withChanged: true })}
      rows={rows}
      rowKey={(row) => row.id}
      pager={{
        page,
        rowsPerPage: WORK_ITEMS_PAGE_SIZE,
        count: query.data?.count ?? 0,
        onPageChange: setPage,
      }}
    />
  );
}

/**
 * «Рабочие элементы» at the organization, department and team level (feat-048 US-6, US-11): the
 * flat list with pager by default, or the same rows in groups of one grouping, each read by its
 * cursor. The filters are feat-003's, held in the address; the grouping is not.
 */
export function AggregateWorkItemsPage() {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const selectors = useAddressSelectors();
  const [filter, setFilter] = useWorkItemFilter();
  const listFilter = { state: filter.state, assigneeId: filter.assigneeId, search: filter.search };
  const v = useAggregateView({
    organizationId,
    view: 'workitems',
    level: 'stories',
    selectors,
    filter: listFilter,
  });
  const narrowed = useNarrowedTag(organizationId, selectors);
  const filtered = isFiltered(filter);
  const [announcement, setAnnouncement] = useState({ text: '', serial: 0 });
  const announce = (text: string) =>
    setAnnouncement(({ serial }) => ({ text, serial: serial + 1 }));
  const clear = () => setFilter({ state: [], assigneeId: [], search: '', teamId: [] });
  const groups = v.groups;
  const grouped = v.grouping !== 'none';

  let body;
  if (!grouped) {
    body = (
      <FlatList
        key={`${filterContent(filter)}|${JSON.stringify(selectors)}`}
        organizationId={organizationId}
        selectors={selectors}
        filter={filter}
        filtered={filtered}
        onClear={clear}
      />
    );
  } else if (v.headers.isPending) {
    body = <HeadersSkeleton />;
  } else if (v.headers.isError || groups === undefined) {
    body = (
      <InlineNotice
        severity="error"
        action={{ label: aggregateTexts.retry, onClick: () => void v.headers.refetch() }}
      >
        {aggregateTexts.errorStream}
      </InlineNotice>
    );
  } else if (groups.length === 0) {
    body = filtered ? (
      <StateFrame
        icon={FilterListOffIcon}
        heading={aggregateTexts.nothingFound}
        action={{ label: CLEAR_FILTERS, icon: <FilterListOffIcon />, onClick: clear }}
      />
    ) : (
      <StateFrame icon={InboxOutlinedIcon} heading={aggregateTexts.emptyList} />
    );
  } else {
    body = (
      <Box sx={{ display: 'grid', gap: 1 }}>
        {groups.map((header) => {
          const id = groupIdOf(header);
          const name =
            header.name ??
            (v.grouping === 'department' ? aggregateTexts.noDepartment : aggregateTexts.noTeam);
          return (
            <ViewGroup
              key={`${v.grouping}:${id}`}
              id={`items-${v.grouping}-${id}`}
              name={name}
              tag={header.kind === 'project' ? header.key : undefined}
              count={countText(header.count, header.total)}
              open={v.isOpen(id)}
              onToggle={(expanded) => v.toggle(id, expanded)}
            >
              <GroupRows v={v} header={header} announce={announce} />
            </ViewGroup>
          );
        })}
      </Box>
    );
  }

  return (
    <>
      <AggregateHeader
        title={TITLE}
        narrowed={narrowed}
        offered={v.offered}
        grouping={v.grouping}
        onGrouping={v.changeGrouping}
        onCollapseAll={grouped && (groups?.length ?? 0) > 0 ? v.collapseAll : undefined}
      />
      <FilterBar active={filtered} onClear={clear}>
        <KeywordFilter value={filter.search} onChange={(search) => setFilter({ search })} />
        <StateFilter
          organizationId={organizationId}
          value={filter.state}
          onChange={(state) => setFilter({ state })}
        />
        <AssigneeFilter
          organizationId={organizationId}
          value={filter.assigneeId}
          states={filter.state}
          onChange={(assigneeId) => setFilter({ assigneeId })}
        />
      </FilterBar>
      {body}
      <StatusRegion text={announcement.text} serial={announcement.serial} />
    </>
  );
}
