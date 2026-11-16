import Box from '@mui/material/Box';
import {
  DataTable,
  ErrorBanner,
  InlineEmpty,
  Section,
  TruncatedText,
  type DataColumn,
} from '../../../shared/ui';
import { formatDate } from '../../../shared/lib/format';
import type { Dynamics, DynamicsRow, Figure, FigureSet, OutcomeQuery } from '../api/iterationsApi';
import { countText, percentText } from '../lib/figures';
import { outcomeSearch } from '../lib/outcomeQuery';
import { sprintTexts } from '../lib/sprintTexts';
import { sprintOutcomePath } from '../paths';

const t = sprintTexts.outcome.dynamics;

type Line =
  | { kind: 'current' | 'sprint'; row: DynamicsRow }
  | { kind: 'summary'; label: string; pick: keyof Figure };

/** The cell of a summary line: the figure's min, average or max, in the column's own format. */
function summaryCell(set: FigureSet, key: keyof FigureSet, pick: keyof Figure): string {
  const value = set[key][pick];
  return key.endsWith('Percent') ? percentText(value) : countText(value);
}

/**
 * «Динамика» (design § 6.3, US-10): this sprint first (bold, «(этот)»), the range's sprints by
 * latest end, then «Мин», «Среднее», «Макс» under the four «закрыто» columns only. A percent is
 * whole, «—» when nothing was held; an average of counts has one decimal with a comma.
 */
export function DynamicsBlock({
  organizationId,
  teamId,
  query,
  read,
}: {
  organizationId: string;
  teamId: string;
  query: OutcomeQuery;
  read: {
    data: Dynamics | undefined;
    isPending: boolean;
    isError: boolean;
    error: unknown;
    refetch(): unknown;
  };
}) {
  const { data } = read;
  const c = t.columns;
  const lines: Line[] = data
    ? [
        { kind: 'current', row: data.current },
        ...data.sprints.map((row): Line => ({ kind: 'sprint', row })),
        { kind: 'summary', label: t.summary.min, pick: 'min' },
        { kind: 'summary', label: t.summary.average, pick: 'average' },
        { kind: 'summary', label: t.summary.max, pick: 'max' },
      ]
    : [];
  const search = outcomeSearch({ level: query.level });
  const summary = data?.summary;

  const bold = (line: Line, text: string) =>
    line.kind === 'current' ? (
      <Box component="span" sx={{ fontWeight: 600 }}>
        {text}
      </Box>
    ) : (
      text
    );
  const real = (line: Line, pick: (row: DynamicsRow) => string) =>
    line.kind === 'summary' ? '' : bold(line, pick(line.row));
  const figureCell = (
    line: Line,
    key: keyof FigureSet,
    own: (row: DynamicsRow) => string,
  ): React.ReactNode =>
    line.kind === 'summary'
      ? summary
        ? summaryCell(summary, key, line.pick)
        : '—'
      : bold(line, own(line.row));

  const columns: DataColumn<Line>[] = [
    {
      key: 'sprint',
      header: c.sprint,
      render: (line) =>
        line.kind === 'summary' ? (
          line.label
        ) : line.kind === 'current' ? (
          bold(line, `${line.row.name} ${t.this}`)
        ) : (
          <TruncatedText
            to={sprintOutcomePath(organizationId, teamId, line.row.id, search)}
            look="link"
          >
            {line.row.name}
          </TruncatedText>
        ),
    },
    { key: 'end', header: c.end, render: (line) => real(line, (row) => formatDate(row.endDate)) },
    {
      key: 'items',
      header: c.items,
      align: 'right',
      render: (line) => real(line, (row) => String(row.items)),
    },
    {
      key: 'points',
      header: c.storyPoints,
      align: 'right',
      render: (line) => real(line, (row) => String(row.storyPoints)),
    },
    {
      key: 'closedItems',
      header: c.closedItems,
      align: 'right',
      render: (line) => figureCell(line, 'closedItems', (row) => String(row.closedItems)),
    },
    {
      key: 'closedItemsPercent',
      header: c.closedItemsPercent,
      align: 'right',
      render: (line) =>
        figureCell(line, 'closedItemsPercent', (row) => percentText(row.closedItemsPercent)),
    },
    {
      key: 'closedStoryPoints',
      header: c.closedStoryPoints,
      align: 'right',
      render: (line) =>
        figureCell(line, 'closedStoryPoints', (row) => String(row.closedStoryPoints)),
    },
    {
      key: 'closedStoryPointsPercent',
      header: c.closedStoryPointsPercent,
      align: 'right',
      render: (line) =>
        figureCell(line, 'closedStoryPointsPercent', (row) =>
          percentText(row.closedStoryPointsPercent),
        ),
    },
  ];

  return (
    <Section title={t.title}>
      {read.isError ? (
        <ErrorBanner error={read.error} title={t.failed} onRetry={() => void read.refetch()} />
      ) : (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          <DataTable
            aria-label={t.tableLabel}
            columns={columns}
            rows={lines}
            rowKey={(line) =>
              line.kind === 'summary' ? `summary-${line.pick}` : `${line.kind}-${line.row.id}`
            }
            loading={read.isPending}
          />
          {data && data.sprints.length === 0 && <InlineEmpty>{t.emptyRange}</InlineEmpty>}
        </Box>
      )}
    </Section>
  );
}
