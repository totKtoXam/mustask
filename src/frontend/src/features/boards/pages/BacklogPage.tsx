import { useCallback, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import AddIcon from '@mui/icons-material/Add';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { NEW_WORK_ITEM_LABEL, WorkItemCardDialog } from '../../workitems';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import { backlogSkeletonColumns } from '../components/backlogColumns';
import { BacklogTable } from '../components/BacklogTable';
import { useNotice } from '../components/Notice';
import { StatusRegion } from '../components/StatusRegion';
import { TeamHead } from '../components/TeamHead';
import { useBacklog, useReadAgain } from '../hooks/queries';
import { useNewWorkItem } from '../hooks/useNewWorkItem';
import { isBoardLevel, type BoardLevelCode } from '../lib/levels';
import { notFoundLevel } from '../lib/refusals';
import { creationSurface, readName, teamScope, type BoardScope } from '../lib/scope';

/**
 * «Бэклоги» (feat-027 US-1..US-6; spec § Page «Бэклоги»; mod-015 § 10.5 without
 * «Итерация» and the planning pane): a team's ordered backlog of one level,
 * under the team head (BR-T-16) — the team's name, the tab «Бэклог», the level
 * drop-down at its right end, «Новый рабочий элемент» and «Показать канбан».
 * `:level` is one of the three or the address is unknown (mod-015 BR-13); the
 * backlog's read validates the rest of the address. Keyed by its team, so
 * another team starts afresh, while choosing a level in the level drop-down
 * (feat-001 US-6) keeps the head and the drop-down's focus. The table is the
 * backlog's whole tree (US-2) with the inline add of a child (US-4), a row
 * dropped onto another (US-5) and any row's parent changed from its menu (US-6).
 *
 * A re-read that fails while the table is shown leaves the table as it was —
 * its rows, what is expanded, the selection, the scroll — with the error above
 * it; a retry that succeeds takes the error away (tech-012 m2, folded in by
 * feat-027 D-6).
 *
 * The body is `ScopedBacklog`, which the project's own backlog shares
 * (`ProjectBacklogPage`, feat-039): a team's page, a team's read and the
 * creation of an item in the team; the project's, the project's read and none.
 */
export function BacklogPage() {
  const {
    organizationId = '',
    projectId = '',
    teamId = '',
    level,
  } = useParams<{ organizationId: string; projectId: string; teamId: string; level: string }>();
  // One scope per team: the table's and the board's callbacks depend on it.
  const scope = useMemo(() => teamScope(teamId), [teamId]);
  if (!isBoardLevel(level)) {
    return <NotFound level="page" />;
  }
  return (
    <ScopedBacklog
      key={`${organizationId}/${projectId}/${teamId}`}
      organizationId={organizationId}
      projectId={projectId}
      scope={scope}
      level={level}
    />
  );
}

/**
 * The backlog under a head, whatever it is the backlog of (`scope`): the read's
 * states, the table or the empty frame, the card's dialog over them, the notice
 * and the announcement region.
 */
export function ScopedBacklog({
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
  const query = useBacklog(organizationId, projectId, scope, level);
  const readAgain = useReadAgain(organizationId, projectId, scope);
  // The card's dialog changed an item behind the table (BR-C-21): the backlog reads again.
  const readAfterCard = useCallback(() => void readAgain(), [readAgain]);
  const { notice, showSuccess, showSuccessAlert, showError } = useNotice();
  // Each message counts, so one equal to the last is spoken again (F-11).
  const [announcement, setAnnouncement] = useState({ text: '', serial: 0 });
  const announce = useCallback(
    (text: string) => setAnnouncement(({ serial }) => ({ text, serial: serial + 1 })),
    [],
  );
  // The project's answer names no team, so the project's page has no surface to create
  // in: no primary command and no action on an empty backlog (feat-039 BR-PB-6).
  const newItem = useNewWorkItem({
    organizationId,
    projectId,
    surface: creationSurface(query.data),
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

  const data = query.data;
  // Only a page that has never been read is loading: a re-read, or the retry
  // of a failed one, keeps what is shown.
  const loading = data === undefined && (query.isPending || (query.isError && query.isFetching));
  const primaryAction = newItem.command && { ...newItem.command, hideMenuIndicator: true };

  return (
    <>
      <TeamHead
        page="backlog"
        organizationId={organizationId}
        projectId={projectId}
        scope={scope}
        level={level}
        name={readName(data)}
        primaryAction={primaryAction}
      />
      {loading ? (
        <RegistrySkeleton columns={backlogSkeletonColumns()} />
      ) : data === undefined ? (
        query.isError && <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        <>
          {query.isError && (
            <Box sx={{ mb: 2 }}>
              <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
            </Box>
          )}
          {data.items.length === 0 ? (
            scope.kind === 'project' ? (
              <StateFrame
                icon={InboxOutlinedIcon}
                heading="Бэклог пуст"
                hint="В проекте нет рабочих элементов этого уровня."
              />
            ) : (
              <StateFrame
                icon={InboxOutlinedIcon}
                heading="Бэклог пуст"
                hint={
                  newItem.canWrite
                    ? 'Добавьте рабочий элемент — он встанет в конец бэклога.'
                    : 'Рабочие элементы создают участники организации с ролью «Участник» или «Администратор».'
                }
                action={
                  newItem.canWrite
                    ? { label: NEW_WORK_ITEM_LABEL, icon: <AddIcon />, onClick: newItem.open }
                    : undefined
                }
              />
            )
          ) : (
            <BacklogTable
              key={level}
              organizationId={organizationId}
              projectId={projectId}
              scope={scope}
              level={level}
              items={data.items}
              announce={announce}
              showSuccess={showSuccessAlert}
              showCreated={showSuccess}
              showError={showError}
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
      <StatusRegion text={announcement.text} serial={announcement.serial} />
    </>
  );
}
