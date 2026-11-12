import { useState } from 'react';
import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import EventNoteOutlinedIcon from '@mui/icons-material/EventNoteOutlined';
import FlagOutlinedIcon from '@mui/icons-material/FlagOutlined';
import PlayArrowOutlinedIcon from '@mui/icons-material/PlayArrowOutlined';
import {
  ActionButton,
  DataTable,
  InlineNotice,
  MoreActionsMenu,
  PageHeader,
  TruncatedText,
  type DataColumn,
  type MenuEntry,
} from '../../../shared/ui';
import { NotFound } from '../../../shared/ui/StateFrame';
import { notFoundLevel } from '../../organizations';
import type { SprintRow } from '../api/iterationsApi';
import { SprintDialogs, type SprintDialog } from '../components/SprintDialogs';
import { SprintStateTag } from '../components/SprintStateTag';
import { useSprintList } from '../hooks/queries';
import { useSprintAccess } from '../hooks/useSprintAccess';
import { placeSprintRefusal } from '../lib/refusals';
import { sprintTexts } from '../lib/sprintTexts';
import { formatDate } from '../../../shared/lib/format';
import { sprintOutcomePath } from '../paths';

const t = sprintTexts.list;

/** The row's figures: a Closed sprint reads closed of held at the level «Истории» (design § 3.2, D-4 A, D-8 A). */
function figure(row: SprintRow, held: number, closed: number | null): string {
  return row.state === 'Closed' && closed !== null ? t.closedOf(closed, held) : String(held);
}

/**
 * The team's «Спринты» (design § 3, § 4; US-1..US-3): the list, highest number first, 200 a page
 * with «Показать ещё»; the commands of a manager (create, the row menu) are absent — not disabled —
 * for anyone else. The states are the canon's: loading, the team not found, a first read that failed,
 * a re-read that failed above the kept table, a failed «Показать ещё», empty, content.
 */
export function TeamSprintsPage() {
  const { organizationId = '', teamId = '' } = useParams<{
    organizationId: string;
    teamId: string;
  }>();
  const { team, canManage } = useSprintAccess(organizationId, teamId);
  const list = useSprintList(organizationId, teamId);
  const [dialog, setDialog] = useState<SprintDialog>(null);

  const gone =
    (team.isError && notFoundLevel(team.error) === 'team') ||
    (list.isError && placeSprintRefusal(list.error).teamGone);
  if (gone) {
    return <NotFound level="team" keepNavigation />;
  }

  const rows = list.data?.pages.flatMap((page) => page.items) ?? [];
  const name = team.data?.name ?? t.tableLabel;

  function rowMenu(row: SprintRow): MenuEntry[] {
    const edit = { label: sprintTexts.commands.edit, icon: <EditOutlinedIcon /> };
    if (row.state === 'Planned') {
      return [
        { ...edit, onSelect: () => setDialog({ kind: 'edit', row }) },
        {
          label: sprintTexts.commands.start,
          icon: <PlayArrowOutlinedIcon />,
          onSelect: () => setDialog({ kind: 'start', row }),
        },
        {
          label: sprintTexts.commands.remove,
          icon: <DeleteOutlineOutlinedIcon />,
          destructive: true,
          onSelect: () => setDialog({ kind: 'remove', row }),
        },
      ];
    }
    if (row.state === 'Active') {
      return [
        { ...edit, onSelect: () => setDialog({ kind: 'edit', row }) },
        {
          label: sprintTexts.commands.close,
          icon: <FlagOutlinedIcon />,
          onSelect: () => setDialog({ kind: 'close', row }),
        },
      ];
    }
    return [
      {
        label: sprintTexts.commands.rename,
        icon: <EditOutlinedIcon />,
        onSelect: () => setDialog({ kind: 'rename', row }),
      },
    ];
  }

  const columns: DataColumn<SprintRow>[] = [
    {
      key: 'number',
      header: t.columns.number,
      align: 'right',
      width: 56,
      render: (row) => row.number,
    },
    {
      key: 'name',
      header: t.columns.name,
      render: (row) =>
        row.state === 'Closed' ? (
          <TruncatedText to={sprintOutcomePath(organizationId, teamId, row.id)} look="link">
            {row.name}
          </TruncatedText>
        ) : (
          <TruncatedText>{row.name}</TruncatedText>
        ),
    },
    {
      key: 'state',
      header: t.columns.state,
      render: (row) => <SprintStateTag state={row.state} />,
    },
    { key: 'start', header: t.columns.start, render: (row) => formatDate(row.startDate) },
    { key: 'end', header: t.columns.end, render: (row) => formatDate(row.endDate) },
    {
      key: 'items',
      header: t.columns.items,
      align: 'right',
      render: (row) => figure(row, row.itemCount, row.closedItems),
    },
    {
      key: 'points',
      header: t.columns.storyPoints,
      align: 'right',
      render: (row) => figure(row, row.storyPoints, row.closedStoryPoints),
    },
  ];
  if (canManage) {
    columns.push({
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (row) => (
        <MoreActionsMenu label={t.rowMenu(row.name)} size="row" items={rowMenu(row)} />
      ),
    });
  }

  const create = {
    label: t.create,
    icon: <AddIcon />,
    onClick: () => setDialog({ kind: 'create' }),
  };
  const loading = list.isPending || (list.isError && list.isFetching && rows.length === 0);

  return (
    <>
      <PageHeader
        variant="team"
        title={name}
        documentTitle={sprintTexts.sectionLabel}
        titleIcon={<EventNoteOutlinedIcon aria-hidden="true" sx={{ fontSize: 24 }} />}
        primaryAction={canManage ? create : undefined}
      />
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <DataTable
          aria-label={t.tableLabel}
          columns={columns}
          rows={rows}
          rowKey={(row) => row.id}
          loading={loading}
          error={
            list.isError && !list.isFetching && !list.isFetchNextPageError
              ? t.loadFailed
              : undefined
          }
          onRetry={() => void list.refetch()}
          empty={{
            icon: EventNoteOutlinedIcon,
            heading: t.empty.heading,
            hint: canManage ? t.empty.plannerHint : t.empty.otherHint,
          }}
        />
        {list.isFetchNextPageError && (
          <InlineNotice
            severity="error"
            action={{ label: t.retry, onClick: () => void list.fetchNextPage() }}
          >
            {t.moreFailed}
          </InlineNotice>
        )}
        {list.hasNextPage && (
          <ActionButton
            label={t.more}
            busy={list.isFetchingNextPage}
            onClick={() => void list.fetchNextPage()}
            fullWidth
          />
        )}
      </Box>
      <SprintDialogs
        organizationId={organizationId}
        teamId={teamId}
        dialog={dialog}
        onChange={setDialog}
        onRefresh={({ codes }) => {
          void list.refetch();
          if (codes) void team.refetch();
        }}
      />
    </>
  );
}
