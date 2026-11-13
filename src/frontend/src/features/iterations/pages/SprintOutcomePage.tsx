import { useState } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import EventNoteOutlinedIcon from '@mui/icons-material/EventNoteOutlined';
import SearchOffIcon from '@mui/icons-material/SearchOff';
import {
  ActionButton,
  ErrorBanner,
  FieldsSkeleton,
  InlineDateField,
  InlineNotice,
  InlineSelectField,
  PageHeader,
  StateFrame,
  type Command,
} from '../../../shared/ui';
import { NotFound } from '../../../shared/ui/StateFrame';
import { formatDate } from '../../../shared/lib/format';
import { notFoundLevel } from '../../organizations';
import type { ClosedSprint, OutcomeLevel, SprintRow } from '../api/iterationsApi';
import { SprintStateTag } from '../components/SprintStateTag';
import { DynamicsBlock } from '../components/DynamicsBlock';
import { ParticipantsBlock } from '../components/ParticipantsBlock';
import { ResultBlock } from '../components/ResultBlock';
import { SprintDialogs, type SprintDialog } from '../components/SprintDialogs';
import { useClosedSprint, useDynamics, useOutcomeItems, useParticipants } from '../hooks/queries';
import { useSprintAccess } from '../hooks/useSprintAccess';
import { OUTCOME_LEVELS, outcomeSearch, parseOutcomeQuery } from '../lib/outcomeQuery';
import { placeSprintRefusal } from '../lib/refusals';
import { sprintTexts } from '../lib/sprintTexts';
import { teamSprintsPath } from '../paths';

const t = sprintTexts.outcome;

/** The Closed sprint as the list's row, for the rename dialog: it needs the name, nothing of the figures. */
function rowOf(closed: ClosedSprint): SprintRow {
  const { sprint } = closed;
  return {
    id: sprint.id,
    number: sprint.number,
    name: sprint.name,
    state: sprint.state,
    startDate: sprint.startDate,
    endDate: sprint.endDate,
    itemCount: closed.itemCount,
    storyPoints: closed.storyPoints,
    closedItems: null,
    closedStoryPoints: null,
  };
}

/**
 * A Closed sprint's outcome (design § 6; US-5, US-9, US-10): the facts, the level and range
 * controls, «Динамика», «Итоги по участникам» and «Итог спринта». The level and the range live
 * in the address, so a view is a link; each block reads and fails on its own.
 */
