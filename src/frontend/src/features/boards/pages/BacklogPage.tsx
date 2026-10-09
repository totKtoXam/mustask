import { useState } from 'react';
import { useParams } from 'react-router-dom';
import AddIcon from '@mui/icons-material/Add';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import ViewWeekOutlinedIcon from '@mui/icons-material/ViewWeekOutlined';
import { NEW_WORK_ITEM_LABEL } from '../../workitems';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import { backlogSkeletonColumns } from '../components/backlogColumns';
import { BacklogTable } from '../components/BacklogTable';
import { LevelMenu } from '../components/LevelMenu';
import { useNotice } from '../components/Notice';
import { StatusRegion } from '../components/StatusRegion';
import { useBacklog, useReadTeamAgain } from '../hooks/queries';
import { useNewWorkItem } from '../hooks/useNewWorkItem';
import { isBoardLevel, type BoardLevelCode } from '../lib/levels';
import { notFoundLevel } from '../lib/refusals';
import { backlogPath, boardPath } from '../paths';

/**
 * spec § Page «Бэклоги»: the secondary command to the same team's and level's
 * board, named «Канбан» since feat-001 (BR-H-16).
 */
const SHOW_BOARD_LABEL = 'Показать канбан';

/**
 * «Бэклоги» (US-1, US-2, US-3, US-7; spec § Page «Бэклоги»; mod-015 § 10.5
 * without «Итерация» and the planning pane): a team's ordered backlog of one
 * level. `:level` is one of the three or the address is unknown (mod-015
 * BR-13); the backlog's read validates the rest of the address. Keyed by its
 * team, so another team starts afresh, while choosing a level in the level
 * drop-down (feat-001 US-6) keeps the header and the drop-down's focus. Since
 * feat-001 the table is the backlog's tree with the «Родитель» column (US-4)
 * and, on «Фичи» and «Истории», the row menu that maps a parent (US-5).
 */
export function BacklogPage() {
  const {
    organizationId = '',
    projectId = '',
    teamId = '',
    level,
  } = useParams<{ organizationId: string; projectId: string; teamId: string; level: string }>();
  if (!isBoardLevel(level)) {
    return <NotFound level="page" />;
  }
  return (
    <TeamBacklog
      key={`${organizationId}/${projectId}/${teamId}`}
      organizationId={organizationId}
      projectId={projectId}
      teamId={teamId}
      level={level}
    />
  );
}

function TeamBacklog({
  organizationId,
  projectId,
  teamId,
  level,
}: {
  organizationId: string;
  projectId: string;
  teamId: string;
  level: BoardLevelCode;
}) {
  const query = useBacklog(organizationId, projectId, teamId, level);
  const readAgain = useReadTeamAgain(organizationId, projectId, teamId);
  const { notice, showSuccess, showSuccessAlert, showError } = useNotice();
  const [announcement, setAnnouncement] = useState('');
  const newItem = useNewWorkItem({
    organizationId,
    projectId,
    surface: query.data,
    onCreated: (item) => {
      showSuccess(`Рабочий элемент ${item.identifier} создан.`);
      void readAgain();
    },
    onContextGone: () => void readAgain(),
  });

  const frame = query.isError ? notFoundLevel(query.error) : null;
  if (frame) {
    return <NotFound level={frame} />;
  }

  const loading = query.isPending || (query.isError && query.isFetching);

  return (
    <>
      <PageHeader
        title="Бэклоги"
        primaryAction={newItem.command}
        commands={[
          {
            label: SHOW_BOARD_LABEL,
            icon: <ViewWeekOutlinedIcon />,
            to: boardPath(organizationId, projectId, teamId, level),
          },
        ]}
      />
      <LevelMenu
        level={level}
        pathOf={(other) => backlogPath(organizationId, projectId, teamId, other)}
      />
      {loading ? (
        <RegistrySkeleton columns={backlogSkeletonColumns(level)} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : query.data.items.length === 0 ? (
        <StateFrame
          icon={InboxOutlinedIcon}
          heading="Бэклог пуст"
          hint="Добавьте рабочий элемент — он встанет в конец бэклога."
          action={{ label: NEW_WORK_ITEM_LABEL, icon: <AddIcon />, onClick: newItem.open }}
        />
      ) : (
        <BacklogTable
          key={level}
          organizationId={organizationId}
          projectId={projectId}
          teamId={teamId}
          level={level}
          items={query.data.items}
          announce={setAnnouncement}
          showSuccess={showSuccessAlert}
          showError={showError}
        />
      )}
      {newItem.elements}
      {notice}
      <StatusRegion text={announcement} />
    </>
  );
}
