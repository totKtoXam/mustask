import { useCallback, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import AddIcon from '@mui/icons-material/Add';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import ViewWeekOutlinedIcon from '@mui/icons-material/ViewWeekOutlined';
import { useProcessDefinition } from '../../process';
import {
  NEW_WORK_ITEM_LABEL,
  WorkItemCardDialog,
  createdNotice,
  type WorkItemStateReference,
} from '../../workitems';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import { ProjectFilterRow } from '../components/ProjectFilterRow';
import { BoardColumns } from '../components/BoardColumns';
import { BoardSkeleton } from '../components/BoardSkeleton';
import { useNotice } from '../components/Notice';
import { StatusRegion } from '../components/StatusRegion';
import { TeamHead } from '../components/TeamHead';
import { useBoard, useReadAgain } from '../hooks/queries';
import { useNewWorkItem } from '../hooks/useNewWorkItem';
import { emptyBoardHint, isBoardEmpty } from '../lib/board';
import { isBoardLevel, type BoardLevelCode } from '../lib/levels';
import { notFoundLevel } from '../lib/refusals';
import { creationSurface, readName, startTeamOf, teamScope, type BoardScope } from '../lib/scope';

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
 *
 * The body is `ScopedBoard`, which the project's own board shares
 * (`ProjectBoardPage`, feat-039): a team's page reads the team's board; the
 * project's reads the project's, under the team filter (mod-017 US-5), which lives
 * here, above the level. Both create items (mod-017 US-2).
 *
 * A re-read that fails while the columns are shown leaves them as they were —
 * their cards, the open checklists, the scroll — with the error above them; a
 * retry that succeeds takes the error away (feat-032 design § 7.1, "a failed
 * re-read keeps the board and its open lists, with the banner above"; the
 * backlog's rule, feat-027 D-6; feat-039 BR-PB-8 for both pages).
 */
export function BoardPage() {
  const {
    organizationId = '',
    projectId = '',
    teamId = '',
    level,
  } = useParams<{ organizationId: string; projectId: string; teamId: string; level: string }>();
  // One scope per team: the columns' callbacks depend on it.
  const scope = useMemo(() => teamScope(teamId), [teamId]);
  if (!isBoardLevel(level)) {
    return <NotFound level="page" />;
  }
  return (
    <ScopedBoard
      key={`${organizationId}/${projectId}/${teamId}`}
      organizationId={organizationId}
      projectId={projectId}
      scope={scope}
      level={level}
    />
  );
}

/**
 * The board under a head, whatever it is the board of (`scope`): the read's
 * states, the columns or the empty frame, the card's dialog over them, the
 * notice and the announcement region.
 */
export function ScopedBoard({
  organizationId,
  projectId,
  scope,
  level,
}: {
  organizationId: string;
  projectId: string;
  scope: BoardScope;
  level: BoardLevelCode;
}) {
  // mod-017 BR-OT-8: the project's views alone are filtered; the chosen values belong to the page.
  const [teamIds, setTeamIds] = useState<string[]>([]);
  const query = useBoard(organizationId, projectId, scope, level, teamIds);
  // The process names the state a moved card takes while the move is saved;
  // a failed read only leaves the card's old state shown until the board's.
  const process = useProcessDefinition(organizationId);
  const readAgain = useReadAgain(organizationId, projectId, scope);
  // The card's dialog changed an item behind the columns (BR-C-21): the board reads again.
  const readAfterCard = useCallback(() => void readAgain(), [readAgain]);
  const { notice, showSuccess, showError } = useNotice();
  const [announcement, setAnnouncement] = useState('');
  // BR-PB-6 is lifted (mod-017 design § 6.1): the project's board draws «Новый элемент» in its first
  // column and the empty board's action, as a team's does. `canWrite` is what the role holds.
  const newItem = useNewWorkItem({
    organizationId,
    projectId,
    surface: creationSurface(query.data),
    startTeamId: startTeamOf(scope),
    onCreated: (item) => {
      showSuccess(createdNotice(item.identifier, item.team, teamIds));
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
  const data = query.data;
  // Only a page that has never been read is loading: a re-read, or the retry of a failed
  // one, keeps what is shown (design § 7, states 6 and 9).
  const loading = data === undefined && (query.isPending || (query.isError && query.isFetching));
  const canCreate = newItem.canWrite;
  const filtered = teamIds.length > 0;

  return (
    <>
      <TeamHead
        page="board"
        organizationId={organizationId}
        projectId={projectId}
        scope={scope}
        level={level}
        name={readName(data)}
      />
      {scope.kind === 'project' && (
        <ProjectFilterRow
          organizationId={organizationId}
          projectId={projectId}
          teamIds={teamIds}
          onChange={setTeamIds}
        />
      )}
      {loading ? (
        <BoardSkeleton />
      ) : data === undefined ? (
        query.isError && <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        <>
          {query.isError && (
            <Box sx={{ mb: 2 }}>
              <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
            </Box>
          )}
          {isBoardEmpty(data) && filtered ? (
            <StateFrame
              icon={ViewWeekOutlinedIcon}
              heading="Ничего не найдено"
              hint="Ни один рабочий элемент не подходит под заданные условия фильтра."
              action={{
                label: 'Сбросить фильтр',
                icon: <FilterListOffIcon />,
                onClick: () => setTeamIds([]),
              }}
            />
          ) : isBoardEmpty(data) ? (
            <StateFrame
              icon={ViewWeekOutlinedIcon}
              heading="Канбан пуст"
              hint={emptyBoardHint(data)}
              action={
                canCreate
                  ? { label: NEW_WORK_ITEM_LABEL, icon: <AddIcon />, onClick: newItem.open }
                  : undefined
              }
            />
          ) : (
            <BoardColumns
              key={level}
              organizationId={organizationId}
              projectId={projectId}
              scope={scope}
              level={level}
              board={data}
              teamIds={teamIds}
              canWrite={newItem.canWrite}
              newItem={newItem}
              busy={query.isPlaceholderData}
              stateOf={stateOf}
              announce={setAnnouncement}
              showError={showError}
              showCreated={showSuccess}
            />
          )}
        </>
      )}
      {newItem.elements}
      <WorkItemCardDialog
        organizationId={organizationId}
        projectId={projectId}
        onChanged={readAfterCard}
      />
      {notice}
      <StatusRegion text={announcement} />
    </>
  );
}
