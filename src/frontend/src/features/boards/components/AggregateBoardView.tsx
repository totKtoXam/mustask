import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import Typography from '@mui/material/Typography';
import ViewWeekOutlinedIcon from '@mui/icons-material/ViewWeekOutlined';
import {
  InlineNotice,
  MoreActionsMenu,
  StateFrame,
  Surface,
  useNotice,
  type MenuEntry,
} from '../../../shared/ui';
import {
  AssigneeName,
  TypeName,
  teamTexts,
  useSetWorkItemStateIn,
  workItemPath,
  workItemsPermissions,
} from '../../workitems';
import { useProjectCodes } from '../../organizations';
import { getCells, type AggregateCard, type GroupHeader } from '../api/aggregateApi';
import { useGroupRead, isProjectGone, type UseGroupRead } from '../hooks/useGroupRead';
import { useNarrowedTag, type AggregateViewState } from '../hooks/useAggregateView';
import { useAnnouncement } from '../hooks/useAnnouncement';
import { columnsOf, type AggregateColumn } from '../lib/aggregateColumns';
import { aggregateTexts, countText, streamCountText } from '../lib/aggregateTexts';
import { groupIdOf } from '../lib/openGroups';
import { stateRefusalNotice } from '../lib/refusals';
import { CategoryMarker } from '../../../shared/ui/WorkTrackingMarks';
import { cardStripeSx, columnStripeSx } from './boardStripes';
import { GroupBody, HeadersSkeleton } from './GroupBody';
import { StatusRegion } from './StatusRegion';
import { ViewGroup } from './ViewGroup';

type CellRegistry = Map<string, UseGroupRead<AggregateCard>>;

/** The card being dragged inside one lane (or the whole board): a drop elsewhere is no target. */
interface Dragged {
  card: AggregateCard;
  from: AggregateColumn;
}

interface MoveCard {
  (card: AggregateCard, from: AggregateColumn, to: AggregateColumn): Promise<void>;
}

/** One cell (lane × column): its own read, 50 cards a page, «Показать ещё» (design § 3). */
function BoardCell({
  v,
  header,
  column,
  columns,
  count,
  enabled,
  stream,
  registry,
  canMove,
  onMove,
  dragged,
  onDragged,
  announce,
}: {
  v: AggregateViewState;
  header: GroupHeader;
  column: AggregateColumn;
  columns: readonly AggregateColumn[];
  count: number | undefined;
  enabled: boolean;
  stream: boolean;
  registry: CellRegistry;
  canMove: (card: AggregateCard) => boolean;
  onMove: MoveCard;
  dragged: Dragged | null;
  onDragged(next: Dragged | null): void;
  announce(text: string): void;
}) {
  const [over, setOver] = useState(false);
  const accepts =
    dragged !== null &&
    dragged.from.key !== column.key &&
    column.stateOfType.has(dragged.card.type.id);
  const group = groupIdOf(header);
  const read = useGroupRead<AggregateCard>({
    enabled,
    fetchPage: async (cursor) => {
      const page = await getCells(
        v.organizationId,
        v.level,
        { grouping: v.grouping, group: stream ? undefined : group, column: column.key, cursor },
        v.selectors,
      );
      return { items: page.cards, nextCursor: page.nextCursor };
    },
    onGone: () => v.grouping === 'project' && v.markGone(group),
    onCursorReload: v.readHeadersAgain,
  });
  useEffect(() => {
    registry.set(column.key, read);
    return () => {
      registry.delete(column.key);
    };
  });

  return (
    <Box
      role="group"
      aria-label={column.header}
      data-drop-target={over && accepts ? 'allowed' : undefined}
      onDragOver={(event) => {
        if (!accepts) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
        if (!over) setOver(true);
      }}
      onDragLeave={() => setOver(false)}
      onDrop={(event) => {
        setOver(false);
        if (!accepts || !dragged) return;
        event.preventDefault();
        const { card, from } = dragged;
        onDragged(null);
        void onMove(card, from, column);
      }}
      sx={(theme) => ({
        ...columnStripeSx(theme, column.categoryCode),
        width: theme.layout.boardColumnWidth,
        flexShrink: 0,
        display: 'grid',
        alignContent: 'start',
        gap: 1,
        outline: over && accepts ? `2px solid ${theme.palette.primary.main}` : 'none',
      })}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <CategoryMarker code={column.categoryCode} />
        <Typography variant="subtitle2" component="div">
          {column.header} {count !== undefined && <span>({count})</span>}
        </Typography>
      </Box>
      <GroupBody read={read} stream={stream} skeleton="cards" announce={announce}>
        {(cards) => (
          <Box sx={{ display: 'grid', gap: 1 }}>
            {cards.map((card) => {
              const targets = columns.filter(
                (other) => other.key !== column.key && other.stateOfType.has(card.type.id),
              );
              const entries: MenuEntry[] = [
                {
                  group: aggregateTexts.moveToColumn,
                  items: targets.map((target) => ({
                    label: target.header,
                    onSelect: () => void onMove(card, column, target),
                  })),
                },
              ];
              return (
                <Box
                  key={card.id}
                  draggable={canMove(card) && targets.length > 0}
                  data-drag-card={card.identifier}
                  sx={(theme) => ({
                    ...cardStripeSx(theme, card.type.appearance),
                    borderRadius: `${theme.shape.borderRadius}px`,
                  })}
                  onDragStart={(event) => {
                    event.dataTransfer.effectAllowed = 'move';
                    event.dataTransfer.setData('text/plain', card.identifier);
                    onDragged({ card, from: column });
                  }}
                  onDragEnd={() => onDragged(null)}
                >
                  <Surface variant="card" padding="dense" component="article">
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 1 }}>
                      <Link
                        component={RouterLink}
                        to={workItemPath(v.organizationId, card.project.id, card.identifier)}
                      >
                        {card.identifier}
                      </Link>
                      {canMove(card) && targets.length > 0 && (
                        <MoreActionsMenu label={aggregateTexts.moveActions} items={entries} />
                      )}
                    </Box>
                    <Typography variant="body2">{card.title}</Typography>
                    <TypeName type={card.type} />
                    <Box>
                      <AssigneeName assignee={card.assignee} />
                    </Box>
                    <Typography variant="caption" color="text.secondary">
                      {card.project.name} · {card.team?.name ?? teamTexts.noTeam}
                    </Typography>
                  </Surface>
                </Box>
              );
            })}
          </Box>
        )}
      </GroupBody>
    </Box>
  );
}

