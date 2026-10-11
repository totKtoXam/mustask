import { useState } from 'react';
import { useParams } from 'react-router-dom';
import AddIcon from '@mui/icons-material/Add';
import ViewWeekOutlinedIcon from '@mui/icons-material/ViewWeekOutlined';
import { useProcessDefinition } from '../../process';
import { NEW_WORK_ITEM_LABEL, type WorkItemStateReference } from '../../workitems';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import { BoardColumns } from '../components/BoardColumns';
import { BoardSkeleton } from '../components/BoardSkeleton';
import { useNotice } from '../components/Notice';
import { StatusRegion } from '../components/StatusRegion';
import { TeamHead } from '../components/TeamHead';
import { useBoard, useReadTeamAgain } from '../hooks/queries';
import { useNewWorkItem } from '../hooks/useNewWorkItem';
import { isBoardEmpty } from '../lib/board';
import { isBoardLevel, type BoardLevelCode } from '../lib/levels';
import { notFoundLevel } from '../lib/refusals';

/**
 * «Канбан» (US-4, US-5, US-6, US-7's board scenario; spec § Page «Доски»,
 * renamed by feat-001 BR-H-16 — the address segment `boards` stays; mod-015
 * § 10.4 without «Настройки доски» and column collapse): a team's board of
 * one level. `:level` is one of the three or the address is unknown (mod-015
 * BR-13); the board's read validates the rest of the address. Keyed by its
 * team, so another team starts afresh, while choosing a level in the level
 * drop-down (feat-001 US-6) keeps the header and the drop-down's focus. Its
 * head is the backlog's (feat-027 BR-T-16): the team's name, the tab «Канбан»,
 * the level drop-down at its right end and «Показать бэклог»; a board has no
 * primary action in the head (mod-015 § 10.4).
 */
export function BoardPage() {
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
    <TeamBoard
      key={`${organizationId}/${projectId}/${teamId}`}
      organizationId={organizationId}
      projectId={projectId}
      teamId={teamId}
      level={level}
    />
  );
}

function TeamBoard({
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
  const query = useBoard(organizationId, projectId, teamId, level);
  // The process names the state a moved card takes while the move is saved;
  // a failed read only leaves the card's old state shown until the board's.
  const process = useProcessDefinition(organizationId);
  const readAgain = useReadTeamAgain(organizationId, projectId, teamId);
  const { notice, showSuccess, showError } = useNotice();
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

  const stateOf = (typeId: string, stateId: string): WorkItemStateReference | undefined => {
    const state = process.data?.workItemTypes
      .find((type) => type.id === typeId)
      ?.states.find((candidate) => candidate.id === stateId);
    return state && { id: state.id, name: state.name, category: state.category };
  };
  const loading = query.isPending || (query.isError && query.isFetching);

  return (
    <>
      <TeamHead
        page="board"
        organizationId={organizationId}
        projectId={projectId}
        teamId={teamId}
        level={level}
        teamName={query.data?.team.name}
      />
      {loading ? (
        <BoardSkeleton />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : isBoardEmpty(query.data) ? (
        <StateFrame
          icon={ViewWeekOutlinedIcon}
          heading="Канбан пуст"
          hint="Новые рабочие элементы этого уровня появятся в колонке «Новые»."
          action={{ label: NEW_WORK_ITEM_LABEL, icon: <AddIcon />, onClick: newItem.open }}
        />
      ) : (
        <BoardColumns
          key={level}
          organizationId={organizationId}
          projectId={projectId}
          teamId={teamId}
          level={level}
          board={query.data}
          newItem={newItem}
          stateOf={stateOf}
          announce={setAnnouncement}
          showError={showError}
        />
      )}
      {newItem.elements}
      {notice}
      <StatusRegion text={announcement} />
    </>
  );
}
