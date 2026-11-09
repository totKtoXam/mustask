import { type ReactNode } from 'react';
import Box from '@mui/material/Box';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import {
  InlineNotice,
  MoreActionsMenu,
  StateFrame,
  Surface,
  useNotice,
  type MenuEntry,
} from '../../../shared/ui';
import { ApiError } from '../../../shared/api/httpClient';
import { useProjectCodes } from '../../organizations';
import { getRows, type AggregateRow, type GroupHeader } from '../api/aggregateApi';
import { reorderProjectBacklog } from '../api/boardsApi';
import { useAnnouncement } from '../hooks/useAnnouncement';
import { useNarrowedTag } from '../hooks/useAggregateView';
import { useGroupRead, isProjectGone } from '../hooks/useGroupRead';
import { useRowDrag } from '../hooks/useRowDrag';
import type { AggregateViewState } from '../hooks/useAggregateView';
import { aggregateTexts, countText, streamCountText } from '../lib/aggregateTexts';
import { backlogCanMove, backlogHint } from '../lib/grouping';
import { dropBody, droppedRows, moveBody, moveItems, movedRows } from '../lib/aggregateMove';
import { groupIdOf } from '../lib/openGroups';
import { moveRefusalNotice } from '../lib/refusals';
import { MOVE_ENTRY_LABELS, type MoveEntry } from '../lib/reorder';
import { boardsPermissions } from '../permissions';
import { AggregateRows } from './AggregateRows';
import { RowDragCell } from './RowDragCell';
import { GroupBody, HeadersSkeleton } from './GroupBody';
import { StatusRegion } from './StatusRegion';
import { ViewGroup } from './ViewGroup';

const STALE_CODE = 'boards.backlog.reorder_stale';

/** One group of the backlog, or the stream of «Без группировки» when `header` is the one group. */
function BacklogGroupBody({
  v,
  header,
  stream,
  canMove,
  announce,
}: {
  v: AggregateViewState;
  header: GroupHeader;
  stream: boolean;
  canMove: boolean;
  announce(text: string): void;
}) {
  const { show } = useNotice();
  const id = groupIdOf(header);
  const read = useGroupRead<AggregateRow>({
    enabled: stream || v.isOpen(id),
    fetchPage: async (cursor) => {
      const page = await getRows(
        v.organizationId,
        'backlogs',
        v.level,
        { grouping: v.grouping, group: stream ? undefined : id, cursor },
        v.selectors,
      );
      return { items: page.rows, nextCursor: page.nextCursor };
    },
    onGone: () => {
      if (v.grouping === 'project') {
        v.markGone(id);
        show(aggregateTexts.projectGone);
      }
    },
    onCursorReload: v.readHeadersAgain,
  });

  /** Sends one reorder of the row at `index` and shows its result: the same for a menu item and a drop. */
  const send = async (
    index: number,
    body: NonNullable<ReturnType<typeof moveBody>>,
    next: (rows: readonly AggregateRow[]) => AggregateRow[],
  ) => {
    const rows = read.state.items;
    const partial = read.state.nextCursor !== null;
    const row = rows[index];
    if (!row) return;
    try {
      await reorderProjectBacklog(v.organizationId, row.project.id, v.level, body);
      if (body.position === 'bottom' && partial) {
        read.dropRow(row.id);
        show(aggregateTexts.movedToEnd);
        return;
      }
      const moved = next(rows);
      read.updateRows(() => moved);
      announce(
        `${row.identifier} перемещён: позиция ${moved.findIndex((r) => r.id === row.id) + 1} из ${moved.length}.`,
      );
    } catch (error) {
      if (error instanceof ApiError && error.errorCode === STALE_CODE) {
        read.reload();
      } else if (isProjectGone(error)) {
        v.markGone(id);
        show(aggregateTexts.projectGone);
        return;
      }
      const text = moveRefusalNotice(error);
      if (text) show(text, 'error');
    }
  };

  const move = async (index: number, entry: MoveEntry) => {
    const rows = read.state.items;
    const body = moveBody(rows, index, entry, read.state.nextCursor !== null);
    if (body) await send(index, body, (loaded) => movedRows(loaded, index, entry));
  };

  const partial = read.state.nextCursor !== null;
  const { drag, handlers } = useRowDrag({
    count: read.state.items.length,
    partial,
    onDrop: (from, gap) => {
      const body = dropBody(read.state.items, from, gap, partial);
      if (body) void send(from, body, (loaded) => droppedRows(loaded, from, gap));
    },
  });
  const wrapIdentifier = canMove
    ? (row: AggregateRow, index: number, content: ReactNode) => (
        <RowDragCell
          index={index}
          count={read.state.items.length}
          drag={drag}
          handlers={handlers(index)}
          label={row.identifier}
        >
          {content}
        </RowDragCell>
      )
    : undefined;

  const actions = canMove
    ? (_row: AggregateRow, index: number) => {
        const rows = read.state.items;
        const items = moveItems(index, rows.length, read.state.nextCursor !== null);
        if (rows.length < 2 && read.state.nextCursor === null) return null;
        const entries: MenuEntry[] = [
          {
            group: aggregateTexts.moveHeading,
            items: items.map((item) => ({
              label: item.needsMore
                ? `${MOVE_ENTRY_LABELS[item.entry]}. ${aggregateTexts.loadMoreFirst}`
                : MOVE_ENTRY_LABELS[item.entry],
              disabled: !item.enabled,
              onSelect: () => void move(index, item.entry),
            })),
          },
        ];
        return <MoreActionsMenu label={aggregateTexts.moveActions} items={entries} />;
      }
    : undefined;

  return (
    <GroupBody read={read} stream={stream} announce={announce}>
      {(rows) => (
        <AggregateRows
          organizationId={v.organizationId}
          label={stream ? 'Бэклог' : (header.name ?? aggregateTexts.noTeam)}
          rows={rows}
          hideProject={v.grouping === 'project'}
          actions={actions}
          wrapIdentifier={wrapIdentifier}
        />
      )}
    </GroupBody>
  );
}