/** The columns of a lane (or of the whole board without lanes). */
function BoardColumns({
  v,
  header,
  columns,
  enabled,
  stream,
  canMove,
  announce,
}: {
  v: AggregateViewState;
  header: GroupHeader;
  columns: readonly AggregateColumn[];
  enabled: boolean;
  stream: boolean;
  canMove: (card: AggregateCard) => boolean;
  announce(text: string): void;
}) {
  const { show } = useNotice();
  const setState = useSetWorkItemStateIn(v.organizationId);
  const [registry] = useState<CellRegistry>(() => new Map());
  const [dragged, setDragged] = useState<Dragged | null>(null);

  const onMove: MoveCard = async (card, from, to) => {
    const stateId = to.stateOfType.get(card.type.id);
    if (!stateId) return;
    try {
      await setState.mutateAsync({
        projectId: card.project.id,
        identifier: card.identifier,
        stateId,
      });
    } catch (error) {
      if (isProjectGone(error) && v.grouping === 'project') {
        v.markGone(groupIdOf(header));
        show(aggregateTexts.projectGone);
        return;
      }
      const text = stateRefusalNotice(error);
      if (text) show(text, 'error');
      return;
    }
    registry.get(from.key)?.dropRow(card.id);
    // D-3 A: a card moved into a part-loaded cell re-reads that cell from its first page.
    registry.get(to.key)?.reload();
    v.readHeadersAgain();
    announce(`${card.identifier} перемещён в колонку «${to.header}».`);
  };

  const counts = useMemo(
    () => new Map((header.columns ?? []).map((c) => [c.key, c.count])),
    [header.columns],
  );
  return (
    <Box sx={{ display: 'flex', gap: 2, overflowX: 'auto', pb: 1 }}>
      {columns.map((column) => (
        <BoardCell
          key={column.key}
          v={v}
          header={header}
          column={column}
          columns={columns}
          count={counts.get(column.key)}
          enabled={enabled}
          stream={stream}
          registry={registry}
          canMove={canMove}
          onMove={onMove}
          dragged={dragged}
          onDragged={setDragged}
          announce={announce}
        />
      ))}
    </Box>
  );
}

function laneName(header: GroupHeader, grouping: AggregateViewState['grouping']): string {
  if (header.name) return header.name;
  return grouping === 'department' ? aggregateTexts.noDepartment : aggregateTexts.noTeam;
}

/** «Канбан» at the organization, department and team level (BR-AV-7, design § 3, § 5). */
export function AggregateBoardView({ v }: { v: AggregateViewState }) {
  // A card is moved by its own project's codes (plan F-6, D-4): the board of several projects serves them
  // once per project, on the project rows, and never per card.
  const { codes: projectCodes } = useProjectCodes(v.organizationId);
  const columns = useMemo(() => columnsOf(v.groups ?? []), [v.groups]);
  const { announcement, announce } = useAnnouncement(v.grouping);
  const narrowedTag = useNarrowedTag(v.organizationId, v.selectors);
  const canMove = useCallback(
    (card: AggregateCard) =>
      projectCodes?.get(card.project.id)?.includes(workItemsPermissions.workItemWrite) ?? false,
    [projectCodes],
  );
  const groups = v.groups;

  let body;
  if (v.headers.isPending) {
    body = <HeadersSkeleton />;
  } else if (v.headers.isError || groups === undefined) {
    body = (
      <InlineNotice
        severity="error"
        action={{
          label: aggregateTexts.retry,
          onClick: () => {
            void v.headers.refetch();
          },
        }}
      >
        {aggregateTexts.errorStream}
      </InlineNotice>
    );
  } else if (groups.length === 0) {
    body = (
      <StateFrame
        icon={ViewWeekOutlinedIcon}
        heading={narrowedTag ? aggregateTexts.nothingFound : aggregateTexts.emptyBoard}
      />
    );
  } else if (v.grouping === 'none') {
    const only = groups[0];
    body = only && (
      <>
        <Box sx={{ mb: 1 }}>{streamCountText(only.count, only.total)}</Box>
        <BoardColumns
          v={v}
          header={only}
          columns={columns}
          enabled
          stream
          canMove={canMove}
          announce={announce}
        />
      </>
    );
  } else {
    body = (
      <Box sx={{ display: 'grid', gap: 1 }}>
        {groups.map((header) => {
          const id = groupIdOf(header);
          return (
            <ViewGroup
              key={`${v.grouping}:${id}`}
              id={`lane-${v.grouping}-${id}`}
              name={laneName(header, v.grouping)}
              tag={header.kind === 'project' ? header.key : undefined}
              count={countText(header.count, header.total)}
              open={v.isOpen(id)}
              onToggle={(expanded) => v.toggle(id, expanded)}
            >
              <BoardColumns
                v={v}
                header={header}
                columns={columns}
                enabled={v.isOpen(id)}
                stream={false}
                canMove={canMove}
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
      {body}
      <StatusRegion text={announcement.text} serial={announcement.serial} />
    </>
  );
}
