import { useCallback, useState } from 'react';
import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import AddIcon from '@mui/icons-material/Add';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { NEW_WORK_ITEM_LABEL } from '../../workitems';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import { backlogSkeletonColumns } from '../components/backlogColumns';
import { BacklogTable } from '../components/BacklogTable';
import { useNotice } from '../components/Notice';
import { StatusRegion } from '../components/StatusRegion';
import { TeamHead } from '../components/TeamHead';
import { useBacklog, useReadTeamAgain } from '../hooks/queries';
import { useNewWorkItem } from '../hooks/useNewWorkItem';
import { isBoardLevel, type BoardLevelCode } from '../lib/levels';
import { notFoundLevel } from '../lib/refusals';

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
  // Each message counts, so one equal to the last is spoken again (F-11).
  const [announcement, setAnnouncement] = useState({ text: '', serial: 0 });
  const announce = useCallback(
    (text: string) => setAnnouncement(({ serial }) => ({ text, serial: serial + 1 })),
    [],
  );
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
        teamId={teamId}
        level={level}
        teamName={data?.team.name}
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
      {notice}
      <StatusRegion text={announcement.text} serial={announcement.serial} />
    </>
  );
}