export function SprintOutcomePage() {
  const {
    organizationId = '',
    teamId = '',
    sprintId = '',
  } = useParams<{ organizationId: string; teamId: string; sprintId: string }>();
  const [params, setParams] = useSearchParams();
  const query = parseOutcomeQuery(params);
  const { team, canManage } = useSprintAccess(organizationId, teamId);
  const closed = useClosedSprint(organizationId, teamId, sprintId);
  const ready = closed.isSuccess;
  const dynamics = useDynamics(organizationId, teamId, sprintId, query, { enabled: ready });
  const participants = useParticipants(organizationId, teamId, sprintId, query, { enabled: ready });
  const items = useOutcomeItems(organizationId, teamId, sprintId, { enabled: ready });
  const [dialog, setDialog] = useState<SprintDialog>(null);
  const [fromDraft, setFromDraft] = useState<string | null>(null);
  const [toDraft, setToDraft] = useState<string | null>(null);
  const [rangeError, setRangeError] = useState<string | undefined>();

  const refusal = closed.isError ? placeSprintRefusal(closed.error) : null;
  if ((team.isError && notFoundLevel(team.error) === 'team') || refusal?.teamGone) {
    return <NotFound level="team" keepNavigation />;
  }
  if (refusal?.sprintGone) {
    return (
      <StateFrame
        icon={SearchOffIcon}
        heading={t.notFound}
        fullPage
        action={{ label: sprintTexts.sectionLabel, to: teamSprintsPath(organizationId, teamId) }}
      />
    );
  }

  const teamName = team.data?.name ?? sprintTexts.sectionLabel;
  const data = closed.data;
  const fromValue = fromDraft ?? query.from ?? dynamics.data?.from ?? '';
  const toValue = toDraft ?? query.to ?? dynamics.data?.to ?? '';

  const go = (next: { level: OutcomeLevel; from?: string; to?: string }) =>
    setParams(outcomeSearch(next).replace(/^\?/, ''));

  const apply = () => {
    if (!fromValue || !toValue) return;
    if (toValue < fromValue) {
      setRangeError(t.datesReversed);
      return;
    }
    setRangeError(undefined);
    setFromDraft(null);
    setToDraft(null);
    go({ level: query.level, from: fromValue, to: toValue });
  };

  const commands: Command[] =
    canManage && data
      ? [
          {
            label: sprintTexts.commands.rename,
            icon: <EditOutlinedIcon />,
            onClick: () => setDialog({ kind: 'rename', row: rowOf(data) }),
          },
        ]
      : [];

  return (
    <>
      <PageHeader
        variant="team"
        title={data ? data.sprint.name : teamName}
        ancestors={[
          { label: sprintTexts.sectionLabel, to: teamSprintsPath(organizationId, teamId) },
        ]}
        titleControl={data ? <SprintStateTag state={data.sprint.state} /> : undefined}
        documentTitle={data ? data.sprint.name : sprintTexts.sectionLabel}
        titleIcon={<EventNoteOutlinedIcon aria-hidden="true" sx={{ fontSize: 24 }} />}
        commands={commands}
      />
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
        {closed.isPending && <FieldsSkeleton count={2} />}
        {closed.isError && (
          <ErrorBanner error={closed.error} onRetry={() => void closed.refetch()} />
        )}
        {data && (
          <>
            <Box>
              <Box component="dl" sx={{ display: 'flex', gap: 4, m: 0, flexWrap: 'wrap' }}>
                <Box>
                  <Box component="dt" sx={{ color: 'text.secondary', typography: 'caption' }}>
                    {t.dates}
                  </Box>
                  <Box component="dd" sx={{ m: 0 }}>
                    {`${formatDate(data.sprint.startDate)} – ${formatDate(data.sprint.endDate)}`}
                  </Box>
                </Box>
                <Box>
                  <Box component="dt" sx={{ color: 'text.secondary', typography: 'caption' }}>
                    {t.closedBy}
                  </Box>
                  <Box component="dd" sx={{ m: 0 }}>
                    {`${data.closedByName ?? '—'}, ${formatDate(data.sprint.closedAt)}`}
                  </Box>
                </Box>
              </Box>
            </Box>
            <Box sx={{ display: 'flex', gap: 2, alignItems: 'flex-start', flexWrap: 'wrap' }}>
              <Box sx={{ minWidth: 180 }}>
                <InlineSelectField<OutcomeLevel>
                  label={t.level}
                  frame="always"
                  value={query.level}
                  options={OUTCOME_LEVELS.map((level) => ({
                    value: level,
                    label: t.levels[level],
                  }))}
                  onChange={(level) => {
                    if (level) go({ ...query, level });
                  }}
                />
              </Box>
              <InlineDateField
                label={t.from}
                frame="always"
                value={fromValue}
                onChange={(value) => {
                  setFromDraft(value);
                  setRangeError(undefined);
                }}
              />
              <InlineDateField
                label={t.to}
                frame="always"
                value={toValue}
                error={rangeError}
                onChange={(value) => {
                  setToDraft(value);
                  setRangeError(undefined);
                }}
              />
              <Box sx={{ pt: 3 }}>
                <ActionButton label={t.apply} onClick={apply} />
              </Box>
            </Box>
            {dynamics.data && (
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                <Box sx={{ color: 'text.secondary', typography: 'body2' }}>
                  {t.inRange(dynamics.data.rangeSprints)}
                </Box>
                {dynamics.data.truncated && (
                  <InlineNotice severity="info">
                    {t.capped(dynamics.data.shownSprints, dynamics.data.rangeSprints)}
                  </InlineNotice>
                )}
              </Box>
            )}
            <DynamicsBlock
              organizationId={organizationId}
              teamId={teamId}
              query={query}
              read={dynamics}
            />
            <ParticipantsBlock read={participants} />
            <ResultBlock organizationId={organizationId} read={items} />
          </>
        )}
      </Box>
      <SprintDialogs
        organizationId={organizationId}
        teamId={teamId}
        dialog={dialog}
        onChange={setDialog}
        onRefresh={({ codes }) => {
          void closed.refetch();
          if (codes) void team.refetch();
        }}
      />
    </>
  );
}
