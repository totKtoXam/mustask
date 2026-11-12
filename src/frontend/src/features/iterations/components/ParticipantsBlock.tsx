import { Fragment, useState } from 'react';
import Box from '@mui/material/Box';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import {
  ActionButton,
  BodyRow,
  Cell,
  ErrorBanner,
  HeadCell,
  InlineEmpty,
  InlineNotice,
  PersonLabel,
  RowControl,
  Section,
  TableCard,
  TableSkeleton,
  Tag,
} from '../../../shared/ui';
import type { FigureSet, Participant, ParticipantsPage } from '../api/iterationsApi';
import { countText, percentText } from '../lib/figures';
import { sprintTexts } from '../lib/sprintTexts';

const t = sprintTexts.outcome.participants;
const COLUMN_COUNT = 9;

/** The read the block takes: the infinite query's own fields, no more. */
export interface ParticipantsRead {
  data: { pages: ParticipantsPage[] } | undefined;
  isPending: boolean;
  isError: boolean;
  error: unknown;
  hasNextPage: boolean;
  isFetchingNextPage: boolean;
  isFetchNextPageError: boolean;
  refetch(): unknown;
  fetchNextPage(): unknown;
}

const rowKeyOf = (row: Participant) => row.userId ?? 'unassigned';

function nameOf(row: Participant) {
  if (row.userId === null) return <PersonLabel name={null} emptyLabel={t.noAssignee} />;
  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 1 }}>
      <PersonLabel name={row.displayName} emptyLabel={t.unknownUser} />
      {!row.isMember && <Tag label={t.notInOrganization} />}
    </Box>
  );
}

/**
 * A person's own min, average and max, as rows under the person's: the same columns, so each
 * figure stands under its own header (design § 6.4). A person compared across no sprint has one
 * line saying so.
 */
function NestedRows({ row, rangeSprints }: { row: Participant; rangeSprints: number }) {
  if (row.comparedSprints === 0) {
    return (
      <BodyRow>
        <Cell />
        <Cell>
          <InlineEmpty>{t.noData}</InlineEmpty>
        </Cell>
        {Array.from({ length: COLUMN_COUNT - 2 }, (_, index) => (
          <Cell key={index} />
        ))}
      </BodyRow>
    );
  }
  const lines: { label: string; pick: 'min' | 'average' | 'max' }[] = [
    { label: sprintTexts.outcome.dynamics.summary.min, pick: 'min' },
    { label: sprintTexts.outcome.dynamics.summary.average, pick: 'average' },
    { label: sprintTexts.outcome.dynamics.summary.max, pick: 'max' },
  ];
  const keys: (keyof FigureSet)[] = [
    'closedStoryPoints',
    'closedStoryPointsPercent',
    'closedItems',
    'closedItemsPercent',
  ];
  const text = (key: keyof FigureSet, pick: 'min' | 'average' | 'max') =>
    key.endsWith('Percent')
      ? percentText(row.dynamics[key][pick])
      : countText(row.dynamics[key][pick]);
  return (
    <>
      {lines.map((line, index) => (
        <BodyRow key={line.pick}>
          <Cell />
          <Cell>
            <Box component="span" sx={{ color: 'text.secondary', pl: 2 }}>
              {line.label}
            </Box>
          </Cell>
          <Cell />
          <Cell />
          {keys.map((key) => (
            <Cell key={key} align="right">
              {text(key, line.pick)}
            </Cell>
          ))}
          <Cell align="right">
            {index === 0 ? (
              <Box component="span" sx={{ color: 'text.secondary' }}>
                {t.caption(row.comparedSprints, rangeSprints)}
              </Box>
            ) : null}
          </Cell>
        </BodyRow>
      ))}
    </>
  );
}

/**
 * «Итоги по участникам» (design § 6.4, US-10): a person a row, «Без исполнителя» and «Итого» in
 * the same columns, «Сравнение» as «k из N»; a row opens to the person's own min, average and
 * max. The table is built from the table parts since a row expands under itself.
 */
