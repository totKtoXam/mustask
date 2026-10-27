import { useCallback, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import AddIcon from '@mui/icons-material/Add';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import {
  NEW_WORK_ITEM_LABEL,
  WorkItemCardDialog,
  createdNotice,
  createdNoticeOnTeamView,
} from '../../workitems';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import { backlogSkeletonColumns } from '../components/backlogColumns';
import { BacklogTable } from '../components/BacklogTable';
import { useNotice } from '../components/Notice';
import { StatusRegion } from '../components/StatusRegion';
import { TeamHead } from '../components/TeamHead';
import { ViewFilterRow } from '../components/ViewFilterRow';
import { useBacklog, useReadAgain } from '../hooks/queries';
import { useNewWorkItem } from '../hooks/useNewWorkItem';
import { useViewFilter } from '../hooks/useViewFilter';
import { NO_PROJECT_HINT } from '../lib/board';
import { isBoardLevel, type BoardLevelCode } from '../lib/levels';
import { notFoundLevel } from '../lib/refusals';
import { creationSurface, readName, readProjects, teamScope, type BoardScope } from '../lib/scope';

/**
 * «Бэклоги» (feat-027 US-1..US-6; spec § Page «Бэклоги»; mod-015 § 10.5 without
 * «Итерация» and the planning pane): a team's ordered backlog of one level,
 * under the team head (BR-T-16) — the team's name, the tab «Бэклог», the level
 * drop-down at its right end, «Новый рабочий элемент» and «Показать канбан».
 * Since feat-040 the page is at the team's address, `/organizations/:o/teams/:t/backlogs/:level`,
 * and holds the items the team owns in every project it works on (BR-TS-1, BR-TS-2).
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
 * (`ProjectBacklogPage`, feat-039): a team's page reads the team's backlog, under the project
 * filter (feat-040 BR-TS-8); the project's reads the project's, under the team filter (mod-017
 * US-5). The filter lives here, above the level, and in the page address (mod-020; OQ-2 A). Both
 * create items (mod-017 US-2; feat-040 US-5): a team's in the project chosen in the dialog, the
 * project's in the project.
 */
export function BacklogPage() {
  const {
    organizationId = '',
    teamId = '',
    level,
  } = useParams<{ organizationId: string; teamId: string; level: string }>();
  // One scope per team: the table's and the board's callbacks depend on it.
  const scope = useMemo(() => teamScope(teamId), [teamId]);
  if (!isBoardLevel(level)) {
    return <NotFound level="page" />;
  }
  return (
    <ScopedBacklog
      key={`${organizationId}/${teamId}`}
      organizationId={organizationId}
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
  scope,
  level,
}: {
  organizationId: string;
  scope: BoardScope;
  level: BoardLevelCode;
}) {
  // The view's one filter lives in the page's address (mod-020, feat-040 OQ-2 A): the project
  // filter on a team's views, the team filter on the project's.
  const [filterIds, setFilterIds] = useViewFilter(scope);
  const query = useBacklog(organizationId, scope, level, filterIds);
  const readAgain = useReadAgain(organizationId, scope);
  // The card's dialog changed an item behind the table (BR-C-21): the backlog reads again.
  const readAfterCard = useCallback(() => void readAgain(), [readAgain]);
  const { notice, showSuccess, showSuccessAlert, showError } = useNotice();
  // Each message counts, so one equal to the last is spoken again (F-11).
  const [announcement, setAnnouncement] = useState({ text: '', serial: 0 });
  const announce = useCallback(
    (text: string) => setAnnouncement(({ serial }) => ({ text, serial: serial + 1 })),
    [],
  );
  // BR-PB-6 is lifted (mod-017 design § 6.1): the project's views create too.
  const newItem = useNewWorkItem({
    organizationId,
    scope,
    surface: creationSurface(query.data),
    teamName: readName(query.data),
    filterIds,
    onCreated: (item) => {
      showSuccess(
        scope.kind === 'team'
          ? createdNoticeOnTeamView(item.identifier, item.team, item.project, {
              teamId: scope.teamId,
              projectIds: filterIds,
            })
          : createdNotice(item.identifier, item.team, filterIds),
      );
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
  const filtered = filterIds.length > 0;
  // A team that works on no active project (BR-TS-2): nothing to show, filter or create by.
  const worksOnNoProject = readProjects(data)?.length === 0;

  return (
    <>
      <TeamHead
        page="backlog"
        organizationId={organizationId}
        scope={scope}
        level={level}
        name={readName(data)}
        primaryAction={primaryAction}
      />
      <ViewFilterRow
        organizationId={organizationId}
        scope={scope}
        filterIds={filterIds}
        read={{
          data,
          failed: data === undefined && query.isError && !query.isFetching,
          refetch: () => void query.refetch(),
        }}
        onChange={setFilterIds}
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
            filtered ? (
              <StateFrame
                icon={FilterListOffIcon}
                heading="Ничего не найдено"
                hint="Ни один рабочий элемент не подходит под заданные условия фильтра."
                action={{
                  label: 'Сбросить фильтр',
                  icon: <FilterListOffIcon />,
                  onClick: () => setFilterIds([]),
                }}
              />
            ) : (
              <StateFrame
                icon={InboxOutlinedIcon}
                heading="Бэклог пуст"
                hint={
                  worksOnNoProject
                    ? NO_PROJECT_HINT
                    : newItem.canWrite
                      ? 'Добавьте рабочий элемент — он встанет в конец бэклога.'
                      : 'Рабочие элементы создают участники организации с ролью «Участник» или «Администратор».'
                }
                action={
                  newItem.canCreate
                    ? { label: NEW_WORK_ITEM_LABEL, icon: <AddIcon />, onClick: newItem.open }
                    : undefined
                }
              />
            )
          ) : (
            <BacklogTable
              key={level}
              organizationId={organizationId}
              scope={scope}
              level={level}
              items={data.items}
              filterIds={filterIds}
              announce={announce}
              showSuccess={showSuccessAlert}
              showCreated={showSuccess}
              showError={showError}
              busy={query.isPlaceholderData}
            />
          )}
        </>
      )}
      {newItem.elements}
      <WorkItemCardDialog
        organizationId={organizationId}
        projectId={scope.kind === 'project' ? scope.projectId : undefined}
        onChanged={readAfterCard}
      />
      {notice}
      <StatusRegion text={announcement.text} serial={announcement.serial} />
    </>
  );
}