function groupName(header: GroupHeader, grouping: AggregateViewState['grouping']): string {
  if (header.name) return header.name;
  return grouping === 'department' ? aggregateTexts.noDepartment : aggregateTexts.noTeam;
}

/** «Бэклог» at the organization, department and team level (BR-AV-6, design § 3, § 5). */
export function AggregateBacklogView({ v }: { v: AggregateViewState }) {
  // A group is one project's: the reorder is judged on that project's served codes (plan F-6, BR-SA-18).
  const { codes: projectCodes } = useProjectCodes(v.organizationId);
  const { announcement, announce } = useAnnouncement(v.grouping);
  const narrowedTag = useNarrowedTag(v.organizationId, v.selectors);
  const movable = backlogCanMove(v.grouping, v.selectors, false);
  const canMoveIn = (header: GroupHeader) =>
    movable &&
    (projectCodes?.get(groupIdOf(header))?.includes(boardsPermissions.projectBacklogReorder) ??
      false);
  const hint = backlogHint(v.grouping, v.selectors, false);
  const groups = v.groups;

  let body;
  if (v.headers.isPending || (v.headers.isError && v.headers.isFetching)) {
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
    body = (
      <StateFrame
        icon={InboxOutlinedIcon}
        heading={narrowedTag ? aggregateTexts.nothingFound : aggregateTexts.emptyBacklog}
      />
    );
  } else if (v.grouping === 'none') {
    const only = groups[0];
    body = only && (
      <Surface variant="card" padding="dense">
        <Box sx={{ mb: 1 }}>{streamCountText(only.count, only.total)}</Box>
        <BacklogGroupBody
          v={v}
          header={only}
          stream
          canMove={canMoveIn(only)}
          announce={announce}
        />
      </Surface>
    );
  } else {
    body = (
      <Box sx={{ display: 'grid', gap: 1 }}>
        {groups.map((header) => {
          const id = groupIdOf(header);
          return (
            <ViewGroup
              key={`${v.grouping}:${id}`}
              id={`group-${v.grouping}-${id}`}
              name={groupName(header, v.grouping)}
              tag={header.kind === 'project' ? header.key : undefined}
              count={countText(header.count, header.total)}
              open={v.isOpen(id)}
              onToggle={(expanded) => v.toggle(id, expanded)}
            >
              <BacklogGroupBody
                v={v}
                header={header}
                stream={false}
                canMove={canMoveIn(header)}
                announce={announce}
              />
            </ViewGroup>
          );
        })}
      </Box>
    );
  }

  return (
    <>
      {hint && (
        <Box sx={{ mb: 2 }}>
          <InlineNotice severity="info">{hint}</InlineNotice>
        </Box>
      )}
      {body}
      <StatusRegion text={announcement.text} serial={announcement.serial} />
    </>
  );
}