export function ParticipantsBlock({ read }: { read: ParticipantsRead }) {
  const [open, setOpen] = useState<ReadonlySet<string>>(new Set());
  const pages = read.data?.pages ?? [];
  const rows = pages.flatMap((page) => page.rows);
  const first = pages[0];
  const total = pages.at(-1)?.total;
  const rangeSprints = first?.rangeSprints ?? 0;

  const toggle = (key: string) =>
    setOpen((current) => {
      const next = new Set(current);
      if (!next.delete(key)) next.add(key);
      return next;
    });

  let body: React.ReactNode;
  if (read.isError && rows.length === 0) {
    body = <ErrorBanner error={read.error} title={t.failed} onRetry={() => void read.refetch()} />;
  } else if (read.isPending) {
    body = <TableSkeleton headers={[t.columns.person, t.columns.items, t.columns.storyPoints]} />;
  } else if (rows.length === 0) {
    body = <InlineEmpty>{t.empty}</InlineEmpty>;
  } else {
    body = (
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <TableCard
          aria-label={t.tableLabel}
          head={
            <>
              <HeadCell label="" width={40} />
              <HeadCell label={t.columns.person} />
              <HeadCell label={t.columns.items} align="right" />
              <HeadCell label={t.columns.storyPoints} align="right" />
              <HeadCell label={t.columns.closedStoryPoints} align="right" />
              <HeadCell label={t.columns.closedStoryPointsPercent} align="right" />
              <HeadCell label={t.columns.closedItems} align="right" />
              <HeadCell label={t.columns.closedItemsPercent} align="right" />
              <HeadCell label={t.columns.compared} align="right" />
            </>
          }
        >
          {rows.map((row) => {
            const key = rowKeyOf(row);
            const expanded = open.has(key);
            const name = row.displayName ?? (row.userId === null ? t.noAssignee : t.unknownUser);
            return (
              <Fragment key={key}>
                <BodyRow>
                  <Cell>
                    <RowControl
                      label={t.expand(name)}
                      icon={expanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}
                      aria-expanded={expanded}
                      onClick={() => toggle(key)}
                    />
                  </Cell>
                  <Cell>{nameOf(row)}</Cell>
                  <Cell align="right">{row.items}</Cell>
                  <Cell align="right">{row.storyPoints}</Cell>
                  <Cell align="right">{row.closedStoryPoints}</Cell>
                  <Cell align="right">{percentText(row.closedStoryPointsPercent)}</Cell>
                  <Cell align="right">{row.closedItems}</Cell>
                  <Cell align="right">{percentText(row.closedItemsPercent)}</Cell>
                  <Cell align="right">{t.comparedOf(row.comparedSprints, rangeSprints)}</Cell>
                </BodyRow>
                {expanded && <NestedRows row={row} rangeSprints={rangeSprints} />}
              </Fragment>
            );
          })}
          {total && (
            <BodyRow>
              <Cell />
              <Cell>
                <Box component="span" sx={{ fontWeight: 600 }}>
                  {t.total}
                </Box>
              </Cell>
              <Cell align="right">{total.items}</Cell>
              <Cell align="right">{total.storyPoints}</Cell>
              <Cell align="right">{total.closedStoryPoints}</Cell>
              <Cell align="right">{percentText(total.closedStoryPointsPercent)}</Cell>
              <Cell align="right">{total.closedItems}</Cell>
              <Cell align="right">{percentText(total.closedItemsPercent)}</Cell>
              <Cell />
            </BodyRow>
          )}
        </TableCard>
        {read.isFetchNextPageError && (
          <InlineNotice
            severity="error"
            action={{ label: sprintTexts.outcome.retry, onClick: () => void read.fetchNextPage() }}
          >
            {t.failed}
          </InlineNotice>
        )}
        {read.hasNextPage && (
          <ActionButton
            label={sprintTexts.outcome.more}
            busy={read.isFetchingNextPage}
            onClick={() => void read.fetchNextPage()}
            fullWidth
          />
        )}
      </Box>
    );
  }

  return <Section title={t.title}>{body}</Section>;
}
